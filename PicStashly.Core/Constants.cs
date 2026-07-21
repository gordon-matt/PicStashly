namespace PicStashly;

public static class Constants
{
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
}
