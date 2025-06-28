using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.Backend;
using GW2_Addon_Manager.Dependencies.FileSystem;
using Localization;
using Microsoft.WindowsAPICodePack.Dialogs;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GW2_Addon_Manager
{
    /// <summary>
    /// Code-behind for OpeningView.xaml.
    /// </summary>
    public partial class OpeningView : Page
    {
        private const string ReleasesUrl = "https://github.com/Draeggiar/GW2-Addon-Manager/releases";
        private const string UpdateNotificationFile = "updatenotification.txt";

        private readonly IConfigurationManager _configurationManager;
        private readonly PluginManagement _pluginManagement;
        private readonly OpeningViewModel _viewModel;

        /// <summary>
        /// This constructor deals with creating or expanding the configuration file, setting the DataContext, and checking for application updates.
        /// </summary>
        public OpeningView()
        {
            _viewModel = OpeningViewModel.GetInstance;
            DataContext = _viewModel;

            _configurationManager = new ConfigurationManager();
            _pluginManagement = new PluginManagement(_configurationManager);
            _pluginManagement.DisplayAddonStatus();
            
            InitializeComponent();

            //update notification
            if (File.Exists(UpdateNotificationFile))
            {
                Process.Start(ReleasesUrl);
                File.Delete(UpdateNotificationFile);
            }
        }

        public override void BeginInit()
        {
            var configuration = new Configuration(_configurationManager, new UpdateHelper(new HttpClientFactory()), new FileSystemManager());        
            Task.Run(() => SetUpdateButtonVisibilityAsync(configuration));
            base.BeginInit();
        }

        private async Task SetUpdateButtonVisibilityAsync(Configuration configuration)
        {
            var checkResult = await configuration.CheckIfNewVersionIsAvailableAsync();
            if (!checkResult.isUpdateAvailable) return;

            _viewModel.UpdateAvailable = $"{checkResult.latestVersion} {StaticText.Available.ToLower()}!";
            _viewModel.UpdateLinkVisibility = Visibility.Visible;
        }


        /**** What Add-On Is Selected ****/
        /// <summary>
        /// Takes care of description page text updating
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void addOnList_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            AddonInfoFromYaml selected = OpeningViewModel.GetInstance.AddonList[addons.SelectedIndex];
            OpeningViewModel.GetInstance.DescriptionText = selected.description;
            OpeningViewModel.GetInstance.DeveloperText = selected.developer;
            OpeningViewModel.GetInstance.AddonWebsiteLink = selected.website;

            OpeningViewModel.GetInstance.DeveloperVisibility = Visibility.Visible;
        }

        /***************************** NAV BAR *****************************/
        private void TitleBar_MouseHeld(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                Application.Current.MainWindow.DragMove();
        }



        //race condition with processes
        private void close_clicked(object sender, RoutedEventArgs e)
        {
            SelfUpdate.startUpdater();
            Application.Current.Shutdown();
        }

        private void minimize_clicked(object sender, RoutedEventArgs e)
        {
            (this.Parent as Window).WindowState = WindowState.Minimized;
        }

        /*****************************   ***   *****************************/


        //just calls PluginManagement.ForceRedownload(); and then update_button_clicked
        private void RedownloadAddons(object sender, RoutedEventArgs e)
        {
            if (_pluginManagement.ForceRedownload())
                update_button_clicked(sender, e);
        }


        /***** UPDATE button *****/
        private void update_button_clicked(object sender, RoutedEventArgs e)
        {
            //If bin folder doesn't exist then LoaderSetup intialization will fail.
            if (_configurationManager.UserConfig.BinFolder == null)
            {
                MessageBox.Show("Unable to locate Guild Wars 2 /bin/ or /bin64/ folder." + Environment.NewLine + "Please verify Game Path is correct.",
                                "Unable to Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<AddonInfoFromYaml> selectedAddons = new List<AddonInfoFromYaml>();

            //the d3d9 wrapper is installed by default and hidden from the list displayed to the user, so it has to be added to this list manually
            AddonInfoFromYaml wrapper = AddonYamlReader.getAddonInInfo("d3d9_wrapper");
            wrapper.folder_name = "d3d9_wrapper";
            selectedAddons.Add(wrapper);

            foreach (AddonInfoFromYaml addon in OpeningViewModel.GetInstance.AddonList.Where(add => add.IsSelected == true))
            {
                selectedAddons.Add(addon);
            }

            Application.Current.Properties["ForceLoader"] = false;
            Application.Current.Properties["Selected"] = selectedAddons;

            this.NavigationService.Navigate(new Uri("UI/UpdatingPage/UpdatingView.xaml", UriKind.Relative));
        }

        private void reinstallLoader_Click(object sender, RoutedEventArgs e)
        {
            //If bin folder doesn't exist then LoaderSetup intialization will fail.
            if (_configurationManager.UserConfig.BinFolder == null)
            {
                MessageBox.Show("Unable to locate Guild Wars 2 /bin/ or /bin64/ folder." + Environment.NewLine + "Please verify Game Path is correct.",
                                "Unable to Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Application.Current.Properties["ForceLoader"] = true;
            Application.Current.Properties["Selected"] = new List<AddonInfoFromYaml>();

            this.NavigationService.Navigate(new Uri("UI/UpdatingPage/UpdatingView.xaml", UriKind.Relative));
        }

        /***** Hyperlink Handler *****/
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void SelectDirectoryBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var pathSelectionDialog = new CommonOpenFileDialog { IsFolderPicker = true };
            var result = pathSelectionDialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
                OpeningViewModel.GetInstance.GamePath = pathSelectionDialog.FileName;
                
        }
    }
}
