# MemeLibrary — Self-Hosted Image Dump App

## Overview
A self-hosted web app for storing, tagging, and finding random images (memes, reaction images, screenshots, etc.) — optimized for fast capture and search, not chronological photo organization. This is explicitly *not* a photo-management app (Piwigo/PhotoPrism); it's closer to a personal visual search engine.

**Target stack:** .NET 10, ASP.NET Core MVC. Data access / service layer patterns to follow existing MVC apps (Cursor will be pointed to those separately — not detailed here).

---

## Core Design Principles

1. **Capture friction is the #1 risk.** If saving an image takes more than a couple of taps/clicks, the app won't get used. Ingestion UX is priority one.
2. **Tags, not albums.** Flat, multi-tag model. No forced hierarchy or folders.
3. **Search-first browsing.** The main screen is a searchable/filterable grid, not a chronological feed.
4. **Dedup on ingest.** Avoid silently accumulating five copies of the same reaction image.
5. **OCR the images.** Captions baked into memes are often the only meaningful text — index it.

---

## Feature Set (v1)

### Ingestion
- Manual upload (multi-file, drag-and-drop onto the grid page)
- Paste-from-clipboard support (paste an image directly into the browser page)
- Browser bookmarklet: right-click-save-style capture from any webpage → posts to an API endpoint
- PWA manifest + Web Share Target API so mobile "Share" from any app sends an image straight to the app
- On ingest: generate thumbnail, compute perceptual hash (pHash), run OCR, extract basic EXIF/metadata (if present), and prompt for tags (or auto-tag suggestions if you build that later)

### Duplicate Detection
- Compare incoming pHash against existing library on upload
- If a close match is found, show a small comparison modal: "You already have something similar — view existing / save anyway / merge tags"

### Library / Browse
- Masonry-style grid, infinite scroll (no pagination clicks)
- Quick-preview modal/lightbox on click (no full page nav) with next/prev keyboard navigation
- Filter bar: tag multi-select, text search (matches filename, tags, and OCR text), date-added range, "untagged only" toggle
- Sort: date added, random shuffle (useful for a meme dump), most-used tags

### Tagging
- Free-text tag input with autocomplete against existing tags
- Bulk-tag mode: select multiple images (checkbox overlay on grid) → apply/remove tags in one action
- Tag management page: rename, merge, delete tags; see usage counts

### Detail / Single Image View
- Full image, tag list (editable inline), OCR text (editable/correctable), source URL if captured via bookmarklet, date added, dedup hash info
- Delete / replace / download actions

### Search
- Combined query across: tags, OCR text, filename
- Consider simple ranking: tag match > OCR match > filename match

### Admin / Housekeeping
- Storage stats (image count, total size)
- Orphan/broken file check
- Re-run OCR / re-hash tools (for when you tweak the pipeline later)

---

## Explicitly Out of Scope for v1
- Albums/collections/folders
- Multi-user sharing, permissions, comments
- Mobile native app (PWA covers this)
- Auto-tagging via ML/vision models (nice future add-on, not v1)

---

## UI / Page Map

| Page | Purpose |
|---|---|
| `/` (Library) | Main grid, search/filter bar, infinite scroll |
| `/upload` | Drag-and-drop + paste upload, shows ingest queue with dedup prompts |
| `/image/{id}` | Detail view/lightbox |
| `/tags` | Tag management (rename/merge/delete, usage counts) |
| `/admin` | Storage stats, maintenance tools |
| Bookmarklet endpoint | `POST /api/capture` — accepts image URL + page URL, no UI |
| Share target | PWA share intent → posts to same ingest pipeline as `/upload` |

### Grid/Library Page Behavior
- Sticky filter/search bar at top
- Grid below, virtualized/infinite-scroll (avoid loading full library DOM at once — this matters more than it sounds like once you're past a few thousand images)
- Checkbox overlay mode toggle for bulk actions
- Empty/no-results state should suggest removing filters, not just show blank

### Upload Page Behavior
- Drop zone + "paste anywhere on this page" support
- Each file shown as a card with: thumbnail preview, upload progress, dedup warning (if triggered), tag input
- Batch tag field: apply one set of tags to everything in the current upload batch, then override per-image as needed

### Lightbox/Detail Behavior
- Keyboard: arrow keys next/prev, `t` to focus tag input, `Esc` to close, `Delete` to delete (with confirm)
- Tag editing inline, no separate edit page needed for the common case

---

## Data Model (conceptual — not prescribing exact EF Core setup)

- **Image**: Id, FileName, StoragePath, ContentType, FileSizeBytes, Width, Height, PerceptualHash, OcrText, SourceUrl (nullable), DateAdded
- **Tag**: Id, Name (unique, normalized lowercase for matching)
- **ImageTag**: ImageId, TagId (many-to-many join)
- Consider a **DuplicateGroup** or just a self-referencing "PossibleDuplicateOfImageId" if you want to track resolved dedup decisions rather than just checking on ingest

---

## Ingestion Pipeline (conceptual flow)

1. Receive file (upload form, paste, bookmarklet POST, or share target)
2. Validate type/size
3. Compute perceptual hash → compare against existing library
4. If near-duplicate found → return dedup prompt to client instead of auto-saving
5. Generate thumbnail(s)
6. Run OCR → store extracted text
7. Persist file to storage + DB record
8. Return to client for tag entry (or apply batch tags if provided)

---

## Open Questions to Resolve Before/During Build
- Storage: flat folder on disk vs. hashed subfolder structure (e.g. `/ab/cd/abcd1234.jpg`) to avoid one giant directory
- OCR engine choice (Tesseract via wrapper is the obvious free option — worth checking .NET-native bindings vs. shelling out)
- Perceptual hash library choice for .NET
- Whether thumbnails are pre-generated at multiple sizes or generated on-demand and cached

---

## Suggested Build Order
1. Data model + basic CRUD (upload single image, view in grid, no tags/OCR yet)
2. Tagging (manual entry + filter by tag)
3. Perceptual hash + dedup-on-upload
4. OCR pipeline + search-by-text
5. Bookmarklet capture endpoint
6. PWA manifest + share target for mobile capture
7. Bulk-tag mode, tag management page
8. Admin/housekeeping page
