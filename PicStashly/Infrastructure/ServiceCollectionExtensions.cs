using PicStashly.Data.MySql;
using PicStashly.Data.Npgsql;
using PicStashly.Data.Sql;
using PicStashly.Data.Sqlite;

namespace PicStashly.Infrastructure;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Wires up the database provider selected by <c>Database:Provider</c> in configuration.
        /// Defaults to <see cref="Constants.DatabaseProviders.Sqlite"/> for zero-configuration
        /// self-hosting. The matching <c>PicStashly.Data.{Provider}</c> project registers the
        /// concrete <c>ApplicationDbContext</c>, the <see cref="IDbContextFactory"/> and the
        /// abstract <c>ApplicationDbContextBase</c>.
        /// </summary>
        public IServiceCollection AddPicStashlyDatabase(IConfiguration configuration)
        {
            string provider = configuration["Database:Provider"] ?? Constants.DatabaseProviders.Sqlite;

            return provider switch
            {
                Constants.DatabaseProviders.Sqlite => services.AddPicStashlySqlite(configuration),
                Constants.DatabaseProviders.Npgsql => services.AddPicStashlyNpgsql(configuration),
                Constants.DatabaseProviders.SqlServer => services.AddPicStashlySqlServer(configuration),
                Constants.DatabaseProviders.MySql => services.AddPicStashlyMySql(configuration),
                _ => throw new InvalidOperationException(
                    $"Unknown Database:Provider '{provider}'. Valid values: Sqlite, Npgsql, SqlServer, MySql.")
            };
        }
    }
}
