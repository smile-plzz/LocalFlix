# Project Context: LocalFlix

## Project Overview
LocalFlix is a native Windows application that transforms external hard drives containing movies, TV shows, and documentaries into a cinematic, streaming-style library with offline playback and an optional online streaming module in phase 2.

## Core Architectural Decisions
- Application Framework: WPF (.NET 9.0 LTS) with modern cinematic dark styling.
- Media Engine: LibVLCSharp / LibVLC for full codec (MKV, HEVC, AC3, DTS, ASS/SRT subtitles) and hardware-accelerated playback.
- Database: SQLite (via Microsoft.Data.Sqlite / Dapper) stored in LocalAppData with versioned migrations and automatic backups.
- Metadata: TMDB as primary provider (posters, backdrops, cast, overview, IDs), OMDb for IMDb ratings.
- Drive Management: Volume GUID and disk serial tracking with drive-relative paths and live connect/disconnect detection.
- Personal Video Filtering: Multi-signal heuristic classifier (camera filename patterns, DCIM folders, orientation, duration) to keep personal clips out of the library.

## Solution Layout
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

## Milestone Roadmap
- M0: Foundations (Solution skeleton, DI, logging, SQLite with migrations, empty shell window)
- M1: Scanner and parser (Discover files, parse names, classify, personal-video filter)
- M2: Metadata (TMDB and OMDb clients, matching, cache, Needs Review queue)
- M3: Library UI (Home rows, detail page, search, filters, hero banner)
- M4: Playback (LibVLC player, tracks, subtitles, resume, next episode)
- M5: Drive awareness (Volume identity, offline items, live arrival and removal, relinking)
- M6: Polish and packaging (Settings, error states, performance pass, installer, icon)
- v1.0: Offline release
- M7 & M8: Online streaming module (Phase 2)
