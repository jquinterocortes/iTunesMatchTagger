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

- .NET 10, SDK-style, WinForms app + Core library + xUnit tests. Build/test with:
  ```
  dotnet build src/v2/iTunesMatchTaggerV2.sln
  dotnet test  src/v2/iTunesMatchTaggerV2.sln
  ```
- **No COMReference anywhere.** iTunes COM is accessed with `dynamic` late binding via `Type.GetTypeFromProgID("iTunes.Application")` — iTunes is needed at *runtime* only, never to build.
- iTunes COM collections are 1-based (`collection[i]` for `i` in `1..Count`).
- Track-ID binary parsing is a pure function in Core (`TrackIdReader`) — unit-test it with synthetic bytes, don't need real audio files.
- Standalone mode (no iTunes) uses TagLibSharp to read/write tags of m4a/mp3 files directly.
- Runtime prerequisites to manually verify the app: Windows + .NET 10 Desktop Runtime + iTunes running with iTunes Match tracks selected.

## Conventions

- Public repo: English for code, comments, commits, and docs. No company references.
- Commits: short imperative subject + body when context matters; keep the upstream-provenance style of the first commit for anything related to the import.
