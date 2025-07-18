﻿using System.Linq;
using System.Windows;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.Backend;
using GW2_Addon_Manager.Backend.Updating;
using GW2_Addon_Manager.Dependencies.FileSystem;

namespace GW2_Addon_Manager
{
    class PluginManagement
    {
        private readonly IConfigurationManager _configurationManager;

        public PluginManagement(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager;
        }

        /// <summary>
        /// Sets version fields of all installed and enabled addons to a dummy value so they are redownloaded, then starts update process.
        /// Intended for use if a user borks their install (probably by manually deleting something in the /addons/ folder).
        /// </summary>
        public bool ForceRedownload()
        {
            string redownloadmsg = "This will forcibly redownload all installed addons regardless of their version. Do you wish to continue?";
            if (MessageBox.Show(redownloadmsg, "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _configurationManager.UserConfig.AddonsList.Where(a => a.Installed).ToList().ForEach(a => a.Version = "dummy value");
                _configurationManager.SaveConfiguration();
                return true;
            }
            return false; 
        }

        /// <summary>
        /// Deletes all addons and resets config to default state.
        /// <seealso cref="OpeningViewModel.CleanInstall"/>
        /// <seealso cref="Configuration.DeleteAllAddons"/>
        /// </summary>
        public void DeleteAll()
        {
            string deletemsg = "This will delete ALL add-ons from Guild Wars 2 and all data associated with them! Are you sure you wish to continue?";
            string secondPrecautionaryMsg = "Are you absolutely sure you want to delete all addons? This action cannot be undone.";

            //precautionary "are you SURE" messages x2
            if (MessageBox.Show(deletemsg, "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                if (MessageBox.Show(secondPrecautionaryMsg, "Absolutely Sure?", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    new Configuration(_configurationManager, new UpdateHelper(new HttpClientFactory()), new FileSystemManager()).DeleteAllAddons();
                    DisplayAddonStatus();

                    //post-delete info message
                    MessageBox.Show("All addons have been removed.", "Reverted to Clean Install", MessageBoxButton.OK, MessageBoxImage.Information);
                }
        }

        /// <summary>
        /// Deletes the currently selected addons.
        /// <seealso cref="OpeningViewModel.DeleteSelected"/>
        /// </summary>
        public void DeleteSelected()
        {
            const string deletemsg = "This will delete any add-ons that are selected and all data associated with them! Are you sure you wish to continue?";
            if (MessageBox.Show(deletemsg, "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var addonsToRemove = OpeningViewModel.GetInstance.AddonList.Where(add => add.IsSelected).ToArray();
                foreach (var addon in addonsToRemove)
                {
                    new GenericUpdater(addon, _configurationManager).Delete();
                    ChangeAddonStatus(addon);
                }
                if(addonsToRemove.Any())
                    _configurationManager.SaveConfiguration();
            }
        }

        /// <summary>
        /// Disables the currently selected addons.
        /// <seealso cref="OpeningViewModel.DisableSelected"/>
        /// </summary>
        public void DisableSelected()
        {
            const string disablemsg = "This will disable the selected add-ons until you choose to re-enable them. Do you wish to continue?";
            if (MessageBox.Show(disablemsg, "Disable", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                var addonsToDisable = OpeningViewModel.GetInstance.AddonList.Where(add => add.IsSelected).ToArray();
                foreach (var addon in addonsToDisable)
                {
                    new GenericUpdater(addon, _configurationManager).Disable();
                    ChangeAddonStatus(addon);
                }
                if(addonsToDisable.Any())
                    _configurationManager.SaveConfiguration();
            }
        }

        /// <summary>
        /// Enables the currently selected addons.
        /// <seealso cref="OpeningViewModel.EnableSelected"/>
        /// </summary>
        public void EnableSelected()
        {
            const string enablemsg = "This will enable any of the selected add-ons that are disabled. Do you wish to continue?";
            if (MessageBox.Show(enablemsg, "Enable", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                var addonsToEnable = OpeningViewModel.GetInstance.AddonList.Where(add => add.IsSelected).ToArray();
                foreach (var addon in addonsToEnable)
                {
                    new GenericUpdater(addon, _configurationManager).Enable();
                    ChangeAddonStatus(addon);
                }
                if(addonsToEnable.Any())
                    _configurationManager.SaveConfiguration();
            }
        }

        /// <summary>
        /// Displays the latest status of the plugins on the opening screen (disabled, enabled, version, installed).
        /// </summary>
        public void DisplayAddonStatus()
        {
            foreach (var addon in OpeningViewModel.GetInstance.AddonList)
            {
                var addonConfig =
                    _configurationManager.UserConfig.AddonsList[addon.folder_name];
                if (addonConfig == null) continue;

                addon.addon_name = AddonYamlReader.getAddonInInfo(addon.folder_name).addon_name;
                ChangeAddonStatus(addon);
            }
        }

        private void ChangeAddonStatus(AddonInfoFromYaml addon)
        {
            var addonConfig =
                _configurationManager.UserConfig.AddonsList[addon.folder_name];

            var newStatus = string.Empty;
            if (addonConfig.Installed)
            {
                if (addon.folder_name == "arcdps" || addon.folder_name == "buildPad" || addonConfig.Version.Length > 10)
                    newStatus += "(installed)";
                else
                    newStatus += "(" + addonConfig.Version + " installed)";
            }

            if (addonConfig.Disabled)
                newStatus += "(disabled)";

            if (addon.Status != newStatus)
                addon.Status = newStatus;
        }
    }
}
