# LocalFlix: Project Plan

A native Windows app that turns external hard drives full of movies and TV shows into a streaming-style library, with an optional online streaming section added later.

- Owner: Ismail
- Document status: v1.0, planning
- Date: 2026-10-14
- Intended readers: the owner, and any AI coding assistant or developer implementing the app

## 1. Summary

I have several hard disks with a large collection of movies, TV shows, documentaries and other entertainment content stored offline. Today, watching means browsing folders by hand, guessing what a file is from its name, and opening it in a generic player.

LocalFlix fixes that. I connect a drive, choose a folder, and the app scans it, recognizes each title, downloads posters and details from the internet, and shows everything in a Netflix-style interface. Playback is fully local and works without internet.

The app is built in two separate stages:

1. **Offline section (build first):** local library, metadata from the internet, local playback.
2. **Online section (build second):** a separate streaming area, based on the web apps I already built in earlier repos.

The two sections share one shell and one metadata layer but are developed, tested and shipped independently.

## 2. Context

### 2.1 The problem

- Content is spread across multiple HDDs kept in a closet, connected one at a time.
- File and folder names are inconsistent (release-style names, mixed languages, nested folders, extras, samples).
- There is no poster, synopsis, rating or cast info when browsing in File Explorer.
- Drive letters change between connections, so shortcuts and playlists break.
- Personal videos (phone clips, family footage) may live on the same drives and must not pollute the library.

### 2.2 What already exists

Earlier work in my GitHub account (smile-plzz) covers the online side and the metadata side:

| Repo | Relevance |
|---|---|
| NowShowing, NowShowing-Stream, NowShowing-Stream-V2 | Streaming-style web UI, OMDb metadata, watchlist, history, continue watching, embed player |
| CineGemini-Stream (CineVault) | React 19 + TypeScript + Tailwind + Vite, OMDb orchestration with caching, multi-server playback with failover |
| entertainment-suite-stream | Vanilla JS front end with Vercel serverless functions for OMDb proxying and availability checks |
| WatchMatch | Movie and TV title matching, including OMDb disambiguation and fuzzy-match verification |

What I can reuse directly: UI patterns (hero banner, rows, detail modal, watchlist, continue watching), the OMDb integration, the caching approach, and the fuzzy title matching logic from WatchMatch.

Note: while preparing this plan I could read only the repository front pages and READMEs. They do not name the online embed providers. Before starting the online section, the `src/` code of those repos must be reviewed to list exactly which sources are used (see section 12).

### 2.3 Why a native app and not another web app

- Local files need a real media engine. Browsers and WebView-based players handle MP4/H.264 well but struggle with MKV containers, AC3/DTS/E-AC3 audio, HEVC 10-bit, PGS subtitles and multiple audio tracks, which are exactly what offline collections contain.
- A native app can read drives directly, detect when a drive is plugged in or removed, and remember which drive a file lives on.
- The experience should feel like a TV or streaming box: fullscreen, keyboard and remote friendly, fast.

## 3. Goals and non-goals

### Goals

1. Scan a user-chosen folder (or a whole drive in a guided mode) and find movies, TV episodes and other entertainment content.
2. Identify each title correctly and attach metadata: poster, backdrop, synopsis, year, genres, runtime, IMDb rating, cast, director.
3. Present the library in a streaming-platform style UI.
4. Play files locally with strong codec, audio and subtitle support.
5. Remember the library even when a drive is disconnected, and relink it when the drive returns, regardless of drive letter.
6. Keep personal videos out of the library.
7. Later: add an online streaming section as a clearly separate mode.

### Non-goals (for v1)

- No cloud sync, accounts, or multi-user profiles.
- No transcoding or casting to other devices.
- No file renaming or moving of the user's media (the app never modifies files on the drives by default).
- No mobile or macOS version.
- No downloading, torrenting, or ripping features.

## 4. Users and key scenarios

Primary user: me, on a Windows 10/11 laptop or desktop, with drives connected over USB or SATA.

| # | Scenario | Expected result |
|---|---|---|
| S1 | Plug in a drive, pick a folder, press Scan | Progress view, then titles appear with posters |
| S2 | Open the app with no drive connected | Full library still browsable, items from missing drives shown as "Drive not connected" |
| S3 | Plug a drive back in | Items become playable again automatically, even if the drive letter changed |
| S4 | Open a title | Detail page with poster, synopsis, rating, cast, play button, resume point |
| S5 | Watch a TV series | Season and episode list, auto-play next episode, watched markers |
| S6 | Scan a folder with phone videos mixed in | Personal clips are skipped or placed in a "Needs review" list |
| S7 | A title is matched wrongly | I can fix the match in two clicks and the correction is remembered |
| S8 | Use the app with no internet | Everything works from cached metadata and posters |

## 5. Functional requirements

### 5.1 Library scanning

- FR-1: The user selects one or more root folders to scan. The scan only touches what the user selected.
- FR-2: Optional "Smart scan" mode: scan a whole drive in read-only discovery mode, then propose candidate media folders (ranked) for the user to confirm. Nothing is added without confirmation.
- FR-3: Recognize video files by extension (mkv, mp4, avi, mov, wmv, m4v, ts, m2ts, webm, mpg, mpeg, iso/bdmv optional later).
- FR-4: Ignore noise: samples, trailers, featurettes and extras (by name and size), system folders, recycle bin, hidden files, files under a configurable size threshold.
- FR-5: Classify each item as Movie, TV Episode, Documentary, Other Entertainment, or Unknown.
- FR-6: Detect likely personal videos and exclude them (see 7.3).
- FR-7: Rescans are incremental: only new, changed or removed files are processed.
- FR-8: Scanning never blocks the UI and can be paused, resumed or cancelled.

### 5.2 Identification and metadata

- FR-9: Parse title, year, season, episode, resolution, source and language hints from file and folder names.
- FR-10: Match against an online metadata provider and assign a confidence score.
- FR-11: High confidence matches are accepted automatically. Low confidence goes to a "Needs review" queue.
- FR-12: Fetch and cache poster, backdrop, synopsis, genres, runtime, cast, director, IMDb rating and IMDb ID.
- FR-13: Manual re-match: search by title or paste an IMDb ID. Manual matches are stored and never overwritten by later scans.
- FR-14: Read embedded tags and NFO files when present, as a hint to improve matching.
- FR-15: All fetched metadata and images are cached locally so the library works offline.

### 5.3 Browsing UI

- FR-16: Home screen with a hero banner and rows: Continue Watching, Recently Added, Movies, TV Shows, Documentaries, By Genre, By Drive.
- FR-17: Detail page per movie or series, with seasons and episodes for TV.
- FR-18: Search across titles, actors and genres, with filters (type, year, genre, rating, drive, watched/unwatched).
- FR-19: Watchlist (favorites) and watched state.
- FR-20: Dark, cinematic theme. Keyboard navigation and sensible focus handling throughout.
- FR-21: Items from disconnected drives are visibly marked and not hidden.

### 5.4 Playback

- FR-22: Play the vast majority of local formats (MKV, MP4, AVI, HEVC, H.264, AV1, AC3, DTS, AAC, FLAC).
- FR-23: Multiple audio tracks and subtitle tracks, embedded and external (.srt, .ass, .sub), with delay adjustment and basic styling.
- FR-24: Resume from last position, mark as watched near the end.
- FR-25: Auto-play next episode with a cancelable countdown.
- FR-26: Standard controls: seek, volume, fullscreen, playback speed, keyboard shortcuts, media keys.
- FR-27: Hardware-accelerated decoding where available.

### 5.5 Drive handling

- FR-28: Identify a drive by its volume GUID or serial number plus label, not by drive letter.
- FR-29: Store file paths relative to the drive root, and resolve them to the current drive letter at play time.
- FR-30: Detect drive arrival and removal while the app is running and update item availability live.
- FR-31: Handle NTFS and exFAT drives, and long paths.

### 5.6 Settings

- FR-32: Metadata API keys, scan thresholds, subtitle language preference, theme, cache location and size, clear cache, re-scan, export or import of library database.

## 6. Non-functional requirements

| Area | Requirement |
|---|---|
| Performance | UI stays responsive during scan. Home screen loads in under 2 seconds for a library of 5,000 titles. Scrolling stays smooth with lazy image loading. |
| HDD friendliness | Scans use sequential, throttled I/O. No full-file reads for identification. No constant background polling. Drives may be slow to spin up, and the UI must tolerate that. |
| Reliability | A scan can be killed at any point without corrupting the database. Unplugging a drive mid-scan or mid-playback must not crash the app. |
| Privacy | No telemetry. The only network calls are metadata and image requests, and later the online section. No file names are sent anywhere except to the metadata search query. |
| Safety | The app is read-only on media drives. It writes only to its own data folder. |
| Compatibility | Windows 10 (1809+) and Windows 11, x64. |
| Maintainability | Clear module boundaries so the online section can be added without touching the offline code. |
| Accessibility | Keyboard-only use, readable contrast, scalable text. |

## 7. Technical approach (offline section)

### 7.1 Technology choice

Recommended stack:
- App framework: WPF with modern dark styling on .NET 9 LTS (fallback from WinUI 3 to ensure flawless CLI builds and LibVLC integration)
- Media engine: LibVLCSharp (libVLC)
- Database: SQLite (via Microsoft.Data.Sqlite / EF Core or Dapper)
- Metadata: TMDB as primary (posters, backdrops, cast, overviews, IDs), OMDb for IMDb rating
- Media inspection: light media parser / LibVLC
- Drive detection: .NET DriveInfo plus Windows device-change notifications
- Packaging: Self-contained Windows x64 executable

### 7.2 Architecture

```
LocalFlix.sln
  LocalFlix.App            WPF shell, navigation, themes, DI setup
  LocalFlix.Core           Domain models, interfaces, business rules (no UI, no I/O)
  LocalFlix.Data           SQLite, migrations, repositories, image/metadata cache
  LocalFlix.Scanner        File discovery, filename parsing, classification
  LocalFlix.Metadata       TMDB and OMDb clients, matching and scoring
  LocalFlix.Playback       LibVLC wrapper, player view model, resume logic
  LocalFlix.Drives         Volume identity, arrival/removal events, path resolution
  LocalFlix.Online         (phase 2) provider interface, WebView2 host, failover
  LocalFlix.Tests          Unit and integration tests
```

### 7.3 Scan and identification pipeline

1. Discover: walk selected folder, collect video files with size and modified time. Skip ignored names and tiny files.
2. Group: group files by folder context.
3. Parse: extract title, year, season and episode, quality tags and language from names.
4. Probe: read light technical info.
5. Personal-video filter: score each file as personal using signals (camera-style names, folders like DCIM, short duration, portrait). Exclude or send to review.
6. Match: query metadata provider with title and year. Score candidates by title similarity, year match, runtime closeness and type.
7. Decide: score above high threshold gets accepted; mid-range to Needs Review; low to Unmatched.
8. Enrich and cache: fetch full details, download images, store in local cache.
9. Persist: upsert into SQLite.
10. Report: progress events to the UI.

## 8. Delivery plan

- M0: Foundations (Solution skeleton, DI, logging, SQLite with migrations, CI/build, shell window)
- M1: Scanner and parser (Discover files, parse names, classify, personal-video filter)
- M2: Metadata (TMDB and OMDb clients, matching, cache, Needs Review queue)
- M3: Library UI (Home rows, detail page, search, filters, hero banner)
- M4: Playback (LibVLC player, tracks, subtitles, resume, next episode)
- M5: Drive awareness (Volume identity, offline items, live arrival and removal, relinking)
- M6: Polish and packaging (Settings, error states, performance pass, installer, icon)
- v1.0: Offline release
- M7: Online module (Phase 2)
- M8: Online polish (Phase 2)
