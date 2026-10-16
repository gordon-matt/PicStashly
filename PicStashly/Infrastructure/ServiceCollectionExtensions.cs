using System.Net;
using Heroicsoft.NetForge.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using PicStashly.Data;
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

        /// <summary>
        /// Registers either ASP.NET Core Identity (local user database, default) or Keycloak
        /// (external OpenID Connect provider). <paramref name="useKeycloak"/> comes from Program.cs.
        /// PicStashly is a single-user self-hosted app — this exists mainly to gate the whole
        /// app behind a login screen so it isn't exposed unauthenticated when self-hosted.
        /// </summary>
        public void AddPicStashlyAuthentication(IConfiguration configuration, bool useKeycloak)
        {
            services.AddNetForgeAuthentication<ApplicationUser, ApplicationRole, ApplicationDbContextBase>(
                configuration,
                options =>
                {
                    if (useKeycloak)
                    {
                        options.Provider = AuthProviderNames.Keycloak;
                    }

                    options.IncludeAuthController = false;
                    options.RegisterUserContext = false;
                    options.Identity.UseRelaxedPasswordPolicy = false;
                    options.AccessDeniedPath = "/access-denied";
                    options.RemoteFailurePath = "/error";
                    options.Keycloak.RequireHttpsMetadata = false;
                    options.Keycloak.CorrelationCookieSameSite = SameSiteMode.None;
                    options.Keycloak.CorrelationCookieSecurePolicy = CookieSecurePolicy.Always;
                    options.Keycloak.RedirectBrowserToPublicAuthority = true;
                });

            // When behind a reverse proxy (e.g. a NAS), trust forwarded headers so the app
            // sees the original HTTPS scheme and host. Required for OIDC redirect URIs and Secure cookies.
            if (useKeycloak)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                        | ForwardedHeaders.XForwardedProto
                        | ForwardedHeaders.XForwardedHost;
                    options.KnownProxies.Clear();
                    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
                    options.KnownProxies.Add(IPAddress.Parse("::1"));
                    // Docker bridge: when the app runs in a container, the proxy connects from the host.
                    options.KnownProxies.Add(IPAddress.Parse("172.17.0.1"));
                });
            }
        }
    }
}