# Offline release readiness

The README remains the product specification. Existing milestone completion claims were not sufficient evidence of a working release. This revision repairs integration defects; it does not certify the README's real-drive and two-week daily-use acceptance gates.

## Implemented and repaired

- Production TMDB and OMDb providers replace the hard-coded fake catalog. Saved user keys take effect without restarting.
- Persistent provider-response caching, sequential request throttling, retry of unsuccessful responses, and atomic artwork writes.
- Volume-relative scan paths, overlapping-root idempotency, hidden/system/reparse-point exclusion, cancellation, pause/resume, and background discovery.
- Smart discovery asks before adding each candidate folder.
- Correct title upserts, persistent manual overrides, one pending review per file, TV series grouping, multi-episode links, accurate season counts, and title search in review.
- Watchlist filtering, drive status badges, playback progress, keyboard activation, bounded 60-card browsing pages, and an actual Drives page.
- Native-video overlay placement, asynchronous VLC callbacks, resume option passed before playback, next-episode transitions, subtitle delay controls, playback error feedback, and progress saved before disposal.
- Persisted subtitle-language, hardware-decoding and auto-play preferences; consistent SQLite backup.
- Windows test/build/package workflow plus deterministic portable regression tests.

## Run and verify

On Windows with .NET 9 and .NET 10 SDKs:

```powershell
dotnet test tests/LocalFlix.Tests/LocalFlix.Tests.csproj -c Release
dotnet test tests/LocalFlix.PortableTests/LocalFlix.PortableTests.csproj -c Release
./scripts/package.ps1
```

Extract the entire `dist/LocalFlix-windows-x64.zip` before launching `LocalFlix.App.exe`. Copying just the EXE omits libVLC and its plugins. This is a portable package, not a signed installer.

1. Save TMDB and optionally OMDb keys in Settings. Scan a small movie folder nested below the drive root.
2. Rescan twice. Titles, watchlist entries and pending reviews must not multiply.
3. Correct a match, rescan, and confirm the correction and watch progress survive.
4. Scan two episodes from one show. Verify one series card, season counts and next-episode playback.
5. Disconnect the drive. Restart the app offline and browse cached titles. Reconnect using a different letter, then play.
6. Check MKV/HEVC, AC3/DTS, embedded and external subtitles, track switches, resume, fullscreen and drive removal during playback.
7. Verify keyboard navigation, high DPI and browsing a 5,000-title library on the target Windows laptop.

## Existing-library compatibility

Earlier builds stored paths relative to the selected scan folder, without persisting which folder was selected. Those paths cannot be safely reconstructed from the database alone. Back up the old database first. For an affected library, close LocalFlix, move `localflix.db` aside, and scan the original media folders using this build. Keep the backup for recovery of old watchlist/history. No media files need to be moved or changed.

## Remaining README gaps

These are not marked complete or silently removed from the product specification:

- Embedded-tag/lightweight media probing and orientation/duration-based personal-video classification.
- Removed-file reconciliation and persistent scan-root management, configurable scan size thresholds.
- Additional year/rating/drive/watched filters, home genre/drive rows, explicit watched controls and full UI performance measurement.
- User-selectable cache location/limit, database import, rolling local logs, theme and subtitle-style controls.
- Directory-mounted volumes and cloned/reformatted-volume hardware tests.
- Several-hundred-case parser corpus, migration/crash-recovery checks and 20,000-title benchmarks.
- Installer/icon/signing and third-party distribution notice review.
- Clean-machine Windows installation, physical-drive playback matrix and two weeks of daily use without blockers.

Online milestones M7 and M8 remain deferred exactly as required by README section 13. They must not be used to imply that the offline release gate has passed.
