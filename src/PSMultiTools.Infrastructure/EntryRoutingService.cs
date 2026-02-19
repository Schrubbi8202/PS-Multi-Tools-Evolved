using PSMultiTools.Core;

namespace PSMultiTools.Infrastructure;

public sealed class EntryRoutingService : IEntryRoutingService
{
    public EntryRouteResultDto Resolve(string path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new EntryRouteResultDto("/", "home", string.Empty);
        }

        var extension = Path.GetExtension(normalized).ToLowerInvariant();
        var fileName = Path.GetFileName(normalized).ToLowerInvariant();

        return extension switch
        {
            ".pkg" => new EntryRouteResultDto("/pkg/info", "pkg-info", normalized),
            ".json" when fileName == "param.json" => new EntryRouteResultDto("/ps5/editors/param", "ps5-param-editor", normalized),
            ".json" when fileName == "manifest.json" => new EntryRouteResultDto("/ps5/editors/manifest", "ps5-manifest-editor", normalized),
            ".elf" or ".bin" => new EntryRouteResultDto("/ps5/sender", "ps5-payload-sender", normalized),
            _ => new EntryRouteResultDto("/", "home", normalized)
        };
    }

    private static string NormalizePath(string input)
    {
        var trimmed = input.Trim().Trim('"');
        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new Uri(trimmed).LocalPath;
            }
            catch
            {
                return trimmed;
            }
        }

        return trimmed;
    }
}
