# Community translation packs

This folder holds the downloadable ZIPs for the in-app **Community** tab. The app reads
[`../catalog.json`](../catalog.json) from the repo root and lets users download & install any
listed pack with one click.

## Adding a translation

1. **Build the ZIP.** Inside it, put one folder named like the language, with `dialogue.txt`
   (or `dialogue.csv`) directly inside — that folder name becomes the installed name under
   `www/languages/`:

   ```
   deutsch.zip
   └── Deutsch/
       ├── dialogue.txt        (or dialogue.csv)
       ├── font/               (optional)
       └── pictures/ …         (optional translated images)
   ```

2. **Drop the ZIP in this folder** (e.g. `packs/deutsch.zip`).

3. **Add an entry to `catalog.json`.** Only `id`, `language`, `version` and `file` are required;
   the rest is display metadata:

   ```json
   {
     "id": "deutsch-marie",
     "language": "Deutsch",
     "authors": "Marie",
     "version": "1.0",
     "gameVersion": "3.0.13",
     "description": "Vollständige deutsche Übersetzung.",
     "source": "https://steamcommunity.com/app/.../discussions/...",
     "file": "packs/deutsch.zip"
   }
   ```

4. **Commit & push.** The app fetches the catalog live — no app update needed.

## Notes

- Bump `version` when you update a pack; the app then shows **"Update available"** to users
  who installed the older version.
- `authors` / `source` credit the original translators — please keep them filled in.
- Large, image-heavy packs can instead be hosted as GitHub **Release** assets; put the full
  asset URL in `file` (absolute URLs are used as-is).
