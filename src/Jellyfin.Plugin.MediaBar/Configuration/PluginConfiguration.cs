using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaBar.Configuration
{
    public enum MediaBarState
    {
        Disabled,
        Enabled,
    }
    
    public class PluginConfiguration : BasePluginConfiguration
    {
        public const string EmbeddedVersion = "embedded";

        public MediaBarState Enabled { get; set; } = MediaBarState.Enabled;

        public string VersionString { get; set; } = EmbeddedVersion;

        public bool AllowUnpinnedRefs { get; set; } = false;

        public static bool UsesEmbeddedAssets(string? version, bool allowUnpinnedRefs)
        {
            version = version?.Trim();

            if (string.IsNullOrWhiteSpace(version) ||
                string.Equals(version, EmbeddedVersion, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // main/latest predate the embedded assets; configs that saved them meant
            // "newest", which is the embedded copy unless the admin opts back in
            return !allowUnpinnedRefs &&
                   (string.Equals(version, "main", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase));
        }

        public bool UseAvatarsFile { get; set; } = true;

        public string AvatarsPlaylist { get; set; } = string.Empty;
        
        public WebConfig WebConfig { get; set; } = new WebConfig();
    }

    public class WebConfig
    {
        public ImageSvgs ImageSvgs { get; set; } = new ImageSvgs();
        
        public int ShuffleInterval { get; set; } = -1;
        
        public int RetryInterval { get; set; } = -1;
        
        public int MinSwipeDistance { get; set; } = -1;
        
        public int LoadingCheckInterval { get; set; } = -1;
        
        public int MaxPlotLength { get; set; } = -1;

        public int MaxMovies { get; set; } = -1;
        
        public int MaxTvShows { get; set; } = -1;

        public int MaxItems { get; set; } = -1;

        public int PreloadCount { get; set; } = -1;
        
        public int FadeTransitionDuration { get; set; } = -1;

        public bool SlideAnimationEnabled { get; set; } = true;
        
        public bool SyncPageBackdrop { get; set; } = false;

        public bool EnableTrailers { get; set; } = true;

        /// <summary>
        /// "marquee", "plate" or "classic". Empty uses the frontend default.
        /// </summary>
        public string Layout { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated library display names the bar may draw from. Empty means all.
        /// </summary>
        public string Libraries { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated library display names allowed to autoplay trailers. Empty means wherever trailers are enabled.
        /// </summary>
        public string TrailerLibraries { get; set; } = string.Empty;

        public bool PauseOnHover { get; set; } = true;

        public bool AllowTrailersOnTouch { get; set; } = false;

        public int TrailerVolume { get; set; } = -1;

        public bool RememberOrderForSession { get; set; } = true;

        public bool RespectDataSaver { get; set; } = true;

        /// <summary>
        /// When true, every option exposed in the media bar's per-user settings panel is
        /// locked to the values above and users' local overrides are ignored.
        /// </summary>
        public bool EnforceForAllUsers { get; set; } = false;

        /// <summary>
        /// When true, the per-user settings (gear) button is not shown on the media bar.
        /// </summary>
        public bool HideUserSettingsButton { get; set; } = false;
    }

    public class ImageSvgs
    {
        public string? ImdbLogo { get; set; } = null;

        public string? TomatoLogo { get; set; } = null;

        public string? FreshTomato { get; set; } = null;
        
        public string? RottenTomato { get; set; } = null;
    }
}