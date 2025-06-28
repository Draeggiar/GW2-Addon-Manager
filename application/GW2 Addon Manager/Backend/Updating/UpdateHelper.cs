using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.Backend.Updating;
using GW2_Addon_Manager.Dependencies.WebClient;
using JetBrains.Annotations;

namespace GW2_Addon_Manager
{
    public class UpdateHelper
    {
        private readonly IWebClient _webClient;

        public UpdateHelper([NotNull] IWebClient webClient)
        {
            _webClient = webClient ?? throw new ArgumentNullException(nameof(webClient));
        }

        public string DownloadStringFromGithubAPI(string url)
        {
            try
            {
                return _webClient.DownloadString(url);
            }
            catch (WebException ex)
            {
                MessageBox.Show(
                    "Github servers returned an error; please try again in a few minutes.\n\nThe error was: " +
                    ex.Message, "Github API Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw ex;
            }
        }

        public void DownloadFileFromGithubAPI(string url, string destPath)
        {
            try
            {
                _webClient.DownloadFile(url, destPath);
            }
            catch (WebException ex)
            {
                MessageBox.Show(
                    "Github servers returned an error; please try again in a few minutes.\n\nThe error was: " +
                    ex.Message, "Github API Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw ex;
            }
        }

        public dynamic GitReleaseInfo(string gitUrl)
        {
            var release_info_json = DownloadStringFromGithubAPI(gitUrl);
            return JsonConvert.DeserializeObject(release_info_json);
        }

        public static async Task UpdateAllAsync()
        {
            UpdatingViewModel viewModel = UpdatingViewModel.GetInstance;

            LoaderSetup settingUp = new LoaderSetup(new ConfigurationManager());
            await settingUp.HandleLoaderUpdate((bool)Application.Current.Properties["ForceLoader"]);

            List<AddonInfoFromYaml> addons = (List<AddonInfoFromYaml>)Application.Current.Properties["Selected"];

            var configurationManager = new ConfigurationManager();
            foreach (AddonInfoFromYaml addon in addons.Where(add => add != null))
            {
                GenericUpdater updater = new GenericUpdater(addon, configurationManager);

                if (!(addon.additional_flags != null && addon.additional_flags.Contains("self-updating")
                                                     && configurationManager.UserConfig.AddonsList
                                                         .FirstOrDefault(a => a.Name == addon.addon_name)?.Installed ==
                                                     true))
                    await updater.Update();
            }

            viewModel.ProgBarLabel = "Updates Complete";
            viewModel.DownloadProgress = 100;
            viewModel.CloseBtnEnabled = true;
            viewModel.BackBtnEnabled = true;
        }
    }
}