# PicStashly

A self-hosted web app for storing, tagging, and finding random images — memes, reaction images,
screenshots — optimized for fast capture and search rather than chronological photo organization.
Think of it as a personal visual search engine, not a photo manager.

## Features

- **Fast capture**: multi-file drag-and-drop (on any page), paste-from-clipboard, a browser
  bookmarklet ("Stash it!" on the Admin page), and a PWA share target so mobile "Share" sends
  images straight into the app.
- **Tags, not albums**: flat multi-tag model with autocomplete, bulk-tag mode, and a tag
  management page (rename / merge / delete with usage counts).
- **Search-first library**: masonry grid with infinite scroll, combined search across filename,
  tags and OCR text, tag filters, "untagged only" toggle, and random shuffle.
- **Dedup on ingest**: a 64-bit perceptual hash (dHash) is computed for every image; near-duplicates
  are flagged on upload with a "view existing / save anyway / merge tags" prompt.
- **OCR**: meme captions are extracted with [tesseract](https://github.com/tesseract-ocr/tesseract)
  (when installed) and become searchable. Text is editable/correctable per image.
- **Automatic downscaling**: images larger than the configured maximum are resized on upload
  (aspect ratio preserved, original format kept); WebP thumbnails are generated for the grid.
- **Lightbox** with keyboard navigation: `←`/`→` prev/next, `t` focus tags, `Esc` close, `Del` delete.

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
| `Ocr` | `TesseractPath` | `tesseract` | Path to the tesseract CLI |
| `Ocr` | `Languages` | `eng` | Tesseract language(s), e.g. `eng+deu` |

OCR requires the tesseract CLI (`winget install tesseract-ocr.tesseract` on Windows,
`apt install tesseract-ocr` on Debian/Ubuntu). When it's not installed, OCR is skipped
gracefully and everything else works normally. The Docker image includes it.

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
PicStashly.Core/         Constants
PicStashly.Data/         Entities + ApplicationDbContextBase (provider-agnostic)
PicStashly.Data.Npgsql/  PostgreSQL DbContext, factory + migrations
PicStashly.Data.MySql/   MySQL DbContext, factory + migrations
PicStashly.Data.Sqlite/  SQLite DbContext, factory + migrations
PicStashly.Data.Sql/     SQL Server DbContext, factory + migrations
PicStashly.Models/       Request/response DTOs
PicStashly.Services/     Ingest pipeline, search, tags, OCR, pHash, storage
```
