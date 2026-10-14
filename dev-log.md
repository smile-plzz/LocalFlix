# Developer Log

## [2026-10-14 14:31:00 +06:00] - Project Initialization
- **Task**: Initialize repository documentation structure according to development workflow.
- **Actions**:
  - Cloned repository from `https://github.com/smile-plzz/LocalFlix`.
  - Created missing core workflow documents: `README.md`, `dev-log.md`, `context.md`, `CHANGELOG.md`.
  - Established project baseline for media streaming server.
- **Verification**: Documentation created and validated.
- **Status**: Completed.

## [2026-10-14 14:45:00 +06:00] - Milestone 0: Foundations Completed
- **Task**: Implement solution layout, domain models, database schema with migrations, repository, offline image cache, and WPF dark shell.
- **Actions**:
  - Initialized solution `LocalFlix.sln` with 8 modular projects (Core, Data, Scanner, Metadata, Drives, Playback, App, Tests).
  - Defined pure domain entities (Drive, MediaFile, TitleItem, Series, Season, Episode, PlaybackState, UserList, ReviewItem) and service contracts.
  - Implemented `DatabaseMigrator` with automated pre-migration backup and versioned migrations.
  - Implemented `SqliteLibraryRepository` with Dapper for high-speed local queries.
  - Implemented `ImageCacheService` for offline artwork caching.
  - Implemented `FakeMetadataProvider` for deterministic offline testing.
  - Designed WPF dark cinematic shell (`MainWindow.xaml` and `App.xaml`) with dependency injection and mode switcher.
- **Verification**:
  - Solution build succeeded: 0 warnings, 0 errors.
  - `M0_DatabaseTests` passed (3/3 tests green).
- **Status**: Completed.

## [2026-10-14 14:50:00 +06:00] - Milestone 1: Scanner and Parser Completed
- **Task**: Implement file discovery, filename parsing, media classification, personal video exclusion, and test fixtures corpus.
- **Actions**:
  - Implemented `FilenameParser` with support for movies, TV series (SxxExx and 1x01), multi-episode files, anime numbering, release tags, and directory context fallback.
  - Implemented `MediaClassifier` with heuristics for camera prefixes, DCIM/camera folders, sample/trailer noise filtering, and reason generation.
  - Implemented `FileScanner` with throttled sequential I/O, incremental rescanning (skipping unchanged files), and smart scan candidate folder discovery.
  - Added comprehensive test suite `M1_ScannerAndParserTests` covering real-world release naming, foreign films, anime, personal video filtering, and directory scans.
- **Verification**:
  - 40 tests passed across all test suites (40/40 green in 170 ms).
  - Incremental scanning and personal video review queue verified.
- **Status**: Completed.

## [2026-10-14 14:55:00 +06:00] - Milestone 2: Metadata Completed
- **Task**: Implement metadata providers (TMDB, OMDb, Fake), fuzzy title matcher, NFO hint reader, offline artwork caching, and Needs Review queue orchestration.
- **Actions**:
  - Created `FuzzyMatcher` using Dice coefficient bigram scoring and year penalties.
  - Implemented `NfoMetadataReader` to detect local XML/text metadata and extract IMDb/TMDb IDs.
  - Implemented `TmdbMetadataProvider` and `OmdbMetadataProvider` with strict query filtering (only clean title and year sent).
  - Implemented `MetadataOrchestrator` to handle auto-acceptance (> 0.85 confidence), Needs Review routing, and manual re-matching with overrides remembered.
  - Added `config.example.json` for API keys and user preferences.
  - Built `M2_MetadataTests` verifying auto-matching, review routing, and manual corrections.
- **Verification**:
  - 48 tests passed (48/48 green).
- **Status**: Completed.

## [2026-10-14 15:00:00 +06:00] - Milestone 3: Library UI Completed
- **Task**: Implement streaming-platform style UI views (Home with Hero and horizontal rows, DetailView with season/episodes, SearchView with type/genre filters, NeedsReviewView, and ScanDialog).
- **Actions**:
  - Created `TitleCardControl` with poster artwork, rating badge, type chip, watch progress indicator, and disconnected drive overlay.
  - Implemented `HomeView` with featured Hero banner, backdrop gradient overlay, and horizontal rows (Continue Watching, Recently Added, Movies, TV Shows, Documentaries).
  - Implemented `DetailView` with backdrop header, poster, meta chips, watchlist toggle, overview, cast pill list, and series season/episode selector.
  - Implemented `SearchView` with live search filtering across titles, directors, actors, and media type/genre chips.
  - Implemented `NeedsReviewView` with candidate suggestion matching, confidence pill badges, and direct ID lookup.
  - Implemented `ScanDialog` with folder browsing, smart scan option, real-time progress bar, and cancellation handling.
  - Implemented `WindowsDriveService` for volume serial, label, and volume GUID resolution.
  - Added `M3_UiTests` verifying ViewModel display formatting and badge logic.
- **Verification**:
  - 53 tests passed (53/53 green).
  - UI builds with 0 errors and 0 warnings.
- **Status**: Completed.

## [2026-10-14 15:03:00 +06:00] - Milestone 4: Playback Completed
- **Task**: Implement native media player using LibVLCSharp, subtitle and audio track selection, delay adjustment, resume prompts, and auto-play next episode.
- **Actions**:
  - Implemented `LibVlcPlayerService` with hardware decoding flags, position/duration events, audio/subtitle track enumeration and switching, subtitle delay control, and external subtitle auto-attachment (.srt, .ass, .vtt).
  - Implemented `PlayerWindow` with auto-hiding cinematic HUD, seek scrubber, volume control, track selection, speed controller (0.5x to 2.0x), and fullscreen mode.
  - Implemented periodic watch position persistence (every 5 seconds) and automatic 90% watched threshold marking in SQLite.
  - Added resume prompt modal and next episode countdown prompt (10-second countdown before playing next episode).
  - Added full keyboard shortcuts (Space, Left/Right, Up/Down, F/F11, M, Esc).
  - Built `M4_PlaybackTests` verifying resume positions and watched state thresholds.
- **Verification**:
  - 59 tests passed (59/59 green).
  - Clean build across all 8 projects.
- **Status**: Completed.




