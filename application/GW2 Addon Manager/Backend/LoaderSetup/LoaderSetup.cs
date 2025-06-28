using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.Backend;

namespace GW2_Addon_Manager
{
    class LoaderSetup
    {
        const string loader_git_url = "https://api.github.com/repos/gw2-addon-loader/loader-core/releases/latest";

        private readonly IConfigurationManager _configurationManager;
        string loader_game_path;

        UpdatingViewModel viewModel;
        string fileName;
        string latestLoaderVersion;
        string[] loader_dxgi_destination;
        string loader_d3d11_destination;
        string loader_self_destination;

        /// <summary>
        /// Constructor; also sets some UI text to indicate that the addon loader is having an update check
        /// </summary>
        public LoaderSetup(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager;
            viewModel = UpdatingViewModel.GetInstance;
            viewModel.ProgBarLabel = "Checking for updates to Addon Loader";
            loader_game_path = configurationManager.UserConfig.GamePath;
        }

        /// <summary>
        /// Checks for update to addon loader and downloads if a new release is available
        /// </summary>
        /// <returns></returns>
        public async Task HandleLoaderUpdate(bool force)
        {
            dynamic releaseInfo = await new UpdateHelper(new HttpClientFactory()).GitReleaseInfoAsync(loader_git_url);
            if (releaseInfo == null)
                return;

            loader_d3d11_destination = Path.Combine(loader_game_path, "d3d11.dll");
            loader_dxgi_destination = new string[]
            {
                Path.Combine(loader_game_path, "bin64/cef/dxgi.dll"),
                Path.Combine(loader_game_path, "bin64/dxgi.dll"),
                Path.Combine(loader_game_path, "dxgi.dll")
            };
            loader_self_destination = Path.Combine(loader_game_path, "addonLoader.dll");

            latestLoaderVersion = releaseInfo.tag_name;

            if (!force && File.Exists(loader_d3d11_destination) &&
                loader_dxgi_destination.All(File.Exists) &&
                File.Exists(loader_self_destination) &&
                _configurationManager.UserConfig.LoaderVersion == latestLoaderVersion)
                return;

            string downloadLink = releaseInfo.assets[0].browser_download_url;
            await Download(downloadLink);
        }

        private async Task Download(string url)
        {
            viewModel.ProgBarLabel = "Downloading Addon Loader";

            fileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(url));

            if (File.Exists(fileName))
                File.Delete(fileName);

            using (var client = new HttpClientFactory().Create())
            {
                using (var response = await client.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    await SaveFileWithProgress(response);
                }
            }

            Install();
        }

        private async Task SaveFileWithProgress(HttpResponseMessage response)
        {
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;
            var buffer = new byte[81920];
            long totalRead = 0;
            using var stream = await response.Content.ReadAsStreamAsync();
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (canReportProgress)
                {
                    viewModel.DownloadProgress = (int)(totalRead * 100 / totalBytes);
                }
            }
        }

        private void Install()
        {
            viewModel.ProgBarLabel = "Installing Addon Loader";

            if (File.Exists(loader_d3d11_destination))
                File.Delete(loader_d3d11_destination);

            foreach (var x in loader_dxgi_destination)
                if (File.Exists(x))
                    File.Delete(x);

            if (File.Exists(loader_self_destination))
                File.Delete(loader_self_destination);

            ZipFile.ExtractToDirectory(fileName, loader_game_path);

            _configurationManager.UserConfig.LoaderVersion = latestLoaderVersion;
            _configurationManager.SaveConfiguration();
        }
    }
}