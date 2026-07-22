using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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
        /// (external OpenID Connect provider), selected via <c>Authentication:Provider</c>.
        /// PicStashly is a single-user self-hosted app — this exists mainly to gate the whole
        /// app behind a login screen so it isn't exposed unauthenticated when self-hosted.
        /// </summary>
        public void AddPicStashlyAuthentication(IConfiguration configuration, bool useKeycloak)
        {
            if (useKeycloak)
            {
                string? keycloakAuthority = configuration["Authentication:Keycloak:Authority"];
                string? keycloakBackchannelBaseUrl = configuration["Authentication:Keycloak:BackchannelBaseUrl"];

                services.AddSingleton<IAuthProviderService>(new AuthProviderService(AuthProvider.Keycloak, keycloakAuthority));

                // ── Keycloak / OpenID Connect authentication ──────────────────────────────
                // A cookie keeps the server-side session; OIDC handles the external login flow.
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/auth/login";
                    options.AccessDeniedPath = "/Home/AccessDenied";
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    // Authority: the public Keycloak realm URL the browser uses to reach Keycloak.
                    // The app also uses this for backchannel calls (discovery + token exchange) unless
                    // BackchannelBaseUrl is set, in which case server-to-server calls are routed there.
                    options.Authority = keycloakAuthority;

                    // When BackchannelBaseUrl is set, all backchannel HTTP calls (discovery and the token
                    // exchange) are transparently rerouted to that URL. This avoids hairpin NAT / DNS
                    // issues in Docker where the container cannot reach the public Keycloak hostname.
                    // Example: when app and Keycloak share a Docker network, set BackchannelBaseUrl to
                    // "http://keycloak:8080" so the token exchange goes directly container-to-container.
                    if (!string.IsNullOrWhiteSpace(keycloakBackchannelBaseUrl) && !string.IsNullOrWhiteSpace(keycloakAuthority))
                    {
                        options.BackchannelHttpHandler = new KeycloakBackchannelHandler(keycloakAuthority, keycloakBackchannelBaseUrl);
                    }

                    options.ClientId = configuration["Authentication:Keycloak:ClientId"];
                    options.ClientSecret = configuration["Authentication:Keycloak:ClientSecret"];
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;

                    // Map the OIDC "preferred_username" claim to the standard Name claim.
                    // The .NET JWT handler's default inbound claim type map already converts
                    // the "roles" JWT claim → ClaimTypes.Role automatically, so no RoleClaimType
                    // override is needed. [Authorize(Roles = "...")] and IsInRole() both check
                    // ClaimTypes.Role, which is what the handler produces.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "preferred_username"
                    };

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");

                    // Required when Keycloak is accessed over HTTP (e.g. local dev or internal Docker).
                    // Remove this once Keycloak is always reached via HTTPS from the app container.
                    options.RequireHttpsMetadata = false;

                    // Keycloak redirects the browser back to /signin-oidc with a POST (cross-site).
                    // Browsers do not send SameSite=Lax cookies on cross-site POSTs, so the OIDC
                    // correlation cookie must be SameSite=None + Secure for the callback to work.
                    // The app must be reached over HTTPS (reverse proxy that sets X-Forwarded-Proto).
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.NonceCookie.SameSite = SameSiteMode.None;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

                    // Keycloak 26+ enables Pushed Authorization Requests (PAR) by default.
                    // PAR requires exact redirect URI matches (wildcards are rejected), which
                    // conflicts with the wildcard URIs typically used during development.
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                    // When using BackchannelBaseUrl, discovery is fetched from the internal URL so
                    // Configuration.AuthorizationEndpoint has the internal host (e.g. keycloak:8080).
                    // We must send the browser to the public Authority URL instead.
                    string? publicOrigin = configuration["Authentication:Keycloak:PublicOrigin"];
                    bool useBackchannel = !string.IsNullOrWhiteSpace(keycloakBackchannelBaseUrl);
                    if (!string.IsNullOrWhiteSpace(publicOrigin) || useBackchannel)
                    {
                        publicOrigin = publicOrigin?.TrimEnd('/');
                        options.Events.OnRedirectToIdentityProvider = context =>
                        {
                            if (!string.IsNullOrWhiteSpace(publicOrigin))
                            {
                                context.ProtocolMessage.RedirectUri = publicOrigin + "/signin-oidc";
                            }

                            if (useBackchannel && !string.IsNullOrEmpty(keycloakAuthority))
                            {
                                // Build auth URL from public Authority so the browser goes to
                                // https://keycloak.example.com/... (no internal host/port).
                                string fullUrl = context.ProtocolMessage.CreateAuthenticationRequestUrl();
                                int queryIndex = fullUrl.IndexOf('?');
                                string query = queryIndex >= 0 ? fullUrl[queryIndex..] : string.Empty;
                                string authUrl = keycloakAuthority.TrimEnd('/') + "/protocol/openid-connect/auth" + query;
                                context.Response.Redirect(authUrl);
                                context.HandleResponse();
                            }
                            return Task.CompletedTask;
                        };

                        // Sign-out: post_logout_redirect_uri must match Keycloak's "Valid post logout redirect URIs"
                        // (e.g. https://picstashly.example.com/signout-callback-oidc). When behind a proxy the app may see
                        // HTTP, so set it explicitly from PublicOrigin.
                        if (!string.IsNullOrWhiteSpace(publicOrigin))
                        {
                            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                            {
                                context.ProtocolMessage.PostLogoutRedirectUri = publicOrigin + "/signout-callback-oidc";
                                return Task.CompletedTask;
                            };
                        }
                    }

                    // Redirect auth failures to the error page rather than re-throwing. Without this
                    // a failed callback redirects to /Home/Error which (if protected) re-challenges,
                    // creating an infinite redirect loop.
                    options.Events.OnRemoteFailure = context =>
                    {
                        context.Response.Redirect("/Home/Error");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });

                // When behind a reverse proxy (e.g. a NAS), trust forwarded headers so the app
                // sees the original HTTPS scheme and host. Required for OIDC redirect URIs and Secure cookies.
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
            else
            {
                services.AddSingleton<IAuthProviderService>(new AuthProviderService(AuthProvider.Identity, null));

                // ── ASP.NET Core Identity (default) ──────────────────────────────────────
                services.AddIdentity<ApplicationUser, ApplicationRole>()
                    .AddEntityFrameworkStores<ApplicationDbContextBase>()
                    .AddDefaultTokenProviders()
                    .AddDefaultUI();
            }
        }
    }
}