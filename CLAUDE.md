# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
dotnet build MediaConfigTool.sln

# Run (debug)
dotnet run --project MediaConfigTool/MediaConfigTool.csproj

# Publish (release)
dotnet publish MediaConfigTool.sln -c Release
```

Output: `MediaConfigTool/bin/Debug/net8.0-windows/MediaConfigTool.exe`

There are no automated tests in this project.

## Architecture

**Narrin Studio** is a WPF desktop app (.NET 8, `net8.0-windows`) for managing photo metadata and rendering configurations. It follows MVVM and communicates with a Supabase PostgreSQL backend via REST.

### Layers

- **Views** (`Views/`, `MainWindow.xaml`): XAML UI. Dialog windows for CRUD operations. `PreviewWindow` and `PreviewPortraitWindow` render metadata overlays on images. `SlideshowWindow` presents a slideshow.
- **ViewModel** (`ViewModels/MainViewModel.cs`): Single large ViewModel. Holds all `ObservableCollection`s (MediaFiles, Locations, Persons, Events, Tags, Tenants), selection state, highlight sets (e.g. `HighlightedLocationIds`), and 20+ `RelayCommand` instances. `RelayCommand` (`ViewModels/RelayCommand.cs`) supports both sync and async delegates.
- **Services**: Business logic and I/O.
  - `SupabaseService.cs` — All backend calls. Raw `HttpClient` against the Supabase REST API with `apikey` header. Tables: `media_asset`, `media_file_instance`, `storage_source`, `tenant`, `location`, `person`, `event`, `tag`, `tag_category`, `media_location`, `media_person`, `media_event`, `media_tag`, `visual_asset`.
  - `MediaFileService.cs` — Scans local folders for `.jpg`/`.jpeg`/`.png`, reads EXIF via `ExifService`, generates 200 px thumbnails (rotation-corrected).
  - `ImportService.cs` — Batch import: ensures/creates a `storage_source` for the root folder, then upserts `media_asset` + `media_file_instance` per file. Reports progress via `IProgress<string>`.
  - `ExifService.cs` — Reads `DateTaken` (format `"yyyy:MM:dd HH:mm:ss"`) and orientation (EXIF tag 274). Falls back to `LastWriteTime`.
  - `AppPathsService.cs` — Centralizes `%LOCALAPPDATA%/Narrin Studio/` paths (Assets, Logs, maps subfolders).
- **Models** (`Models/`): Plain DTOs with `[JsonPropertyName]` attributes for Supabase JSON mapping. Key types: `MediaFile`, `MediaRenderData`, `Tenant`, `Location`, `Person`, `Event`, `Tag`, `TagCategory`, `VisualAsset`, `ImportResult`, `FolderItem`.

### Multi-tenant

All data is scoped to a selected `Tenant`. Switching tenants reloads all collections. `SupabaseService` methods accept a `tenantId` parameter that is appended as a filter on REST queries.

### Preview / Rendering

`MediaRenderData` aggregates a photo's full metadata (location, persons, events, tags, map asset) for display. `PreviewWindow` (landscape) and `PreviewPortraitWindow` (portrait) receive this object and render the overlay. `SlideshowFilterItem` controls which photos appear in `SlideshowWindow`.

### Backend

Supabase project: `https://neuopqphtwylcgiyczox.supabase.co`. The API key is stored as a constant in `SupabaseService.cs`. All HTTP calls use `System.Net.Http.HttpClient` and `System.Text.Json` — no Supabase SDK is used.
