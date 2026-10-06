# iTunesMatchTagger

A Windows desktop utility that repairs the metadata of **iTunes Match** tracks.
It reads the hidden catalog ID embedded in each matched audio file, looks the
track up in Apple's public **iTunes Search API** across multiple storefront
countries, and writes the authoritative tags (name, artist, album, year,
genre, disc/track numbers) back to your library.

Based on **iTunes Match Tagger** by Martin Pietschmann ([schirkan]),
originally published on Google Code
([project archive](https://code.google.com/archive/p/itunes-match-tagger/)).

## How it works

1. **Get selected tracks** — connect to the running iTunes (COM automation)
   and read the current tags of the tracks you selected in iTunes.
2. **Lookup tracks** — query `https://itunes.apple.com/lookup` for each track
   ID across the iTunes Store countries you selected (US, GB, DE, …).
3. **Update tracks** — write the found metadata back to the tracks in iTunes,
   or apply manual per-field override values.

Only works with **matched and downloaded** AAC files: the track ID is parsed
from the `"song"` marker in the first 1 KB of the file itself.

## Repository layout

| Path | Contents |
|---|---|
| `src/itunes-match-tagger/` | Pristine import of the original project (upstream SVN **r6**, last change 2012-10-07). Legacy, kept as baseline and reference. |
| `src/v2/` | Modern rewrite: .NET 10, WinForms, async I/O, plus a standalone mode that tags audio files directly without iTunes. |

## v2

```
dotnet build src/v2/iTunesMatchTaggerV2.slnx
dotnet test  src/v2/iTunesMatchTaggerV2.slnx
dotnet run --project src/v2/src/iTunesMatchTagger.App
```

- **iTunes mode**: select tracks in iTunes → *1. Get selected tracks* →
  *2. Lookup tracks* → *3. Update tracks*.
- **Standalone mode**: *Load folder...* with m4a/mp3 files — tags are read
  and written directly with TagLib#; files with an embedded iTunes Match
  ID are looked up by ID, the rest by artist/title search.

## Legacy (v1)

Classic non-SDK C# project targeting .NET Framework 4.8 (x86), with an
embedded iTunes COM interop reference. Build it with Visual Studio or the
.NET Framework flavor of MSBuild (not `dotnet build`); iTunes for Windows
must be installed on the build machine because the COM type library is
resolved from the registry.

## Roadmap (v2)

- [x] Import upstream snapshot with full provenance
- [x] Core library: iTunes Search API client, binary Track-ID reader, typed field mapping
- [x] WinForms app on .NET 10: same 3-step workflow, async/parallel lookups
- [x] iTunes COM integration without build-time COM references (`dynamic` + ProgID)
- [x] Standalone mode: scan a folder of m4a/mp3 files and write tags directly (TagLib#)
- [x] Album artwork: downloaded from the Search API (600x600) and embedded via iTunes COM or TagLib#
- [x] Unit tests for all core logic (29 passing)
- [x] Additional tag sources: MusicBrainz, Discogs and Deezer adapters with a
      normalized `TagCandidate`; automatic fallback (Apple first), candidate
      picker and per-field "Use" checkboxes in the detail panel
- [x] Force re-scan: delete + re-add tracks from the library (files kept) so
      Apple's match engine re-evaluates uploaded tracks
- [ ] Candidate quality: MusicBrainz track/disc numbers (needs release lookup), genre via `/recording/{mbid}`
- [ ] CI workflow (build + test on windows-latest)
- [ ] Release packaging (self-contained single-file exe)

## Credits & provenance

- Original **iTunes Match Tagger** © Martin Pietschmann (schirkan), 2012,
  Google Code project `itunes-match-tagger`, imported here from SVN revision
  r6 (repo `itunes-match-tagger.googlecode.com/svn`, UUID
  `1af9f72d-5692-3422-95a2-1f11b698649d`).
- Continued as a personal open-source project by
  [Johann Quintero](https://github.com/jquinterocortes), 2026.

## License

[GPL-3.0](LICENSE) — the same license as the original project. If you build
on this code, your version must remain free and open under the GPL too.
