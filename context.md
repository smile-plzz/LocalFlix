# Project Context: LocalFlix

## Goals
- Provide a simple, self-hosted media server that allows users to stream their local movie and video collection on any device via a web browser.
- Deliver a clean, Netflix-style browsing and streaming interface.
- Support smooth video streaming with seeking, subtitles, and resume playback.

## Proposed Tech Stack Options
- **Backend Options**:
  - Python (FastAPI / Flask) with async streaming and Range header support.
  - Node.js (Express / Fastify) with native streaming.
- **Frontend Options**:
  - Vanilla HTML5 / Modern CSS / Vanilla JS (lightweight, zero-build setup).
  - React / Vite + Tailwind CSS (rich interactive single-page application).
- **Database / Metadata Storage**:
  - SQLite or file-based JSON cache for metadata and watch progress.

## Constraints & Requirements
- Support HTTP Range requests (`Accept-Ranges: bytes`) for smooth video seeking.
- Cross-platform support (Windows, macOS, Linux).
- Efficient directory indexing without blocking streaming operations.
- Clean separation between server and client components.
