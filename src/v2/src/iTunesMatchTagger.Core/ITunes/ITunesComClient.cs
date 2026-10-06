using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.Core.ITunes;

/// <summary>
/// Talks to a running iTunes for Windows through its COM automation
/// interface using late binding (IDispatch), so that no COM reference or
/// interop assembly is needed at build time - a plain `dotnet build` works
/// on any machine. iTunes must be installed (and, in practice, running)
/// when this class is actually used.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ITunesComClient : IDisposable
{
    public const string ProgId = "iTunes.Application";

    private object? _application;

    /// <summary>Connects to (or starts) iTunes and returns its application object.</summary>
    public dynamic Application => _application ??= Activator.CreateInstance(Type.GetTypeFromProgID(ProgId, throwOnError: true)!)!;

    /// <summary>
    /// The tracks currently selected in the iTunes UI, or an empty list when
    /// nothing is selected.
    /// </summary>
    public IReadOnlyList<ComTrack> GetSelectedTracks()
    {
        var tracks = new List<ComTrack>();

        dynamic selection = Application.SelectedTracks;
        if (selection is null)
        {
            return tracks;
        }

        int count = (int)selection.Count;
        for (int i = 1; i <= count; i++) // COM collections are 1-based
        {
            tracks.Add(new ComTrack(selection[i]));
        }

        return tracks;
    }

    /// <summary>
    /// Removes a track from the iTunes library. The file on disk is NOT
    /// deleted, which is what the re-scan flow relies on.
    /// </summary>
    public void RemoveFromLibrary(ComTrack track) => track.Raw.Delete();

    /// <summary>
    /// Re-imports an audio file into the main library (the delete + re-add
    /// trick that makes Apple's match engine re-evaluate an uploaded track).
    /// Returns the freshly added library track, or null when iTunes added
    /// nothing (already present, filter rejected the kind, ...).
    /// </summary>
    public ComTrack? AddFileToLibrary(string path)
    {
        dynamic operation = Application.LibraryPlaylist.AddFile(path);
        dynamic tracks = operation.Tracks;
        int count = (int)tracks.Count;
        return count == 0 ? null : new ComTrack(tracks[1]); // 1-based
    }

    public void Dispose()
    {
        if (_application is not null)
        {
            try
            {
                Marshal.FinalReleaseComObject(_application);
            }
            catch (ArgumentException)
            {
                // not a runtime-callable COM object; nothing to release
            }

            _application = null;
        }
    }
}
