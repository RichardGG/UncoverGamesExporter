using Newtonsoft.Json;
using Playnite.SDK;
using System;
using System.Linq;
using System.Threading.Tasks;
using Playnite.SDK.Models;
using System.Data;

namespace UncoverGamesExporter.Services
{
    internal class Exporter
    {
        private IPlayniteAPI playniteApi;

        public Exporter(IPlayniteAPI playniteApi)
        {
            this.playniteApi = playniteApi;
        }

        public async void Export()
        {
            Configuration config = Configuration.GetInstance();
            GoogleDrive drive = new GoogleDrive();

            // TODO we probably don't need to do this anymore
            drive.ConfigureClient(config);

            if (!config.IsConfigured())
            {
                Playnite.SDK.API.Instance.Notifications.Add(new NotificationMessage(
                    id: "uncover.games:not-configured",
                    text: "Could not sync. Click to sign in to Uncover.Games",
                    type: NotificationType.Error,
                    action: delegate () { drive.StartAuthentication(this.Export); }
                ));
                return;
            }
            if (config.HasExpired())
            {
                drive.RefreshToken(this.Export);
                return;
            }

            IItemCollection<Game> games = Playnite.SDK.API.Instance.Database.Games;

            UploadData(drive, "Games.json", games);
            UploadData(drive, "AgeRatings.json", Playnite.SDK.API.Instance.Database.AgeRatings);
            UploadData(drive, "Categories.json", Playnite.SDK.API.Instance.Database.Categories);
            UploadData(drive, "Companies.json", Playnite.SDK.API.Instance.Database.Companies);
            UploadData(drive, "CompletionStatuses.json", Playnite.SDK.API.Instance.Database.CompletionStatuses);
            UploadData(drive, "Emulators.json", Playnite.SDK.API.Instance.Database.Emulators);
            UploadData(drive, "Features.json", Playnite.SDK.API.Instance.Database.Features);
            UploadData(drive, "FilterPresets.json", Playnite.SDK.API.Instance.Database.FilterPresets);
            UploadData(drive, "GameScanners.json", Playnite.SDK.API.Instance.Database.GameScanners);
            UploadData(drive, "Genres.json", Playnite.SDK.API.Instance.Database.Genres);
            UploadData(drive, "Platforms.json", Playnite.SDK.API.Instance.Database.Platforms);
            UploadData(drive, "Regions.json", Playnite.SDK.API.Instance.Database.Regions);
            UploadData(drive, "Series.json", Playnite.SDK.API.Instance.Database.Series);
            UploadData(drive, "Sources.json", Playnite.SDK.API.Instance.Database.Sources);
            UploadData(drive, "Tags.json", Playnite.SDK.API.Instance.Database.Tags);

            ListItem[] files = await drive.ListFiles();

            int gamesCount = games.Count;
            int progress = 0;
            string assetNotificationId = "uncover.games:uploading-assets";

            int batchSize = 10;
            int currentBatch = 0;

            // TODO investigate SemaphoreSlim
            // https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-7.0

            while (currentBatch * batchSize < gamesCount)
            {
                Game[] batch = games.Skip(currentBatch * batchSize)
                    .Take(batchSize).ToArray();
                // TODO need to refresh token when getting an error from auth
                await Task.WhenAll(batch.Select(async (game) =>
                    {
                        // TODO check last modified date of assets instead of never updating
                        if (game.CoverImage != null && !files.Any(file => file.name == game.CoverImage.Replace("\\", "_")))
                        {
                            try
                            {
                                await drive.UploadAsset(game.CoverImage);
                            }
                            catch (Exception e)
                            {
                                // Sometimes throws in PrepareUpload: System.NullReferenceException: Object reference not set to an instance of an object.
                            }
                        }
                        progress++;
                        Playnite.SDK.API.Instance.Notifications.Remove(assetNotificationId);
                        Playnite.SDK.API.Instance.Notifications.Add(new NotificationMessage(
                            id: assetNotificationId,
                            text: "Uncover.Games - Uploading assets " + progress + "/" + gamesCount,
                            type: NotificationType.Info
                        ));
                    }
                ));
                currentBatch++;
            }
        }

        public async void UploadData(GoogleDrive drive, string uploadName, object data)
        {
            string json = JsonConvert.SerializeObject(data);

            string fileId = await drive.GetFileId(uploadName);
            if (fileId != "")
            {
                drive.UpdateJson(fileId, json);
            }
            else
            {
                drive.UploadJson(uploadName, json);
            }
        }
    }
}
