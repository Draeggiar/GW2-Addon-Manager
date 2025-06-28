using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using GW2_Addon_Manager.Backend;
using Localization;

namespace GW2_Addon_Manager
{
    /// <summary>
    /// Handles downloading a new version of the application.
    /// </summary>
    class SelfUpdate
    {
        static readonly string applicationRepoUrl = "https://api.github.com/repos/fmmmlee/GW2-Addon-Manager/releases/latest";
        static readonly string update_folder = "latestRelease";
        static readonly string update_name = "update.zip";

        OpeningViewModel viewModel;

        public static void Update()
        {
            new SelfUpdate();
        }

        /// <summary>
        /// Sets the viewmodel and starts the download of the latest release.
        /// </summary>
        private SelfUpdate()
        {
            viewModel = OpeningViewModel.GetInstance;
            viewModel.UpdateProgressVisibility = Visibility.Visible;
            viewModel.UpdateLinkVisibility = Visibility.Hidden;
            Task.Run(() => downloadLatestRelease());
        }

        /// <summary>
        /// Downloads the latest application release.
        /// </summary>
        /// <returns></returns>
        public async Task downloadLatestRelease()
        {
            //perhaps change this to check if downloaded update is latest or not
            if (Directory.Exists(update_folder))
                Directory.Delete(update_folder, true);

            //check application version
            var httpClientFactory = new HttpClientFactory();
            dynamic latestInfo = await new UpdateHelper(httpClientFactory).GitReleaseInfoAsync(applicationRepoUrl);
            if (latestInfo == null)
                return;

            string downloadUrl = latestInfo.assets[0].browser_download_url;
            viewModel.UpdateAvailable = $"{StaticText.Downloading} {latestInfo.tag_name}";

            Directory.CreateDirectory(update_folder);
            
            using var client = httpClientFactory.Create();
            var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            await SaveFilesWithProgress(response);
        }

        private async Task SaveFilesWithProgress(HttpResponseMessage response)
        {
            using (var fs = new FileStream(Path.Combine(update_folder, update_name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;
                var buffer = new byte[81920];
                long totalRead = 0;
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (!canReportProgress) continue;
                        var progress = (int)(totalRead * 100 / totalBytes);
                        Application.Current.Dispatcher.Invoke(() => viewModel.UpdateDownloadProgress = progress);
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(() => selfUpdate_DownloadCompleted(this, new AsyncCompletedEventArgs(null, false, null)));
        }

        /* updating download status on UI */
        private void selfUpdate_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            viewModel.UpdateAvailable = $"{StaticText.DownloadComplete}!";
            Application.Current.Properties["update_self"] = true;
        }


        /// <summary>
        /// Starts the self-updater if an application update has been downloaded.
        /// </summary>
        public static void startUpdater()
        {
            if(Application.Current.Properties["update_self"] != null && (bool)Application.Current.Properties["update_self"])
                Process.Start("UOAOM Updater.exe");
        }
    }
}
