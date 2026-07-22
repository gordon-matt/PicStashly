namespace PicStashly;

public static class Constants
{
    /// <summary>Browser <c>localStorage</c> key for the per-user Bootswatch theme (client-only).</summary>
    public const string ClientThemeLocalStorageKey = "picstashly.theme";

    /// <summary>
    /// Supported database providers, selected via the <c>Database:Provider</c> configuration key.
    /// Each value maps to a <c>PicStashly.Data.{Provider}</c> project. Defaults to
    /// <see cref="DatabaseProviders.Sqlite"/> for zero-configuration self-hosting.
    /// </summary>
    public static class DatabaseProviders
    {
        public const string MySql = "MySql";
        public const string Npgsql = "Npgsql";
        public const string Sqlite = "Sqlite";
        public const string SqlServer = "SqlServer";
    }

    public static class FileStorage
    {
        public const string Images = "images";
        public const string Thumbnails = "thumbnails";
    }

    /// <summary>Request path prefix under which stored files are served via static files middleware.</summary>
    public const string MediaRequestPath = "/media";

    public static class Roles
    {
        public const string Administrator = "Administrator";
        public const string User = "User";
    }

    /// <summary>
    /// Bootswatch themes shipped via LibMan to <c>wwwroot/lib/bootswatch/dist/&lt;theme&gt;/bootstrap.min.css</c>.
    /// Keep this list in lock-step with <c>libman.json</c> and <c>wwwroot/js/theme-init.js</c>.
    /// </summary>
    public static class BootswatchThemes
    {
        public const string Default = "darkly";

        public static readonly IReadOnlyList<string> All =
        [
            "brite", "cerulean", "cosmo", "cyborg", "darkly", "flatly", "journal", "litera", "lumen",
            "lux", "materia", "minty", "morph", "pulse", "quartz", "sandstone", "simplex", "sketchy",
            "slate", "solar", "spacelab", "superhero", "united", "vapor", "yeti", "zephyr"
        ];

        /// <summary>
        /// Themes with a dark chrome by default. Bootswatch keys off <c>data-bs-theme</c> for
        /// <c>[data-bs-theme=dark]</c> variable overrides (and logo / Tagify contrast rules).
        /// </summary>
        public static readonly IReadOnlySet<string> DarkColorScheme =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cyborg", "darkly", "slate", "solar", "superhero", "vapor"
            };

        public static bool IsDarkColorScheme(string? theme) =>
            !string.IsNullOrEmpty(theme) && DarkColorScheme.Contains(theme);

        public static IEnumerable<string> DarkThemes =>
            All.Where(t => DarkColorScheme.Contains(t));

        public static IEnumerable<string> LightThemes =>
            All.Where(t => !DarkColorScheme.Contains(t));
    }
}
