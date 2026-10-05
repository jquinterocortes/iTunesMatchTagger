# iTunesMatchTagger v2 — Execution plan

Decisions locked in session (2026-10-05) with the project owner:

| Decision | Value |
|---|---|
| Location | `D:\Work\portals\Johann Quintero\iTunesMatchTagger` (personal, public GitHub) |
| VCS | git (SVN retired; `.svn` metadata deleted after import) |
| GitHub repo | `jquinterocortes/iTunesMatchTagger`, public |
| Commit identity | `jquinterocortes@outlook.com` (repo-local; global gitconfig also fixed) |
| License | GPL-3.0 (original upstream project's license, confirmed via community fork) |
| v2 UI | WinForms on .NET 10 LTS |
| v2 scope | iTunes COM mode (feature parity with v1) + standalone file-tagging mode |

## Milestones

1. **Import** — pristine upstream snapshot committed with provenance
   (author Martin Pietschmann / schirkan, Google Code project
   `itunes-match-tagger`, SVN r6, last change 2012-10-07).
2. **Docs** — README, LICENSE (GPL-3.0), AGENTS.md, this plan.
3. **Publish** — create public GitHub repo and push.
4. **v2 milestone 1 — COM feature parity**:
   - `iTunesMatchTagger.Core` (net10.0): iTunes Search API client
     (`HttpClient` + `System.Text.Json`, HTTPS), `ITunesLookupResult` DTO
     (incl. `year` ↔ `releaseDate` logic), `TrackIdReader` (binary parsing
     of the `"song"` marker, first 1024 bytes, big-endian int32),
     typed field map replacing v1's string reflection (defaults from
     `MainForm.InitUpdateOptions()`), 134 store countries,
     `ITunesComClient` via `dynamic` + ProgID (no COMReference).
   - `iTunesMatchTagger.App` (net10.0-windows, WinForms): port of MainForm
     (country list + Select All, update-options grid with manual override,
     tracks grid with row errors, log, progress), lookups with
     `Parallel.ForEachAsync` + `IProgress<T>` + `CancellationToken`.
   - `iTunesMatchTagger.Core.Tests` (xUnit): TrackIdReader with synthetic
     bytes, Search client with fake handler (0/1/N results), field map.
   - Improvement over v1: `IsMatched` = Track ID parsed from file (> 0),
     instead of the 28-language `KindAsString` list.
5. **v2 milestone 2 — standalone mode**: TagLibSharp; scan a folder of
   m4a/mp3, read current tags, lookup by embedded Track ID when present
   else by artist/album/title search, write tags directly to files.
6. **Finalize** — refresh AGENTS.md with the real v2 structure.

## v1 reference facts (extracted during analysis)

- Upstream: `itunes-match-tagger.googlecode.com/svn`, r6, schirkan,
  2012-10-07T21:51:26Z, UUID 1af9f72d-5692-3422-95a2-1f11b698649d.
- Flow: Get selected tracks (COM) → parse Track ID from file bytes →
  lookup per country (Search API) → update via COM or manual overrides.
- Default checked countries: US, GB, AU, FR, DE, CA, IT, JP.
- Lookup API: `http://itunes.apple.com/lookup?id={id}&country={cc}`;
  take first result; `resultCount == 0` → not found.
- Dead code in upstream: `TagTracks.cs`/`TagTracks.resx` (not in csproj).
- Upstream bug/quirk kept out of v2: matched detection via localized
  `KindAsString` strings.
