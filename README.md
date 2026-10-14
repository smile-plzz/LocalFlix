> Implementation status (2026-10-14): offline stabilization in progress. See [release readiness](docs/RELEASE-READINESS.md) for fixes, verification steps and remaining requirements. Historical milestone claims below do not certify the real-drive or two-week acceptance gates.



# LocalFlix (working title): Project Plan

A native Windows app that turns external hard drives full of movies and TV shows into a streaming-style library, with an optional online streaming section added later.

- Owner: Ismail
- Document status: v1.0, planning
- Date: 2026-10-14
- Intended readers: the owner, and any AI coding assistant or developer implementing the app

---

## 1. Summary

I have several hard disks with a large collection of movies, TV shows, documentaries and other entertainment content stored offline. Today, watching means browsing folders by hand, guessing what a file is from its name, and opening it in a generic player.

LocalFlix fixes that. I connect a drive, choose a folder, and the app scans it, recognizes each title, downloads posters and details from the internet, and shows everything in a Netflix-style interface. Playback is fully local and works without internet.

The app is built in two separate stages:

1. **Offline section (build first):** local library, metadata from the internet, local playback.
2. **Online section (build second):** a separate streaming area, based on the web apps I already built in earlier repos.

The two sections share one shell and one metadata layer but are developed, tested and shipped independently.

---

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

---

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

---

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

---

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

---

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

---

## 7. Technical approach (offline section)

### 7.1 Technology choice

Recommended stack:

| Layer | Choice | Reason |
|---|---|---|
| App framework | **WinUI 3 (Windows App SDK) with C# on current .NET LTS** | True native Windows UI, modern look, good performance, first-class WebView2 control for the future online section |
| Media engine | **LibVLCSharp (libVLC)** | Plays almost every container and codec, subtitle and audio-track support, hardware decoding, embeddable in WinUI |
| Database | **SQLite** (via EF Core or Dapper) | Single file, zero setup, fast, easy to back up |
| Metadata | **TMDB as primary** (posters, backdrops, cast, overviews, IDs), **OMDb for IMDb rating** | TMDB has better images and cast data. OMDb is already used in my existing repos and provides the IMDb rating |
| Media inspection | **MediaInfo** (or libVLC parsing) | Read duration, resolution, tracks without reading whole files |
| Drive detection | .NET `DriveInfo` plus Windows device-change notifications | Live connect and disconnect events |
| Packaging | MSIX or a classic installer, self-contained | Simple install on my machines |

Alternatives considered:

- **Tauri or Electron with a React UI:** best fit for my existing web skills and code reuse, but embedding a capable media engine (libmpv or libVLC) in a web-rendered window is awkward, and plain HTML5 video cannot play many MKV/AC3/HEVC files. Rejected for the offline section for that reason.
- **WPF:** mature and simpler, but visually dated and less future-facing than WinUI 3. A reasonable fallback if WinUI 3 causes friction.
- **Hybrid (native shell with a web UI inside WebView2):** possible, but playback still needs a native engine and the two worlds add complexity. Not chosen for v1.

Decision to confirm with the owner: WinUI 3 + LibVLCSharp (recommended) versus WPF + LibVLCSharp (fallback).

### 7.2 Architecture

Layered, with the online section designed as a plug-in module from day one.

```
LocalFlix.sln
  LocalFlix.App            WinUI 3 shell, navigation, themes, DI setup
  LocalFlix.Core           Domain models, interfaces, business rules (no UI, no I/O)
  LocalFlix.Data           SQLite, migrations, repositories, image/metadata cache
  LocalFlix.Scanner        File discovery, filename parsing, classification
  LocalFlix.Metadata       TMDB and OMDb clients, matching and scoring
  LocalFlix.Playback       LibVLC wrapper, player view model, resume logic
  LocalFlix.Drives         Volume identity, arrival/removal events, path resolution
  LocalFlix.Online         (phase 2) provider interface, WebView2 host, failover
  LocalFlix.Tests          Unit and integration tests
```

Key interfaces (so modules stay swappable and testable):

- `IFileScanner`, `IFilenameParser`, `IMediaClassifier`
- `IMetadataProvider` (TMDB, OMDb, and a fake for tests)
- `ILibraryRepository`
- `IDriveService`
- `IPlayerService`
- `IContentSource` (future: `LocalLibrarySource` and `OnlineSource`, so the shell can switch between Offline and Online)

### 7.3 Scan and identification pipeline

1. **Discover:** walk the selected folder, collect video files with size and modified time. Skip ignored names and tiny files.
2. **Group:** group files by folder context (a folder with SxxExx files is a series, a single large file in its own folder is likely a movie).
3. **Parse:** extract title, year, season and episode, quality tags and language from names. Normalize dots, underscores, brackets and release-group tags.
4. **Probe:** read light technical info (duration, resolution, tracks). Duration also helps filter trailers and clips.
5. **Personal-video filter:** score each file as "personal" using signals such as camera-style names (IMG_, VID_, PXL_, MOV_, DSC), folders like DCIM, Camera, WhatsApp, Screen Recordings, embedded camera make/model tags, very short duration, portrait orientation, and no plausible title match. Personal-looking files are excluded or sent to Needs review. They are never added silently.
6. **Match:** query the metadata provider with title and year (and season/episode for TV). Score candidates by title similarity, year match, runtime closeness and type. Reuse the fuzzy matching and disambiguation lessons from WatchMatch.
7. **Decide:** score above the high threshold gets accepted. Mid-range goes to Needs review with top candidates. Low gets Unmatched.
8. **Enrich and cache:** fetch full details, download images at suitable sizes, store in the local cache.
9. **Persist:** upsert into SQLite. Remember user overrides permanently.
10. **Report:** progress events to the UI (files found, matched, needing review, errors).

Important design rules:

- The pipeline is resumable and idempotent. Re-running it produces the same result without duplicates.
- Metadata calls are rate-limited, batched and cached, with retry and backoff.
- Everything the pipeline decides can be explained ("matched because title 0.96, year exact, runtime within 3 minutes").

### 7.4 Data model (simplified)

- `Drive` (id, volume GUID, serial, label, last seen, last known letter)
- `MediaFile` (id, drive id, relative path, size, modified, duration, resolution, audio/subtitle info, hash of path+size for identity, status)
- `Title` (id, type, TMDB id, IMDb id, name, year, overview, runtime, genres, rating, poster path, backdrop path, matched-by [auto or manual])
- `Series`, `Season`, `Episode` (linked to `Title` and `MediaFile`)
- `Person` and `Credit` (cast and crew)
- `PlaybackState` (file id, position, watched, last played)
- `UserList` (watchlist, favorites)
- `ScanRoot` (drive id, relative folder, last scan, options)
- `ReviewItem` (file id, candidate matches, reason)

SQLite file lives in the app data folder. Migrations are versioned. A backup copy is made before each migration.

### 7.5 UI plan

Streaming-style layout, dark theme.

- **Shell:** left or top navigation (Home, Movies, TV Shows, Documentaries, Watchlist, Needs Review, Settings). Later a clear **Offline / Online** switch at the top level.
- **Home:** hero banner with backdrop and Play button, then horizontal rows with lazy-loaded posters.
- **Detail page:** backdrop header, poster, synopsis, rating badge, runtime, genres, cast strip, Play or Resume, season and episode list for series, file/drive info, "Fix match" action.
- **Search:** instant results, filter chips.
- **Player:** fullscreen-first, auto-hiding controls, audio/subtitle menus, next-episode prompt, resume prompt.
- **Scan screen:** folder picker, scan options, live progress, summary with counts and a link to Needs Review.
- **Empty and error states:** friendly messages (no drive, no internet, drive disconnected, no matches).

Design notes: reuse the visual language and interaction patterns from NowShowing and CineVault, keep a consistent token set for colors, spacing and type, and virtualize long lists.

### 7.6 Drive identity and relinking

- On first scan, record the volume GUID and serial of the drive.
- Store only paths relative to the drive root.
- When a drive appears, match it by GUID or serial and update its current letter, then flag its files as available.
- When it disappears, mark files unavailable but keep metadata, posters and watch progress.
- Edge cases to test: two drives with the same label, drive cloned or reformatted, letter reassigned to a different drive, drive mounted as a folder.

### 7.7 Playback details

- Wrap LibVLC behind `IPlayerService` so the engine can be swapped later (for example libmpv).
- Enable hardware decoding by default with a fallback toggle.
- Persist position every few seconds and on exit.
- Detect missing files or drives at play time and show a clear "Connect drive X" message.
- Subtitle preference: auto-select by preferred language, support external subtitle files found beside the video.

### 7.8 Error handling and logging

- Structured local logs (rolling files) with an in-app "Open logs folder" button.
- Never crash on a bad file: mark it with a reason and continue.
- User-visible errors are plain language with a suggested action.

---

## 8. Testing strategy

- **Filename parser:** a fixture corpus of several hundred real-world style names (movies, series, anime-style numbering, multi-episode files, foreign titles, years inside titles) with expected outputs. This is the highest-value test suite.
- **Classifier and personal-video filter:** table-driven tests with positive and negative examples.
- **Matcher:** tests using a fake metadata provider with scripted candidates, including ambiguous titles and remakes.
- **Scanner integration:** generated fake folder trees on a temp drive, including unplugging mid-scan.
- **Drive service:** simulated arrival and removal events and letter changes.
- **Database:** migration tests and crash-safety tests.
- **Playback matrix (manual):** MKV/H.264, MKV/HEVC 10-bit, MP4/AAC, AVI/XviD, AC3 and DTS audio, PGS and SRT subtitles, multiple audio tracks, 4K file, very large file, file on a slow HDD.
- **UI checks:** keyboard navigation, high DPI, small and large windows, offline mode, first-run experience.
- **Performance:** synthetic library of 5,000 and 20,000 titles for load and scroll timing.

Definition of done for a feature: code reviewed, tests added and passing, works on a real drive from the closet, no regressions in the manual playback matrix.

---

## 9. Delivery plan

Each milestone ends with something I can run and judge.

| # | Milestone | Outcome | Acceptance check |
|---|---|---|---|
| M0 | Foundations | Solution skeleton, DI, logging, SQLite with migrations, CI build, empty shell window | App launches, database created, CI green |
| M1 | Scanner and parser | Discover files, parse names, classify, personal-video filter | Scan a real folder and print a correct list of parsed titles in a debug view |
| M2 | Metadata | TMDB and OMDb clients, matching, cache, Needs Review queue | At least 90% of a real folder matched correctly, the rest land in review |
| M3 | Library UI | Home rows, detail page, search, filters, hero | Browse a scanned library that feels like a streaming app |
| M4 | Playback | LibVLC player, tracks, subtitles, resume, next episode | The full manual playback matrix passes |
| M5 | Drive awareness | Volume identity, offline items, live arrival and removal, relinking | Swap drives and letters, library behaves correctly |
| M6 | Polish and packaging | Settings, error states, performance pass, installer, signing, icon | Clean install on a second machine, 5,000 title test passes |
| **v1.0** | **Offline release** | **Stable offline app** | **Used daily for two weeks with no blocking issues** |
| M7 | Online module | Provider interface, WebView2 host, online browse and play, failover | See section 10 |
| M8 | Online polish | Unified watchlist and history, settings, hardening | Mode switch is seamless, offline mode unaffected |

Suggested order of work inside each milestone: interfaces first, then tests, then implementation, then UI.

---

## 10. Online section (phase 2)

### 10.1 What it is

A separate mode for streaming content over the internet, reusing the ideas and parts of my earlier streaming web apps. The top-level switch shows **Offline** and **Online**. The offline mode keeps using the internet only for metadata and images. The online mode is where streaming happens.

### 10.2 Approach

- Build it as `LocalFlix.Online`, behind the same `IContentSource` abstraction, so the shell, metadata layer, watchlist and history are shared.
- Host the player in a **WebView2** control, which also gives the app a controlled place to restrict navigation, block pop-ups and ads, and prevent unexpected redirects.
- Use an `IStreamProvider` interface so each source is a small replaceable adapter (build the embed URL for an IMDb or TMDB ID, health check, name). The app keeps an ordered list of providers and fails over automatically, the way CineVault's multi-node player does.
- Reuse from existing repos: OMDb/TMDB title lookup and caching, health monitoring and failover logic, continue-watching and watchlist behavior, the browse UI patterns.
- Keep the provider list in a user-editable configuration file rather than hard-coded in the app.
- Keep the code, data and settings of the online module isolated so it can be removed or disabled without touching the offline app.

### 10.3 Prerequisite tasks before M7

1. Review the `src/` code of NowShowing-Stream-V2, CineGemini-Stream and entertainment-suite-stream and list which sources they use and how reliably.
2. Decide the provider list and whether any legitimate alternatives should be offered alongside (see 11).
3. Define how the online section behaves when the source is unavailable (clear error, try next provider, suggest checking the local library if the title exists there).

### 10.4 Nice-to-have integration

If a title exists in both the local library and online, prefer the local file and show a "Available locally" badge in the online section.

---

## 11. Risks and legal considerations

| Risk | Impact | Mitigation |
|---|---|---|
| Wrong matches on messy file names | Library looks broken | Confidence scoring, Needs Review queue, easy manual re-match, remembered overrides |
| Personal videos leak into the library | Privacy and clutter | Conservative personal-video filter, nothing added silently, review queue |
| Slow or sleeping HDDs | Scans feel stuck | Throttled I/O, progress UI, cancel and resume, no full-file reads |
| Codec or subtitle edge cases | Some files will not play | LibVLC coverage, manual test matrix, engine abstraction for a fallback |
| Metadata API limits or outages | Missing posters or details | Aggressive caching, batching, backoff, graceful offline mode |
| API terms and attribution | Terms violation | Follow TMDB attribution requirements and usage terms. Note TMDB and OMDb terms differ for commercial use |
| Drive identity errors | Wrong file or broken link | Identify by volume GUID and serial, test cloned and reformatted drives |
| WinUI 3 learning curve or tooling issues | Slower delivery | Keep the UI thin, prove the shell early in M0, fallback to WPF is acceptable |
| LibVLC licensing | Compliance | libVLC is LGPL. Link dynamically, ship license notices, avoid modifying it |
| **Online section legality** | **Possible copyright exposure, takedowns, malware or ad risk from third-party embeds** | **See below** |

### Online section: honest note

The offline section plays files I already own and is low risk. The online section is different. Free third-party embed sources commonly serve content without licenses from the rightsholders, and they are also a common route for aggressive ads and malicious redirects. Recommendations:

- Treat the online section as a personal-use tool. Do not publish it on the Microsoft Store or distribute the installer widely with built-in sources.
- Keep sources out of the shipped code and installer, in a separate user-supplied configuration.
- Sandbox the web content in WebView2 with navigation allow-listing, pop-up blocking and no access to local files or the offline library database.
- Where possible, add legitimate options (for example "where to watch" links to licensed services using TMDB watch-provider data, and trailers) so the online section has value that does not depend on unlicensed sources.
- Because the offline app is the core value and carries no such risk, keep v1.0 completely independent of the online module.

This is a risk statement, not legal advice, and the owner should decide how far to take it.

---

## 12. Open questions for the owner

1. Confirm the stack: WinUI 3 + C# + LibVLCSharp (recommended) or WPF.
2. Roughly how many titles and how many drives, and are they NTFS or exFAT?
3. Primary languages of content (affects title matching and subtitles)?
4. Is a TV or remote-friendly "10-foot" mode needed, or is it laptop and mouse first?
5. Should "Smart scan whole drive" be in v1 or after v1.0?
6. Is there any content that must be kept private even inside the library (a locked or hidden section)?
7. Which online sources are currently used in the existing repos, and are there any I want to drop?
8. Preferred distribution: personal install only, or something shareable?
9. Is an in-app updater wanted, or manual installs are fine?
10. Metadata language preference (English only, or Bangla and other languages where available)?

---

## 13. Instructions for the implementing assistant (for ChatGPT or any developer)

- Build strictly in milestone order (section 9). Do not start the online module before v1.0.
- Create the solution layout from section 7.2 first, with interfaces and a fake metadata provider, so everything is testable without network access.
- Start with the filename parser and its fixture tests. Do not move on until they pass.
- Never write to the user's media drives. All app data goes to the app data folder.
- Never send more than the title query to metadata services.
- Keep secrets (API keys) out of source control. Use user settings or environment variables and provide an example config file.
- Keep each milestone small and runnable. Provide a short "how to run and verify" note at the end of every milestone.
- Prefer simple, readable code and explicit names over clever abstractions.
- Do not use em-dashes in any documentation, UI text or comments.
- When a decision is unclear, pick the safest default, state the assumption in the milestone notes, and continue.

---

## 14. Glossary

- **Embed provider:** a third-party web page or player that can be loaded inside the app to stream a title.
- **Volume GUID:** a stable identifier Windows assigns to a disk volume, independent of its drive letter.
- **Confidence score:** a number between 0 and 1 expressing how sure the matcher is about a title match.
- **Needs Review:** the queue of files the app could not identify with enough confidence.
- **Hero banner:** the large featured title area at the top of the home screen.

A native Windows desktop application that turns external hard drives full of movies, TV shows, and entertainment content into a sleek, streaming-platform style library with offline playback and automatic metadata enrichment.

## Architecture & Tech Stack
- Platform: Windows 10 (1809+) and Windows 11 (x64)
- Framework: WPF on .NET 9.0 with modern dark theme
- Media Engine: LibVLCSharp (LibVLC) with hardware acceleration and extensive audio/subtitle codec support
- Database: Embedded SQLite with versioned migrations
- Metadata: TMDB API (primary details and artwork) + OMDb API (IMDb scores) with full offline disk caching
- Drive Handling: Volume GUID and serial tracking for drive-letter agnostic relinking

## Solution Structure
- `LocalFlix.Core`: Pure domain models, contracts, and business rules.
- `LocalFlix.Data`: SQLite persistence, migrations, and caching layers.
- `LocalFlix.Scanner`: Directory crawling, filename parsing, and personal-video filtering.
- `LocalFlix.Metadata`: TMDB/OMDb clients, fuzzy matching, and confidence scoring.
- `LocalFlix.Playback`: LibVLC player abstraction, playback states, and track management.
- `LocalFlix.Drives`: Volume GUID detection, device arrival/removal notifications.
- `LocalFlix.App`: WPF user interface and dependency injection setup.
- `LocalFlix.Tests`: Comprehensive unit and integration test suite.

## Building and Testing
```bash
# Build the entire solution
dotnet build LocalFlix.sln

# Run all test fixtures
dotnet test LocalFlix.sln
```
