using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.Backend;
using GW2_Addon_Manager.Backend.Updating;
using Newtonsoft.Json;

namespace GW2_Addon_Manager;

public class UpdateHelper : IUpdateHelper
{
    private readonly IHttpClientFactory _clientFactory;

    public UpdateHelper(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<string> DownloadStringFromGithubApiAsync(string url)
    {
        try
        {
            using var httpClient = _clientFactory.Create();
            return await httpClient.GetStringAsync(url).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show(
                "Github servers returned an error; please try again in a few minutes.\n\nThe error was: " +
                ex.Message, "Github API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw ex;
        }
    }

    public async Task DownloadFileFromGithubApiAsync(string url, string destPath)
    {
        try
        {
            using var httpClient = _clientFactory.Create();

            using var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            using var fs = File.Create(destPath);
            await response.Content.CopyToAsync(fs);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show(
                "Github servers returned an error; please try again in a few minutes.\n\nThe error was: " +
                ex.Message, "Github API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw ex;
        }
    }

    public async Task<dynamic> GitReleaseInfoAsync(string gitUrl)
    {
        var release_info_json = await DownloadStringFromGithubApiAsync(gitUrl).ConfigureAwait(false);
        return JsonConvert.DeserializeObject(release_info_json);
    }

    public static async Task UpdateAllAsync()
    {
        var viewModel = UpdatingViewModel.GetInstance;

        var settingUp = new LoaderSetup(new ConfigurationManager());
        await settingUp.HandleLoaderUpdate((bool)Application.Current.Properties["ForceLoader"]);

        var addons = (List<AddonInfoFromYaml>)Application.Current.Properties["Selected"];

        var configurationManager = new ConfigurationManager();
        foreach (var addon in addons.Where(add => add != null))
        {
            var updater = new GenericUpdater(addon, configurationManager);

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