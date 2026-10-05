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

## Legacy (v1)

Classic non-SDK C# project targeting .NET Framework 4.8 (x86), with an
embedded iTunes COM interop reference. Build it with Visual Studio or the
.NET Framework flavor of MSBuild (not `dotnet build`); iTunes for Windows
must be installed on the build machine because the COM type library is
resolved from the registry.

## Roadmap (v2)

- [x] Import upstream snapshot with full provenance
- [ ] Core library: iTunes Search API client, binary Track-ID reader, field mapping
- [ ] WinForms app on .NET 10: same 3-step workflow, async/parallel lookups
- [ ] iTunes COM integration without build-time COM references (`dynamic` + ProgID)
- [ ] Standalone mode: scan a folder of m4a/mp3 files and write tags directly (TagLib#)
- [ ] Unit tests for all core logic

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
