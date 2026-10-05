# AGENTS.md

Instructions for AI coding agents working in this repository.

## Repository facts

- Personal open-source project, **GPL-3.0**. Do not relicense or remove attribution to the original author.
- VCS is **git** hosted at `github.com/jquinterocortes/iTunesMatchTagger`. The upstream project was SVN — never use `svn` commands; its `.svn` metadata was intentionally removed after import.
- Commit identity is set repo-local: `Johann Quintero <jquinterocortes@outlook.com>`.
- Working directory on this machine: `D:\Work\portals\Johann Quintero\iTunesMatchTagger`. Keep everything here; this is a personal project, not a company (Port@ls) one.

## Layout

| Path | What it is |
|---|---|
| `src/itunes-match-tagger/` | Pristine upstream snapshot (SVN r6, 2012). **Frozen** — commit fixes there only to correct import mistakes. |
| `src/v2/` | Modern rewrite (.NET 10 WinForms). All new work happens here. |

## v1 legacy (`src/itunes-match-tagger`) — read before touching

- Old-style non-SDK csproj, .NET Framework 4.8, **x86 only**. Build with Visual Studio or .NET Framework MSBuild: `msbuild src\itunes-match-tagger\trunk\iTunesMatchTagger.sln`. `dotnet build` fails with **MSB4803** (`ResolveComReference` unsupported on .NET Core MSBuild).
- The `iTunesLib` COM reference resolves from the registry at build time → iTunes for Windows must be installed on the build machine.
- `TagTracks.cs` / `TagTracks.resx` are **dead code**: not listed in the csproj and they reference members that no longer exist (`iTunesHelper.FindTags`, `Track.Id3Tag`). Do not try to compile or "fix" them.
- Two `Settings` classes exist. The live one is the root `Settings.Designer.cs` (namespace `iTunesMatchTagger`, used as `Settings.Default.*`). `Properties\Settings.Designer.cs` compiles but is unused.
- `MatchedKindAsString` user setting is a localized list of "Matched AAC audio file" in many languages; `Track.IsMatched()` compares `IITTrack.KindAsString` against it.
- `Track.GetTrackId()` parses the iTunes catalog ID from raw bytes: finds the ASCII marker `"song"` in the first 1024 bytes of the file, then reads the next 4 bytes as big-endian int32.
- Field mapping lives in `MainForm.InitUpdateOptions()`: `LookupMember` = iTunes Search API JSON field, `ITunesComMember` = iTunes COM property (e.g. `trackName`→`Name`, `collectionName`→`Album`).
- Lookup URL: `http://itunes.apple.com/lookup?id={id}&country={cc}` via `WebClient` (synchronous, one raw `Thread` per track in v1).

## v2 (`src/v2`)

- .NET 10, SDK-style, three projects in `src/v2/iTunesMatchTaggerV2.slnx`
  (**note the `.slnx` extension** - the .NET 10 SDK's XML solution format):
  ```
  dotnet build src/v2/iTunesMatchTaggerV2.slnx
  dotnet test  src/v2/iTunesMatchTaggerV2.slnx
  ```
- `src/iTunesMatchTagger.Core` (net10.0): `Lookup/` Search API client + DTO
  + store countries, `Tracks/` TrackIdReader + `ITaggableTrack`
  (`ComTrack` COM-backed, `FileTrack` TagLib#-backed), `Fields/` the
  `TrackField` catalog, `ITunes/` the dynamic COM client, `Settings/` JSON
  user settings.
- `src/iTunesMatchTagger.App` (net10.0-windows, WinForms): `MainForm.cs`
  builds the UI **in code** - there are no Designer files; `TrackRow` /
  `OptionRow` are the grid row models.
- `tests/iTunesMatchTagger.Core.Tests` (xUnit): TrackIdReader with
  synthetic bytes, Search client with a fake `HttpMessageHandler`, field
  map coverage. Keep Core logic pure so it stays testable without iTunes
  or real audio files.
- **No COMReference anywhere.** iTunes COM is accessed with `dynamic` late
  binding via `Type.GetTypeFromProgID("iTunes.Application")` — iTunes is
  needed at *runtime* only, never to build.
- iTunes COM collections are 1-based (`collection[i]` for `i` in `1..Count`).
- `TrackField` (in Core) maps one field across all three back ends (Search
  API JSON, iTunes COM, TagLib# Tag). Add new taggable fields there - never
  with string-based reflection.
- UI-thread discipline: COM access and `TrackRow` mutations happen only on
  the UI thread; background lookups report through `IProgress<T>`. Do not
  raise `TrackRow.PropertyChanged` from worker threads (DataGridView is
  not thread-safe).
- The exe must start without iTunes installed (COM is lazy); smoke-test by
  launching it and killing it after a few seconds.
- App settings: JSON at `%AppData%\iTunesMatchTagger\settings.json`
  (`AppSettings`), not app.config.
- iTunes Search API: `/lookup` only accepts `id=`/`amgArtistId=`; text
  search is the separate `/search?term=` endpoint. An embedded Track ID
  dies when Apple delists an album — `/lookup` then returns
  `resultCount: 0` in every storefront (v1 had the same behavior); v2
  falls back to `/search` with the current tags, which also finds albums
  re-released under a new ID.
- Runtime prerequisites to verify the full workflow: Windows + .NET 10
  Desktop Runtime + iTunes running with iTunes Match tracks selected.

## Conventions

- Public repo: English for code, comments, commits, and docs. No company references.
- Commits: short imperative subject + body when context matters; keep the upstream-provenance style of the first commit for anything related to the import.
