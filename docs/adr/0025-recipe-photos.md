# 0025 Recipe photos: files on a volume named by a photo id, resized with SkiaSharp, served through the API

**Status:** Accepted, 2026-11-10. Builds on [0012](0012-module-persistence.md), [0014](0014-spa-authentication.md) and [0023](0023-recipe-model.md).

## Context
A recipe can have one optional photo, uploaded from a phone (several MB, often sideways with an EXIF orientation, often with a location in its metadata) or attached by an agent from the recipe's source page (E8). The library shows it as a small tile, the detail as a wide picture. Everything else of the household lives in Postgres and is backed up with it (E10). The app runs as one container on a homelab, the repo is public, and every `/api` response is `no-store`.

Options for the bytes:
- **Postgres** (`bytea`). One backup, but every photo travels through EF and the connection pool, and the database grows by megabytes per recipe.
- **A folder on a Docker volume.** The database keeps a reference; files are read straight from disk. A second thing to back up.
- **Object storage** (S3, MinIO). Another service to run for a few hundred files.

Options for resizing: none (serve the phone's original), in the browser only (an agent's upload would not be covered), or on the server with a library. ImageSharp is fully managed, but its current releases need a Six Labors license key for Release builds and the 3.1 line without one has open security advisories. SkiaSharp is MIT licensed and maintained by Microsoft; it needs a native library, which the `SkiaSharp.NativeAssets.Linux.NoDependencies` package ships for the container without extra system packages.

## Decision
- **Files on a volume, a photo id in the database.** `recipes.recipes.photo_id` (nullable). Files live in the folder `Recipes:Photos:Path` (default `data/recipe-photos` under the content root, git-ignored; `/data/recipe-photos` on the `recipe-photos` volume in Docker Compose, or a host folder from `RECIPE_PHOTOS_VOLUME`). Each size is one file, `<photo id>-<size>.webp`.
- **A new photo id with every upload.** A file never changes once written, so its URL can be cached for good. The new files are written first, then the recipe points at them inside a transaction that locks the recipe row, then the replaced photo's files are deleted. A failed save deletes the new files; a failed delete is logged and leaves an orphan file, never a recipe without its files.
- **Two sizes, WebP.** `card` is a 480 px square crop for tiles, `detail` the whole picture with its longer side at most 1600 px. The server decodes JPEG, PNG and WebP only, recognised by content rather than by content type; at most `Recipes:Photos:MaxBytes` (10 MB) and 50 megapixels. JPEG is decoded at a reduced scale when it is larger than needed. The picture is turned upright by its EXIF orientation and only pixels are encoded, so no metadata reaches the volume.
- **REST and contract.** `PUT /api/recipes/{id}/photo` takes the picture as the raw body (no multipart form) and answers the recipe, 413 when too large and 415 when not such a picture; `DELETE` removes it. `GET /api/recipes/{id}/photos/{photoId}/{card|detail}` serves the current photo only. Responses carry `photo: { card, detail }` URLs or null. `IRecipesModule.SetPhotoAsync(recipeId, stream)` and `RemovePhotoAsync` let an agent attach a picture; fetching it from the source page belongs to the MCP tool (E8).
- **Caching.** Photo URLs need a session like every `/api` endpoint, and are marked `CacheImmutably()`: `private, max-age=31536000, immutable`, set after authentication and antiforgery have asked for `no-cache`. Everything else under `/api` stays `no-store`.
- **Archive keeps the photo.** Recipes are never deleted (0023), so a photo goes only when it is replaced or removed.
- **The editor applies the photo on save.** Picking, replacing or removing a photo is part of the unsaved changes; after the recipe saves (a new one gets its id then), the SPA uploads or removes the photo. If that fails the recipe stays saved and a toast says so.

## Consequences
- Backups (E10) must copy the photo volume next to the database dump. A restore of only one of them leaves recipes without pictures or pictures without recipes; both are harmless (the tile with the initial shows, orphans take space).
- A photo cached in a browser outlives a sign-out on that device. It is household data of low sensitivity and the URL cannot be guessed, which we accept for fast library scrolling.
- The image needs a native library (SkiaSharp, about 10 MB). Moving to a managed library later only touches `RecipePhotoProcessor`.
- HEIC from iPhones is not decoded; iOS converts it to JPEG when a picture is chosen in a browser, and the SPA refuses other types before sending.
- Orphan files (a failed delete, two uploads at the same moment) are not cleaned up automatically yet.
