using System.Net;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>Thrown when a tag source HTTP call fails in a meaningful way.</summary>
public class TagSourceException : Exception
{
    public TagSourceException(string message, Exception? inner = null) : base(message, inner)
    {
    }

    public TagSourceException(string message, HttpStatusCode statusCode, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    /// <summary>HTTP status or null when the failure is not HTTP-level (e.g. parsing).</summary>
    public HttpStatusCode? StatusCode { get; }
}

/// <summary>The tag source requires credentials that are not configured.</summary>
public sealed class TagSourceAuthException : TagSourceException
{
    public TagSourceAuthException(string message) : base(message)
    {
    }
}
