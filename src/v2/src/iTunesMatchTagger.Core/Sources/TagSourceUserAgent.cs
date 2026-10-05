namespace iTunesMatchTagger.Core.Sources;

/// <summary>User-Agent parts used by the HTTP-based tag sources.</summary>
public sealed record TagSourceUserAgent(string Product, string Version, string Comment)
{
    public override string ToString() => $"{Product}/{Version} ({Comment})";
}
