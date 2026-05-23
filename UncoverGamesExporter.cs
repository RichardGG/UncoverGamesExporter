using Microsoft.Extensions.Configuration;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using UncoverGamesExporter.Services;

namespace UncoverGamesExporter
{
    class AppSettings
    {
        public string clientId;
        public string clientSecret;
    }

    public class UncoverGamesExporter : GenericPlugin
    {
        private UncoverGamesExporterSettingsViewModel settings { get; set; }
        private AppSettings appSettings;

        private IPlayniteAPI playniteApi;
        private Exporter exporter;

        public override Guid Id { get; } = Guid.Parse("d0ec6248-2b1f-421a-9ed4-722e5623f61f");

        public UncoverGamesExporter(IPlayniteAPI api) : base(api)
        {
            this.playniteApi = api;
            this.settings = new UncoverGamesExporterSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };

            Configuration config = Configuration.GetInstance();
            config.SetPluginDataPath(GetPluginUserDataPath());

            this.appSettings = new AppSettings
            {
                clientId = EnvironmentDetails.ClientId,
                clientSecret = EnvironmentDetails.ClientSecret,
            };
            this.exporter = new Exporter(this.playniteApi, this.appSettings);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            this.exporter.Export();
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            this.exporter.Export();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@",
                Description = "Export to Uncover.Games",
                Action = ManualExport
            };
        }
        private void ManualExport(MainMenuItemActionArgs args)
        {
            this.exporter.Export();
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return this.settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new UncoverGamesExporterSettingsView();
        }
    }
}