using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Extensions;
using Jellyfin.Plugin.MediaBar.Configuration;
using Jellyfin.Plugin.MediaBar.JellyfinVersionSpecific;
using Jellyfin.Plugin.MediaBar.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.MediaBar.Helpers
{
    public static class TransformationPatches
    {
        public static string AvatarsList(PatchRequestPayload payload)
        {
            IPlaylistManager playlistManager = MediaBarPlugin.Instance.ServiceProvider.GetRequiredService<IPlaylistManager>();
            IUserManager userManager = MediaBarPlugin.Instance.ServiceProvider.GetRequiredService<IUserManager>();

            return AvatarsList(payload, playlistManager, userManager) ?? "";
        }
        
        private static string? AvatarsList(PatchRequestPayload payload, IPlaylistManager playlistManager, IUserManager userManager)
        {
            if (MediaBarPlugin.Instance.Configuration.UseAvatarsFile)
            {
                return payload.Contents;
            }
            
            IEnumerable<Guid> allUserIds = userManager.GetAllUserIds();

            Playlist? playlist = null;
            Guid? userIdToUse = null;

            foreach (Guid userId in allUserIds)
            {
                playlist = playlistManager.GetPlaylists(userId)
                    .FirstOrDefault(x => x.Name == MediaBarPlugin.Instance.Configuration.AvatarsPlaylist);

                if (playlist != null)
                {
                    userIdToUse = userId;
                    break;
                }
            }

            if (playlist == null || userIdToUse == null)
            {
                return payload.Contents;
            }

            IEnumerable<Tuple<LinkedChild, BaseItem>> itemsRaw = playlist.GetManageableItems()
                .Where(i => i.Item2.IsVisible(userManager.GetUserById(userIdToUse.Value)));

            StringWriter stringWriter = new StringWriter();

            stringWriter.WriteLine(MediaBarPlugin.Instance.Configuration.AvatarsPlaylist);
                
            List<Guid> idsWritten = new List<Guid>();
                
            foreach (Tuple<LinkedChild, BaseItem> item in itemsRaw)
            {
                BaseItem itemToUse = item.Item2;
                if (item.Item2 is Episode episode)
                {
                    itemToUse = episode.Series;
                }

                if (!idsWritten.Contains(itemToUse.Id))
                {
                    idsWritten.Add(itemToUse.Id);
                }
            }

            idsWritten.Shuffle();

            foreach (Guid id in idsWritten)
            {
                // For some reason the JF api doesn't treat GUIDs correctly
                stringWriter.WriteLine(id.ToString().Replace("-", ""));
            }
            
            return stringWriter.ToString();
        }
        
        public static string IndexHtml(PatchRequestPayload payload)
        {
            if (MediaBarPlugin.Instance.Configuration.Enabled == MediaBarState.Disabled)
            {
                return payload.Contents ?? string.Empty;
            }
            
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{typeof(MediaBarPlugin).Namespace}.Inject.index.html")!;
            using TextReader reader = new StreamReader(stream);

            string importedHtml = reader
                .ReadToEnd()
                .Replace("{{AssetBaseUrl}}", ResolveAssetBaseUrl())
                .Replace("{{MediaBarConfig}}", BuildMediaBarConfigJson())
                .Replace("{{UserSettingsStyle}}", MediaBarPlugin.Instance.Configuration.WebConfig.HideUserSettingsButton
                    ? "<style>#slides-container .ss-settings-toggle { display: none !important; }</style>\n"
                    : string.Empty);

            // Regex.Replace treats "$" in the replacement as a group reference; the config
            // JSON can legitimately contain one, so substitute with a match evaluator.
            string regex = Regex.Replace(payload.Contents!, "</head>", _ => $"{importedHtml}</head>");

            return regex;
        }

        // Keys the frontend exposes in its per-user settings panel (slideshowpure.js SettingsPanel.fields)
        private static readonly string[] s_userSettingKeys =
        {
            "layout", "maxMovies", "maxSeries", "libraries", "shuffleInterval",
            "slideAnimationEnabled", "syncPageBackdrop", "pauseOnHover", "enableTrailers",
            "allowTrailersOnTouch", "trailerVolume", "trailerLibraries",
            "rememberOrderForSession", "respectDataSaver",
        };

        /// <summary>
        /// Serialises the admin WebConfig as the object slideshowpure.js reads from
        /// window.MediaBarConfig at startup. It is applied as a trusted source with -1 as
        /// the "unset" sentinel, and any key listed in "lock" ignores per-user overrides.
        /// </summary>
        private static string BuildMediaBarConfigJson()
        {
            WebConfig web = MediaBarPlugin.Instance.Configuration.WebConfig;

            Dictionary<string, object?> config = new Dictionary<string, object?>
            {
                ["shuffleInterval"] = web.ShuffleInterval,
                ["retryInterval"] = web.RetryInterval,
                ["minSwipeDistance"] = web.MinSwipeDistance,
                ["loadingCheckInterval"] = web.LoadingCheckInterval,
                ["maxPlotLength"] = web.MaxPlotLength,
                ["maxMovies"] = web.MaxMovies,
                ["maxSeries"] = web.MaxTvShows,
                ["maxItems"] = web.MaxItems,
                ["preloadCount"] = web.PreloadCount,
                ["fadeTransitionDuration"] = web.FadeTransitionDuration,
                ["trailerVolume"] = web.TrailerVolume,
                ["slideAnimationEnabled"] = web.SlideAnimationEnabled,
                ["syncPageBackdrop"] = web.SyncPageBackdrop,
                ["enableTrailers"] = web.EnableTrailers,
                ["pauseOnHover"] = web.PauseOnHover,
                ["allowTrailersOnTouch"] = web.AllowTrailersOnTouch,
                ["rememberOrderForSession"] = web.RememberOrderForSession,
                ["respectDataSaver"] = web.RespectDataSaver,
                ["ImageSvgs"] = web.ImageSvgs,
            };

            if (!string.IsNullOrWhiteSpace(web.Layout))
            {
                config["layout"] = web.Layout.Trim();
            }

            string[] libraries = SplitList(web.Libraries);
            if (libraries.Length > 0)
            {
                config["libraries"] = libraries;
            }

            string[] trailerLibraries = SplitList(web.TrailerLibraries);
            if (trailerLibraries.Length > 0)
            {
                config["trailerLibraries"] = trailerLibraries;
            }

            if (web.EnforceForAllUsers)
            {
                config["lock"] = s_userSettingKeys;
            }

            // Escape HTML so the JSON is safe to embed inside a <script> element
            return JsonConvert.SerializeObject(config, new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            });
        }

        private static string[] SplitList(string? value)
        {
            return (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string ResolveAssetBaseUrl()
        {
            PluginConfiguration config = MediaBarPlugin.Instance.Configuration;
            string version = config.VersionString.Trim();

            // The plugin's own version number is what the embedded assets are; treat it as
            // such rather than as a git ref, which may not exist as a tag (e.g. on a fork)
            string ownVersion = MediaBarPlugin.Instance.Version.ToString();

            if (PluginConfiguration.UsesEmbeddedAssets(version, config.AllowUnpinnedRefs) ||
                string.Equals(version, ownVersion, StringComparison.OrdinalIgnoreCase))
            {
                // Relative to /web/ so it resolves when Jellyfin is hosted under a base path
                return "../MediaBar";
            }

            if (string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase))
            {
                // "latest" is not a git ref; it has always meant the main branch
                version = "main";
            }

            return $"https://cdn.jsdelivr.net/gh/IAmParadox27/jellyfin-plugin-media-bar@{version}";
        }

        public static string HomeHtmlChunk(PatchRequestPayload payload)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{typeof(MediaBarPlugin).Namespace}.Inject.home-html.chunk.js")!;
            using TextReader reader = new StreamReader(stream);

            string regex = Regex.Replace(payload.Contents!, "(id=\"homeTab\" data-index=\"0\">)", $"$1{reader.ReadToEnd()}");
            
            return regex;
        }

        public static string MainBundle(PatchRequestPayload payload)
        {
            string replacementText =
                "window.PlaybackManager=this.playbackManager;console.log(\"PlaybackManager is now globally available:\",window.PlaybackManager);";
            
            string regex = Regex.Replace(payload.Contents!, @"(this\.playbackManager=e,)", $"$1{replacementText}");

            return regex;
        }
    }
}