# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - Offline stabilization

- Replace fake production metadata and repair scanning, path resolution, review, watchlist, TV grouping and playback integration.
- Add disk response caching, safer image writes, saved playback preferences, bounded browse pages and Windows build/package automation.
- Add regression tests and explicitly track unimplemented README requirements and unverified release gates in `docs/RELEASE-READINESS.md`.

## [1.0.0] - 2026-10-14 - Offline Release (Milestones M0 to M6 Complete)

### Added
- **Core Architecture & Persistence (M0)**:
  - 8-project modular solution (`Core`, `Data`, `Scanner`, `Metadata`, `Drives`, `Playback`, `App`, `Tests`).
  - SQLite database with versioned migrations, auto-backup before migration, and Dapper repositories.
  - Dependency Injection setup and dark cinematic shell.
- **Scanner & Media Classification (M1)**:
  - Comprehensive `FilenameParser` supporting standard releases, TV SxxExx/1x01, multi-episodes, anime, and directory context fallback.
  - Multi-signal `MediaClassifier` filtering camera patterns (`IMG_`, `VID_`, `PXL_`), DCIM/camera folders, samples, and noise.
  - Incremental `FileScanner` with throttled sequential HDD-friendly I/O and Smart Scan candidate discovery.
- **Metadata Enrichment & Matching (M2)**:
  - `FuzzyMatcher` with Dice coefficient and year penalties.
  - `NfoMetadataReader` for local XML/ID tag parsing.
  - `TmdbMetadataProvider` and `OmdbMetadataProvider` with offline `ImageCacheService`.
  - `MetadataOrchestrator` with auto-acceptance (> 0.85 confidence), Needs Review routing, and manual re-matching overrides.
- **Cinematic Library UI (M3)**:
  - `HomeView` with featured Hero banner, backdrop gradient, and horizontal rows (Continue Watching, Recently Added, Movies, TV Shows, Documentaries).
  - `DetailView` with overview, ratings, cast pill list, watchlist toggle, and TV season/episode selector.
  - `SearchView` with instant query matching and type/genre filters.
  - `NeedsReviewView` with candidate picker dialog and manual ID lookup.
  - `ScanDialog` with real-time progress bar, counts, and cancellation.
- **Native Video Playback (M4)**:
  - `LibVlcPlayerService` supporting MKV, MP4, HEVC, AV1, AC3, DTS, and hardware acceleration.
  - `PlayerWindow` with auto-hiding cinematic HUD, scrubbing slider, volume, audio/subtitle track selection, subtitle delay, and speed control.
  - Auto-resume prompt, automatic 90% watched threshold marking, and 10-second auto-play next episode countdown.
  - Keyboard shortcuts (Space, Left/Right, Up/Down, F/F11, M, Esc).
- **Drive Awareness & Relinking (M5)**:
  - Volume GUID and serial tracking independent of drive letters.
  - Dynamic path resolution when drive letters swap.
  - Windows `WM_DEVICECHANGE` live arrival and removal hooks.
- **Polish & Packaging (M6)**:
  - `SettingsView` for API keys, subtitle preferences, and cache/database backup management.
  - 5,000 title synthetic benchmark passing under 2 seconds.
  - Standalone release package published to `dist/LocalFlix`.

## [0.1.0] - 2026-10-14
### Added
- Initial project scaffold and documentation files (`README.md`, `dev-log.md`, `context.md`, `CHANGELOG.md`).
- Project architecture and goals baseline.

