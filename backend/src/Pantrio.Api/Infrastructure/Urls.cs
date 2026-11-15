namespace Pantrio.Api.Infrastructure;

public static class Urls
{
    /// <summary>True for an absolute http(s) URL. Anything else (javascript:, data:, relative) is refused before it is stored and rendered as a link.</summary>
    public static bool IsHttp(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Null means "leave unchanged"; empty or whitespace means "clear"; otherwise the trimmed URL.</summary>
    public static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
