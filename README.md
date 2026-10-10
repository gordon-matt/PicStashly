# PicStashly

A self-hosted web app for storing, tagging, and finding random images — memes, reaction images,
screenshots — optimized for fast capture and search rather than chronological photo organization.
Think of it as a personal visual search engine, not a photo manager.

## Screenshots

### Library (Home Page):

<img src="https://github.com/gordon-matt/PicStashly/blob/master/_Misc/Screenshots/Home.png" alt="Home" />

### Admin:

<img src="https://github.com/gordon-matt/PicStashly/blob/master/_Misc/Screenshots/Admin.png" alt="Home" />

### Capturing an image with the bookmarklet:

<img src="https://github.com/gordon-matt/PicStashly/blob/master/_Misc/Screenshots/Bookmarklet Capture.png" alt="Home" />

You drag the bookmarklet from the admin page to your bookmarks toolbar first. Then when you hit that bookmarklet on any page, you can capture an image. The image under the cursor will be highlighted as well. In the screenshot above, you can see the Rambo meme is highlighted because my cursor was over it.

## Features

- **Multi-user**: built for a family (or household) to share one instance while keeping libraries
  private — every category, tag and image belongs to exactly one account, and no user can see or
  browse another's images.
- **Fast capture**: multi-file drag-and-drop (on any page), paste-from-clipboard, a browser
  bookmarklet ("Stash it!" on the Admin page), and a PWA share target so mobile "Share" sends
  images straight into the app.
- **Tags, not albums**: flat multi-tag model with autocomplete, bulk-tag mode, and a tag
  management page (rename / merge / delete with usage counts). Each user has their own tags.
- **Categories**: an optional single category per image (e.g. "Memes", "Reaction Images") with its
  own management page (create / rename / delete) and its own set per user. Images are stored on
  disk under a matching `images/{user-id}/{category-slug}/...` subfolder, so the folder layout
  stays human-readable and each user's files are grouped together.
- **Search-first library**: masonry grid with infinite scroll, combined search across filename,
  tags and OCR text, tag and category filters, "untagged only" toggle, and random shuffle — always
  scoped to your own images.
- **Dedup on ingest**: a 64-bit perceptual hash (dHash) is computed for every image; near-duplicates
  within your own library are flagged on upload with a "view existing / save anyway / merge tags" prompt.
- **OCR**: meme captions are extracted with [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR)
  (PP-OCRv5, in-process via [Sdcb.PaddleOCR](https://github.com/sdcb/PaddleSharp)) and become
  searchable. Models are bundled — nothing to install. Text is editable/correctable per image.
- **Automatic downscaling**: images larger than the configured maximum are resized on upload
  (aspect ratio preserved, original format kept); WebP thumbnails are generated for the grid.
- **Lightbox** with keyboard navigation: `←`/`→` prev/next, `t` focus tags, `Esc` close, `Del` delete.
- **Quick actions on the grid**: hover an image tile to edit its tags or delete it without opening
  the lightbox; the lightbox and image details page also support delete and category reassignment.
- **Authentication**: the whole app sits behind a login screen — built-in ASP.NET Core Identity
  by default (a local admin account is created on first run, and family members can register their
  own accounts subject to admin approval), or Keycloak for centralised single sign-on if you
  already run it for other self-hosted apps.

## Tech stack

- .NET 10 / ASP.NET Core MVC, Knockout.js + Bootstrap 5 (Bootswatch Darkly) frontend
- EF Core with [Extenso](https://github.com/gordon-matt/Extenso) repositories
- SixLabors.ImageSharp for resizing, thumbnails, and perceptual hashing
- Serilog logging

## Database support

PicStashly can run on **SQLite** (default, zero configuration), **PostgreSQL**, **MySQL**, or
**SQL Server**. Select the provider and connection string in `appsettings.json` (or via
environment variables in Docker):

```json
{
  "Database": {
    "Provider": "Sqlite" // Sqlite | Npgsql | SqlServer | MySql
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=picstashly.db"
  }
}
```

Migrations for each provider live in the matching `PicStashly.Data.{Provider}` project and are
applied automatically on startup.

## Authentication and multi-user

PicStashly is designed for a family or household to share one instance: since it's typically
exposed on your home network (or further), the whole app sits behind a login screen, and every
account gets its own private library — categories, tags and images belong to exactly one user
and are never visible to anyone else (the `Administrator` role can approve/manage accounts on the
**Admin** page, but does not browse other users' images).

By default it uses **built-in ASP.NET Core Identity**: a local admin account is created
automatically on first run (`admin@picstashly.local` / `Admin@123` — override via `SeedAdmin`
in `appsettings.json`, User Secrets, or `SeedAdmin__Email` / `SeedAdmin__Password` env vars).
Change the password after first login via the **Manage account** link in the navbar. Family
members can self-register from the login screen, but new accounts start **locked** — an
administrator must approve them from the **Admin** page (**Family members** card) before they
can sign in; the same page lets an admin promote/demote the `Administrator` role.

If you already run **Keycloak** for other self-hosted apps, PicStashly can delegate login to
it instead — set `Authentication:Provider` to `Keycloak` (see the commented block in
`appsettings.json` or `docker-compose.yml`). See [`docs/KEYCLOAK_SETUP.md`](docs/KEYCLOAK_SETUP.md)
for full setup instructions. With Keycloak, accounts (and the `Administrator` realm role) are
created and approved in Keycloak itself — the in-app user-approval workflow only applies to the
built-in Identity provider. Each Keycloak user still gets their own private PicStashly library,
keyed off their Keycloak account id.

> **Upgrading an existing single-user install:** this version adds a required owner column to
> categories, images and tags, and nests stored files one level deeper on disk
> (`images/{user-id}/{category-slug}/...` instead of `images/{category-slug}/...`). Existing
> rows are migrated with an empty owner id, which means they won't show up for any real account
> until you assign them (via SQL) to a specific user's id and move their files into that user's
> folder on disk. For a fresh/test instance, it's simplest to delete the database and `uploads`
> folder and start over.

## Configuration

| Section | Key | Default | Purpose |
|---|---|---|---|
| `FileStorage` | `BasePath` | `<app>/uploads` | Root folder for stored images/thumbnails |
| `ImageProcessing` | `AutoShrinkImages` | `true` | Downscale oversized uploads |
| `ImageProcessing` | `MaxWidth` / `MaxHeight` | `1920` | Maximum stored dimensions |
| `ImageProcessing` | `Quality` | `85` | Encoder quality when re-encoding |
| `ImageProcessing` | `ThumbnailWidth` / `ThumbnailHeight` | `400` | Thumbnail bounds |
| `Upload` | `AllowedExtensions` | jpg, jpeg, png, gif, webp, bmp | Accepted file types |
| `Upload` | `MaxFileSizeBytes` | 50 MB | Per-file upload limit |
| `Upload` | `DuplicateHammingThreshold` | `5` | pHash distance considered a near-duplicate (0–64) |
| `Ocr` | `Enabled` | `true` | Extract text from images on ingest |
| `Ocr` | `Device` | `Mkldnn` | Inference backend: `Mkldnn` or `Openblas` (see note) |
| `Ocr` | `DetectionMaxSize` | `960` | Long-side cap (px) for text detection; `0` disables it |
| `Ocr` | `AllowRotateDetection` | `false` | Detect angled text (keep off for upright captions) |
| `Ocr` | `Enable180Classification` | `false` | Detect text rotated 180° |

OCR runs in-process via PaddleOCR with bundled PP-OCRv5 models — no CLI or model download required.
Its native libraries need the Visual C++ Redistributable on Windows, or `libgomp1` on Linux
(included in the Docker image). If the native stack can't load, OCR is skipped gracefully and
everything else works normally. On old CPUs without AVX, set `Ocr:Device` to `Openblas`.

> The `DetectionMaxSize` cap (960 px long side) is what keeps memory bounded — it's the same
> mitigation as PaddleOCR issue #17955, applied here by default, so the 3.x CPU memory blow-up
> doesn't occur. An `Onnx` backend also exists but can't convert the bundled PP-OCRv5 detection
> model (Paddle2ONNX limitation), so it's not used.

## Running

### Locally

```bash
cd PicStashly
dotnet run
```

The database is created/migrated automatically on first run.

### Docker

```bash
docker compose up -d
```

Data (SQLite database + images) is stored in the `picstashly-data` volume. See
`docker-compose.yml` for the optional PostgreSQL setup.

## Capture endpoints

- `POST /api/capture` — bookmarklet endpoint; accepts `{ imageUrl, pageUrl, tagNames }`, downloads
  the image server-side and runs it through the ingest pipeline.
- `POST /upload/share` — PWA Web Share Target (multipart, field name `images`).

## Solution layout

```
PicStashly/              ASP.NET Core MVC web app (controllers, views, wwwroot)
PicStashly.Core/         Constants (incl. Roles)
PicStashly.Data/         Entities (incl. ApplicationUser/Role) + ApplicationDbContextBase (provider-agnostic)
PicStashly.Data.Npgsql/  PostgreSQL DbContext, factory + migrations
PicStashly.Data.MySql/   MySQL DbContext, factory + migrations
PicStashly.Data.Sqlite/  SQLite DbContext, factory + migrations
PicStashly.Data.Sql/     SQL Server DbContext, factory + migrations
PicStashly.Models/       Request/response DTOs
PicStashly.Services/     Ingest pipeline, search, tags, OCR, pHash, storage
```

---

## Did I use AI for this?

Yes—shamelessly. Without AI, this project would not exist. Life is busy: work, family, and everything in between. I am a senior developer and have been coding since the mid-2000s—I know what good code looks like and I know what sloppy code looks like. So yes: I used AI as a **productivity tool**, but the **architecture and decisions are mine**, and I am the one maintaining it.

## Support

If you find this project useful, consider supporting its development.

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/BuyMeACoffee_Logo.png"
         alt="Buy Me a Coffee"
         height="40">
  </a>&nbsp;&nbsp;&nbsp;
  <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=gordon_matt%40live%2ecom&lc=AU&currency_code=AUD&bn=PP%2dDonationsBF%3abtn_donateCC_LG%2egif%3aNonHosted">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/PayPal_Logo.png"
         alt="Donate with PayPal"
         height="40">
  </a>
</p>

<p align="center">
  Prefer using your phone? Scan the QR code:
</p>

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/BuyMeACoffee_QR.png"
         alt="Buy Me a Coffee QR Code"
         width="180">
  </a>
</p>

<img src="https://komarev.com/ghpvc/?username=gordon-matt&label=GitHub%20Hits%20Since%202025-06-01%3A%20&color=ff0000&style=flat" alt="gordon-matt" />
