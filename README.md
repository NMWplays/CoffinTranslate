# CoffinTranslate

**CoffinTranslate** is an unofficial, open-source translation manager for
[*The Coffin of Andy and Leyley*](https://store.steampowered.com/app/2378900/).

It lets anyone install a finished fan translation into their game in a few clicks (no manual
file copying, no CSV editing, no multi-page instructions), and includes an advanced editor for
people who want to create their own translation from scratch.

> **Status:** The installer is complete and stable. The translation editor is available as an
> advanced feature. Prebuilt downloads for Windows and Linux are provided under
> [Releases](../../releases).

## Download

Grab the latest build from the [Releases page](../../releases):

- **Windows:** download `CoffinTranslate-win-x64.exe` and double-click it. No installation, no
  dependencies. Windows SmartScreen may warn about an unknown publisher on the first run.
- **Linux:** download `CoffinTranslate-linux-x64`. It is a self-contained binary, so you need to
  give it the executable permission yourself before running it, for example:

  ```
  chmod +x CoffinTranslate-linux-x64
  ./CoffinTranslate-linux-x64
  ```

  (Or right-click the file in your file manager, open Properties, and tick "Allow executing file
  as program".) It runs on mainstream 64-bit desktop distributions such as Ubuntu, Mint, Debian,
  Fedora and Arch.

Prefer to build it yourself? See [Building from source](#building-from-source).

## Features

- **One-click install** of any finished translation. Drop a folder, a ZIP download, or a `.cld`
  file onto the app and hit *Install*.
- **Community tab** with a built-in catalog. Browse available translations, download and install
  them with one click, and get an *Update available* hint when a newer version is published.
- **Automatic game detection.** Finds your Steam installation automatically, including secondary
  Steam libraries. A manual folder picker is there for non-Steam copies.
- **Launch the game** straight from the app once it is detected.
- **Package inspection before installing.** Shows the translation's language, credits and contents
  (game text, translated images, custom font), plus warnings for common problems (wrong encoding,
  several translations in one archive, unsafe archives).
- **Manage installed translations.** See everything in the game's `languages` folder and remove
  translations safely.
- **Nothing is ever deleted permanently.** Replaced or removed translations are moved to a backup
  folder, never erased.
- **Advanced editor.** A side-by-side editor that reads the game's own source text, lets you
  translate every line without touching a spreadsheet, flags formatting-code mismatches, tracks
  progress and estimated time remaining, filters by speaker, jumps back to where you left off, and
  exports a game-ready `dialogue.txt`.
- **Multilingual UI.** English and German out of the box, with automatic system-language detection.
  Adding a language is a single JSON file.
- **Cross-platform.** Built with [Avalonia](https://avaloniaui.net/). Runs on Windows and Linux.

## Installing a translation

1. Start CoffinTranslate. Your game is usually detected automatically. If not, click *Change…* and
   select the folder that contains `Game.exe`.
2. Either open the **Community** tab and install a listed translation with one click, or drag a
   translation you downloaded elsewhere (folder, `.zip`, or `.cld` file) onto the app.
3. Check the preview (language, credits, contents) and click **Install now**.
4. Start the game and choose your language under **Language** in the main menu.

Note: RAR and 7z archives must be extracted first. ZIP files work directly.

## For translators

CoffinTranslate installs any translation package that follows the game's official layout: a folder
(or ZIP) containing a `dialogue.txt` or `dialogue.csv`, optionally with a `font/` folder and
translated image folders (`pictures/`, `system/`, `titles1/`), or a single compiled `.cld` file.

To translate from scratch, use the built-in **editor**. It reads the game's full translatable
source locally and shows the English original next to each line, so you never have to edit a
spreadsheet by hand. See [How the editor gets the source text](#how-the-editor-gets-the-source-text).

### Publishing a translation to the Community tab

Finished translations shown in the in-app **Community** tab live in a separate repository,
[**CoffinTranslate-Packs**](https://github.com/NMWplays/CoffinTranslate-Packs). Keeping them apart
lets translators be given write access to the packs without touching the app's source code. To get
your translation listed, add your pack and a `catalog.json` entry there — the packs repo's README
walks through it step by step.

## Building from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```
dotnet build CoffinTranslate.slnx     # build everything
dotnet test  CoffinTranslate.slnx     # run the test suite
dotnet run --project src/CoffinTranslate
```

To produce a self-contained single-file build like the ones on the Releases page, run the helper
script from the repo root:

```
.\publish.ps1                         # builds win-x64 and linux-x64 into .\publish
.\publish.ps1 -Runtimes win-x64       # one target only
```

### Project structure

| Path | Contents |
| --- | --- |
| `src/CoffinTranslate` | Avalonia desktop app (views, view models, services, UI translations) |
| `src/CoffinTranslate.Core` | UI-free core library: game detection, package reading, installing, backups |
| `tests/CoffinTranslate.Core.Tests` | xUnit tests for the core library |
| `docs/` | File format documentation and analysis notes |

UI strings live in `src/CoffinTranslate/Assets/i18n/*.json`. To add a UI language, copy `en.json`,
translate the values, and register the language code in `Services/Localizer.cs`.

### How the editor gets the source text

The game ships its full translatable source (every line of dialogue, item names and descriptions,
menus, speaker names) inside `www/languages/tool/Translator.dat`. CoffinTranslate reads that file
locally for reference so you can translate from scratch with the English source shown next to each
line. The format is documented in
[`docs/official-tool-analysis.md`](docs/official-tool-analysis.md) §3.5. No game content is
redistributed: the file stays in your own installation.

## Safety

The installer only writes inside the game's `www/languages/` folder and never touches game files or
the official `tool/` folder. Anything it replaces or removes is moved to
`%LOCALAPPDATA%/CoffinTranslate/backups` (or the XDG equivalent on Linux), so nothing is deleted.
Archives are validated against path-traversal attacks before extraction.

## Disclaimer

CoffinTranslate is an unofficial fan project. It is not affiliated with, endorsed by, or supported
by Kit9 Studio. It does not contain or distribute any game content. You need to own the game, and
translations are made and shared by their respective authors.

## License

To be decided before the first public release (MIT is the working assumption).
