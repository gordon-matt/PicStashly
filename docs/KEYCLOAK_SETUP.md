# Keycloak Setup Guide for PicStashly

By default PicStashly protects itself with a single built-in ASP.NET Core Identity account
(created automatically on first run — see `SeedAdmin` in `appsettings.json`). This is enough
for most self-hosted setups. This guide is only needed if you want to delegate login to an
external Keycloak server instead — typically because you already run Keycloak for other
self-hosted apps and want PicStashly to share the same login.

## Overview

```
Browser → PicStashly → redirects to Keycloak login page
                     ← Keycloak issues a token after successful login
         PicStashly reads the token → user is authenticated
```

There is no per-user data in PicStashly (it's a single-user app) — Keycloak here is purely a
login gate, not a multi-tenant user system. Any user who successfully authenticates is treated
the same, **except** that the `Administrator` role additionally unlocks the Admin page and its
API endpoints.

## Prerequisites

- Keycloak is running and accessible (default: `http://localhost:8081`).
- If you are using the provided `docker-compose.yml`, uncomment the `keycloak` service and
  re-run `docker compose up -d`.

## Step 1 — Log in to the Keycloak Admin Console

1. Open `http://localhost:8081` in your browser.
2. Click **Administration Console**.
3. Log in with the bootstrap admin credentials (default: `admin` / `admin`).
   Change these in `docker-compose.yml` via `KC_BOOTSTRAP_ADMIN_PASSWORD` before
   deploying to a production environment.

## Step 2 — Create a Realm

1. In the top-left dropdown (showing **master**), click **Create realm**.
2. Set **Realm name** to `picstashly` (you may use any name; just keep it
   consistent with the `Authority` URL in your app configuration).
3. Leave **Enabled** toggled on.
4. Click **Create**.

## Step 3 — Create Application Roles

1. In the left sidebar, go to **Realm roles**.
2. Click **Create role**.
3. Set **Role name** to `Administrator` (exact case matters — it must match the
   value in `PicStashly.Constants.Roles`).
4. Click **Save**.
5. Repeat to create a second role named `User` (used for regular, non-admin login).

## Step 4 — Create a Client

1. In the left sidebar, go to **Clients**.
2. Click **Create client**.

### General settings

| Field         | Value        |
|---------------|--------------|
| Client type   | OpenID Connect |
| Client ID     | `picstashly` |

Click **Next**.

### Capability config

| Field                   | Value  |
|-------------------------|--------|
| Client authentication   | **On** |
| Authorization           | Off    |
| Standard flow           | **On** |
| Direct access grants    | Off    |

Click **Next**.

### Login settings

| Field                            | Value                                       |
|----------------------------------|----------------------------------------------|
| Root URL                        | `http://localhost:8080`                       |
| Home URL                        | `http://localhost:8080`                       |
| Valid redirect URIs             | `http://localhost:8080/signin-oidc`           |
| Valid post logout redirect URIs | `http://localhost:8080/signout-callback-oidc` |
| Web origins                     | `http://localhost:8080`                       |

> **Redirect URIs:** Use the exact values above. The login callback is
> `http://localhost:8080/signin-oidc`. The **post-logout** redirect must be
> `http://localhost:8080/signout-callback-oidc` — the OIDC middleware uses this
> path to receive the redirect from Keycloak and complete sign-out (clear cookies).
> If this URI is missing in Keycloak, you get "Invalid redirect uri" after clicking Logout.
>
> **Production / NAS (behind a reverse proxy):** use your public HTTPS URLs everywhere
> (Keycloak client settings and `Authority`), because Keycloak posts back to `/signin-oidc`
> as a cross-site request and browsers only send the OIDC correlation cookie on cross-site
> POSTs when it is `SameSite=None` + `Secure` — i.e. the app must be reached over HTTPS.
>
> **Separate Docker stacks (app and Keycloak on the same host):** the app makes a
> server-to-server (backchannel) token exchange call to Keycloak's token endpoint. If the
> app and Keycloak containers cannot resolve each other's public hostname (DNS/hairpin NAT),
> set `Authentication:Keycloak:BackchannelBaseUrl` to the internal Keycloak URL (e.g.
> `http://keycloak:8080`) so the token exchange goes directly container-to-container while
> `Authority` stays public for browser-facing login redirects. Requires both containers on
> the same Docker network.

Click **Save**.

## Step 5 — Copy the Client Secret

1. Stay on the client page and click the **Credentials** tab.
2. Copy the value under **Client secret**.
3. Add it to your configuration (see [Step 7](#step-7--configure-picstashly)).

> **Security:** Never commit the secret to source control. Use an environment variable
> or [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
> in development.

## Step 6 — Configure the Roles Claim Mapper

The app expects a flat `roles` claim in the token (e.g. `roles: ["Administrator"]`).

1. Go to **Clients → picstashly** (click the client name).
2. Open the **Client scopes** tab. Click on the **picstashly-dedicated** scope.
3. Open the **Mappers** tab.
4. Click **Create** (or **Add mapper → By configuration**).
5. Choose **User Realm Role**.
6. Set these values **exactly**:

| Field               | Value         |
|---------------------|---------------|
| Name                | `realm-roles` |
| **Multivalued**     | **On**        |
| Token Claim Name    | `roles`       |
| **Claim JSON Type** | **`String`** — do **not** use `JSON` or Keycloak will throw "cannot map type for token claim". |
| Add to ID token     | **On**        |
| Add to access token | On            |
| Add to userinfo     | On            |

7. Click **Save**.

## Step 7 — Create a User and Assign the Administrator Role

1. In the left sidebar, go to **Users → Create new user**.
2. Fill in **Username** and **Email**, toggle **Email verified** on, click **Create**.
3. On the user page, click **Credentials → Set password** (toggle **Temporary** off).
4. Click **Role mapping → Assign role**, select `Administrator` (or `User` for a
   non-admin login that can browse/upload but not see the Admin page), click **Assign**.

## Step 8 — Configure PicStashly

### Option A — `appsettings.json` (development / local)

Uncomment and fill in the `Authentication` section in `PicStashly/appsettings.json`:

```json
"Authentication": {
  "Provider": "Keycloak",
  "Keycloak": {
    "Authority": "http://localhost:8081/realms/picstashly",
    "ClientId": "picstashly",
    "ClientSecret": "paste-your-client-secret-here"
  }
}
```

> Store the client secret in User Secrets rather than directly in `appsettings.json`:
>
> ```bash
> dotnet user-secrets set "Authentication:Keycloak:ClientSecret" "your-secret"
> ```

### Option B — Environment variables (Docker / production)

In `docker-compose.yml`, uncomment the Keycloak lines under the `app` service:

```yaml
Authentication__Provider: Keycloak
Authentication__Keycloak__Authority: http://host.docker.internal:8081/realms/picstashly
Authentication__Keycloak__ClientId: picstashly
Authentication__Keycloak__ClientSecret: "${KEYCLOAK_CLIENT_SECRET}"
```

Set `KEYCLOAK_CLIENT_SECRET` in a `.env` file next to `docker-compose.yml` (never commit it):

```
KEYCLOAK_CLIENT_SECRET=paste-your-client-secret-here
```

> **Authority URL (local dev):** use `http://host.docker.internal:8081/realms/picstashly` so
> both the app container and the browser can reach Keycloak.
>
> **Authority URL (NAS / production — separate stacks):** use the public HTTPS URL and set
> `BackchannelBaseUrl` to the internal Keycloak URL, as described in the Step 4 note above.

## Step 9 — First Login

1. Start (or restart) the application.
2. Navigate to `http://localhost:8080`.
3. Click **Login** in the navbar — you will be redirected to Keycloak.
4. Log in with the Keycloak user you created.
5. You are redirected back to the app, now authenticated.

## Switching Back to Built-in Identity

1. Remove (or comment out) the `Authentication` section from `appsettings.json` / environment
   variables.
2. Restart the application.

The built-in Identity system is completely independent of Keycloak; its local admin account is
unaffected by any Keycloak configuration and still works if you switch back.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| **Invalid redirect uri** (after clicking Logout) | Post-logout redirect URI not allowed in Keycloak | Set **Valid post logout redirect URIs** to `http://localhost:8080/signout-callback-oidc` (exact). |
| **Invalid parameter: redirect_uri** (at Keycloak login) | App sent `http://` but Keycloak has `https://` registered (or host mismatch) | Ensure Keycloak client has the exact redirect URI the app sends; set `PublicOrigin` if behind a reverse proxy. |
| `unknown_error` / "cannot map type for token claim" | Roles mapper has **Claim JSON Type** set to `JSON` | Recreate the mapper with Claim JSON Type = **String** (Step 6). |
| "We can't connect to the server at keycloak" (browser) | `Authority` used `keycloak:8080`, which only resolves inside Docker | Set `Authority` to `http://host.docker.internal:8081/realms/...` for local dev. |
| 401 after successful Keycloak login on the Admin page | User has no `Administrator` role | Assign the `Administrator` role in Keycloak (**Users → &lt;user&gt; → Role mapping**). |
| `IDX20803: Unable to obtain configuration` | App cannot reach Keycloak | Check the `Authority` URL is reachable from where the app runs. |
| **502 Bad Gateway** when redirected to `/signin-oidc` | App container cannot reach Keycloak for the token exchange | Set `BackchannelBaseUrl: http://keycloak:8080` with both containers on a shared Docker network. |
