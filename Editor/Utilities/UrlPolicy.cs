using System;

namespace Kmd.MarkdownReader
{
    public enum UrlKind
    {
        Blocked,
        Fragment,
        External,
        Relative,
    }

    /// <summary>
    /// URL classification ported from kmd's sanitize policy. Allows http/https/
    /// mailto, in-document fragments, and relative paths; blocks javascript:,
    /// vbscript:, data:, file:, and any other/unknown scheme.
    /// </summary>
    public static class UrlPolicy
    {
        public static UrlKind Classify(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return UrlKind.Blocked;
            }

            url = url.Trim();

            if (url.StartsWith("#", StringComparison.Ordinal))
            {
                return UrlKind.Fragment;
            }

            var scheme = GetScheme(url);
            if (scheme == null)
            {
                // No scheme -> relative path (e.g. ./docs/x.md, images/y.png).
                return UrlKind.Relative;
            }

            switch (scheme)
            {
                case "http":
                case "https":
                case "mailto":
                    return UrlKind.External;
                default:
                    // javascript, vbscript, data, file, and anything else.
                    return UrlKind.Blocked;
            }
        }

        public static bool IsSafe(string url)
        {
            return Classify(url) != UrlKind.Blocked;
        }

        // Returns the lowercased scheme if the text begins with a valid URI scheme
        // ("name:"), else null. A scheme is letters/digits/+/-/. starting with a
        // letter; anything else before ':' (e.g. '/', '.') means it is not a scheme.
        private static string GetScheme(string url)
        {
            var colon = url.IndexOf(':');
            if (colon <= 0)
            {
                return null;
            }

            for (var i = 0; i < colon; i++)
            {
                var c = url[i];
                var isLetter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                var isSchemeTail = (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.';
                if (i == 0)
                {
                    if (!isLetter)
                    {
                        return null;
                    }
                }
                else if (!isLetter && !isSchemeTail)
                {
                    return null;
                }
            }

            return url.Substring(0, colon).ToLowerInvariant();
        }
    }
}
