using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.App.Configuration.Model;
using GW2_Addon_Manager.Dependencies.FileSystem;
using GW2_Addon_Manager.Dependencies.WebClient;
using Microsoft.VisualBasic.FileIO;

namespace GW2_Addon_Manager.Backend.Updating
{
    internal class GenericUpdater
    {
        private const string DisabledPluginsDirectory = "Disabled Plugins";
        private readonly string _addonExpandedPath;
        private readonly AddonInfoFromYaml _addonInfo;
        private readonly string _addonInstallPath;

        private readonly string _addonName;

        private readonly IConfigurationManager _configurationManager;
        private readonly IFileSystemManager _fileSystemManager;

        private readonly UpdatingViewModel _viewModel;

        private string _fileName;

        private string _latestVersion;

        public GenericUpdater(AddonInfoFromYaml addon, IConfigurationManager configurationManager)
        {
            _fileSystemManager = new FileSystemManager();
            _addonName = addon.folder_name;
            _addonInfo = addon;
            _configurationManager = configurationManager;
            _viewModel = UpdatingViewModel.GetInstance;

            _addonExpandedPath = Path.Combine(Path.GetTempPath(), _addonName);
            _addonInstallPath = Path.Combine(configurationManager.UserConfig.GamePath, "addons");
        }

        public Task Update()
        {
            var disabledAddonsNames =
                _configurationManager.UserConfig.AddonsList.Where(a => a.Disabled).Select(a => a.Name);
            if (!disabledAddonsNames.Contains(_addonName))
                return _addonInfo.host_type == "github" ? GitCheckUpdate() : StandaloneCheckUpdate();
            return Task.CompletedTask;
        }

        /***** UPDATE CHECK *****/

        /// <summary>
        ///     Checks whether an update is required and performs it for an add-on hosted on Github.
        /// </summary>
        private async Task GitCheckUpdate()
        {
            var client = new WebClient();
            client.Headers.Add("User-Agent", "request");

            var releaseInfo = new UpdateHelper(new WebClientWrapper()).GitReleaseInfo(_addonInfo.host_url);
            if (releaseInfo == null)
                return;
            _latestVersion = releaseInfo.tag_name;

            var currentAddonVersion =
                _configurationManager.UserConfig.AddonsList.FirstOrDefault(a => a.Name == _addonName);
            if (currentAddonVersion != null && currentAddonVersion.Version == _latestVersion)
                return;

            string downloadLink = releaseInfo.assets[0].browser_download_url;
            _viewModel.ProgBarLabel = "Downloading " + _addonInfo.addon_name + " " + _latestVersion;
            await Download(downloadLink, client);
        }

        private async Task StandaloneCheckUpdate()
        {
            var client = new WebClient();
            var downloadUrl = _addonInfo.host_url;

            if (_addonInfo.version_url != null)
            {
                _latestVersion = client.DownloadString(_addonInfo.version_url);
            }
            else
            {
                //for self-updating addons' first installation
                _viewModel.ProgBarLabel = "Downloading " + _addonInfo.addon_name;
                await Download(downloadUrl, client);
                return;
            }

            var currentAddonVersion =
                _configurationManager.UserConfig.AddonsList.FirstOrDefault(a => a.Name == _addonName);
            if (currentAddonVersion != null && currentAddonVersion.Version == _latestVersion)
                return;

            _viewModel.ProgBarLabel = "Downloading " + _addonInfo.addon_name + " " + _latestVersion;
            await Download(downloadUrl, client);
        }


        /***** DOWNLOAD *****/

        /// <summary>
        ///     Downloads an add-on from the url specified in <paramref name="url" /> using the WebClient provided in
        ///     <paramref name="client" />.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="client"></param>
        private async Task Download(string url, WebClient client)
        {
            //this calls helper method to fetch filename if it is not exposed in URL
            _fileName = Path.Combine(
                Path.GetTempPath(),
                _addonInfo.additional_flags != null && _addonInfo.additional_flags.Contains("obscured-filename")
                    ? GetFilenameFromWebServer(url)
                    : Path.GetFileName(url)
            );

            if (File.Exists(_fileName))
                File.Delete(_fileName);

            client.DownloadProgressChanged += addon_DownloadProgressChanged;

            await client.DownloadFileTaskAsync(new Uri(url), _fileName);
            Install();
        }

        /* helper method */
        /* credit: Fidel @ stackexchange
         * modified version if their answer at https://stackoverflow.com/a/54616044/9170673
         */
        public string GetFilenameFromWebServer(string url)
        {
            var result = "";

            var req = WebRequest.Create(url);
            req.Method = "GET";
            using (var resp = req.GetResponse())
            {
                result = Path.GetFileName(resp.ResponseUri.AbsoluteUri);
            }

            return result;
        }

        /***** INSTALL *****/

        /// <summary>
        ///     Performs archive extraction and file IO operations to install the downloaded addon.
        /// </summary>
        private void Install()
        {
            _viewModel.ProgBarLabel = "Installing " + _addonInfo.addon_name;

            if (_addonInfo.download_type == "archive")
            {
                if (Directory.Exists(_addonExpandedPath))
                    Directory.Delete(_addonExpandedPath, true);

                ZipFile.ExtractToDirectory(_fileName, _addonExpandedPath);


                if (_addonInfo.install_mode != "arc")
                {
                    FileSystem.CopyDirectory(_addonExpandedPath, _addonInstallPath, true);
                }
                else
                {
                    if (!Directory.Exists(Path.Combine(_addonInstallPath, "arcdps")))
                        Directory.CreateDirectory(Path.Combine(_addonInstallPath, "arcdps"));

                    File.Copy(Path.Combine(_addonExpandedPath, _addonInfo.plugin_name),
                        Path.Combine(Path.Combine(_addonInstallPath, "arcdps"), _addonInfo.plugin_name), true);
                }
            }
            else
            {
                if (_addonInfo.install_mode != "arc")
                {
                    if (!Directory.Exists(Path.Combine(_addonInstallPath, _addonInfo.folder_name)))
                        Directory.CreateDirectory(Path.Combine(_addonInstallPath, _addonInfo.folder_name));

                    FileSystem.CopyFile(_fileName,
                        Path.Combine(Path.Combine(_addonInstallPath, _addonInfo.folder_name),
                            Path.GetFileName(_fileName)), true);
                }
                else
                {
                    if (!Directory.Exists(Path.Combine(_addonInstallPath, "arcdps")))
                        Directory.CreateDirectory(Path.Combine(_addonInstallPath, "arcdps"));

                    FileSystem.CopyFile(_fileName,
                        Path.Combine(Path.Combine(_addonInstallPath, "arcdps"), Path.GetFileName(_fileName)), true);
                }
            }

            //removing download from temp folder to avoid naming clashes
            FileSystem.DeleteFile(_fileName);

            var addonConfig = _configurationManager.UserConfig.AddonsList.FirstOrDefault(a => a.Name == _addonName);
            if (addonConfig != null)
            {
                addonConfig.Version = _latestVersion;
            }
            else
            {
                var newAddonConfig = new AddonData {Name = _addonName, Installed = true, Version = _latestVersion};
                _configurationManager.UserConfig.AddonsList.Add(newAddonConfig);
            }

            _configurationManager.SaveConfiguration();
        }


        /***** DISABLE *****/
        //TODO: Note to self May 1 2020: consider making some vanity methods to clean up all the Path.Combine()s in here; the code's a bit of a chore to read.
        public void Disable()
        {
            var addonConfiguration =
                GetAddonConfig();
            if (addonConfiguration != null && addonConfiguration.Installed && !addonConfiguration.Disabled)
            {
                if (_addonInfo.install_mode != "arc")
                {
                    _fileSystemManager.DirectoryMove(
                        Path.Combine(_addonInstallPath, _addonInfo.folder_name),
                        Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name));
                }
                else
                {
                    //probably broken
                    if (!Directory.Exists(Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name)))
                        Directory.CreateDirectory(Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name));

                    if (_addonInfo.addon_name.Contains("BuildPad"))
                    {
                        var buildPadFileName = "";
                        var arcFiles = Directory.GetFiles(Path.Combine(_configurationManager.UserConfig.GamePath,
                            "addons/arcdps"));

                        //search for plugin name in arc folder
                        //TODO: Should break out of operation and give message if the plugin is not found.
                        foreach (var arcFileName in arcFiles)
                            if (arcFileName.Contains("buildpad"))
                                buildPadFileName = Path.GetFileName(arcFileName);

                        File.Move(
                            Path.Combine(_addonInstallPath, "arcdps", buildPadFileName),
                            Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name, buildPadFileName)
                        );
                    }
                    else
                    {
                        File.Move(
                            Path.Combine(_addonInstallPath, "arcdps",
                                _addonInfo.plugin_name),
                            Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name, _addonInfo.plugin_name)
                        );
                    }
                }

                addonConfiguration.Disabled = true;
            }
        }

        private AddonData GetAddonConfig() => _configurationManager.UserConfig.AddonsList.FirstOrDefault(a =>
            string.Compare(a.Name, _addonInfo.folder_name, StringComparison.InvariantCultureIgnoreCase) == 0);

        /***** ENABLE *****/
        public void Enable()
        {
            var addonConfiguration =
                GetAddonConfig();
            if (addonConfiguration != null && addonConfiguration.Installed && addonConfiguration.Disabled)
            {
                if (_addonInfo.install_mode != "arc")
                {
                    //non-arc
                    _fileSystemManager.DirectoryMove(
                        Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name),
                        Path.Combine(_addonInstallPath, _addonInfo.folder_name)
                    );
                }
                else
                {
                    //arc
                    if (!Directory.Exists(Path.Combine(_addonInstallPath, "arcdps")))
                        Directory.CreateDirectory(Path.Combine(_addonInstallPath,
                            "arcdps"));

                    //buildpad compatibility check
                    if (!_addonInfo.addon_name.Contains("BuildPad"))
                    {
                        //non-buildpad
                        File.Move(
                            Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name, _addonInfo.plugin_name),
                            Path.Combine(_addonInstallPath, "arcdps",
                                _addonInfo.plugin_name)
                        );
                    }
                    else
                    {
                        //buildpad
                        var buildPadFileName = "";
                        var buildPadFiles =
                            Directory.GetFiles(Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name));

                        foreach (var someFileName in buildPadFiles)
                            if (someFileName.Contains("buildpad"))
                                buildPadFileName = Path.GetFileName(someFileName);

                        File.Move(
                            Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name, buildPadFileName),
                            Path.Combine(_addonInstallPath, "arcdps",
                                buildPadFileName)
                        );
                    }
                }

                addonConfiguration.Disabled = false;
            }
        }

        /***** DELETE *****/
        public void Delete()
        {
            var addonConfiguration =
                GetAddonConfig();
            if (addonConfiguration != null && addonConfiguration.Installed)
            {
                _configurationManager.UserConfig.AddonsList.Remove(_addonName);

                if (addonConfiguration.Disabled)
                {
                    FileSystem.DeleteDirectory(Path.Combine(DisabledPluginsDirectory, _addonInfo.folder_name),
                        UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else
                {
                    if (_addonInfo.install_mode != "arc")
                    {
                        FileSystem.DeleteDirectory(
                            Path.Combine(_addonInstallPath, _addonInfo.folder_name),
                            UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

                        //deleting arcdps will delete other addons as well
                        if (_addonInfo.folder_name == "arcdps")
                            foreach (var adjInfo in new ApprovedList(_configurationManager).GenerateAddonList())
                                if (adjInfo.install_mode == "arc")
                                {
                                    var arcDependantConfig =
                                        _configurationManager.UserConfig.AddonsList.First(a =>
                                            a.Name == adjInfo.addon_name);
                                    //if arc-dependent plugin is disabled, it won't get deleted since it's not in the /addons/arcdps folder
                                    if (!arcDependantConfig.Disabled)
                                        _configurationManager.UserConfig.AddonsList.Remove(adjInfo.addon_name);
                                }
                    }
                    else
                    {
                        //buildpad check
                        if (!_addonInfo.addon_name.Contains("BuildPad"))
                        {
                            FileSystem.DeleteFile(
                                Path.Combine(_addonInstallPath, "arcdps",
                                    _addonInfo.plugin_name), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            var buildPadFileName = "";
                            var arcFiles = Directory.GetFiles(Path.Combine(_addonInstallPath, "arcdps"));

                            //search for plugin name in arc folder
                            //TODO: Should break out of operation and give message if the plugin is not found.
                            foreach (var arcFileName in arcFiles)
                                if (arcFileName.Contains("buildpad"))
                                    buildPadFileName = Path.GetFileName(arcFileName);

                            FileSystem.DeleteFile(
                                Path.Combine(_addonInstallPath, "arcdps",
                                    buildPadFileName), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                    }
                }
            }
        }

        /***** DOWNLOAD EVENTS *****/
        private void addon_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _viewModel.DownloadProgress = e.ProgressPercentage;
        }
    }
}