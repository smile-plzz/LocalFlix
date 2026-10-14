# LocalFlix

A native Windows desktop application that turns external hard drives full of movies, TV shows, and entertainment content into a sleek, streaming-platform style library with offline playback and automatic metadata enrichment.

## Architecture & Tech Stack
- Platform: Windows 10 (1809+) and Windows 11 (x64)
- Framework: WPF on .NET 9.0 LTS with modern dark theme
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
