using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kmd.MarkdownReader
{
    /// <summary>How an image reference is allowed to be loaded.</summary>
    public enum ImageSourceKind
    {
        /// <summary>Empty, or a scheme we refuse to load as an image (data:, javascript:, file:, ...).</summary>
        Invalid,

        /// <summary>"search:name" — looked up in the AssetDatabase. Always allowed (in-project).</summary>
        Asset,

        /// <summary>http(s) — gated behind the remote opt-in preference.</summary>
        Remote,

        /// <summary>A relative or absolute on-disk path. Allowed in-tree; out-of-tree is gated.</summary>
        Local,
    }

    /// <summary>
    /// Decides whether a Markdown image reference may be loaded, mirroring the link
    /// <see cref="UrlPolicy"/> so that rendering a document does not silently perform
    /// network requests or read arbitrary files. Remote (http/https) images and local
    /// paths outside the project / document directory require an explicit opt-in
    /// (Preferences ▸ Kmd Markdown ▸ Allow external images).
    /// </summary>
    public static class ImagePolicy
    {
        public const string AllowExternalPrefKey = "Kmd.MarkdownReader.AllowExternalImages";

        /// <summary>
        /// When true, remote http(s) images and absolute local paths outside the
        /// project / document directory are permitted. Off by default.
        /// </summary>
        public static bool AllowExternalImages
        {
            get => EditorPrefs.GetBool(AllowExternalPrefKey, false);
            set => EditorPrefs.SetBool(AllowExternalPrefKey, value);
        }

        public static ImageSourceKind Classify(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ImageSourceKind.Invalid;
            }

            url = url.Trim();

            if (url.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            {
                return ImageSourceKind.Asset;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return ImageSourceKind.Remote;
            }

            var scheme = SchemeOf(url);
            if (scheme == null || scheme.Length == 1)
            {
                // No scheme -> relative/rooted path; a single-letter "scheme" is a
                // Windows drive letter (C:\...). Both are on-disk paths.
                return ImageSourceKind.Local;
            }

            // Any other explicit scheme (data:, javascript:, file:, ftp:, ...) is not a
            // loadable image source here.
            return ImageSourceKind.Invalid;
        }

        /// <summary>
        /// True if a resolved local <paramref name="fullPath"/> may be read without the
        /// external opt-in: it lives under the project root or under the document's own
        /// directory. Anything else is treated as external.
        /// </summary>
        public static bool IsAllowedLocalPath(string fullPath, string baseDirectory)
        {
            if (AllowExternalImages)
            {
                return true;
            }

            // Path.GetDirectoryName(Application.dataPath) is the folder that contains
            // Assets/ and Packages/ — i.e. the Unity project root.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (IsUnder(fullPath, projectRoot))
            {
                return true;
            }

            return !string.IsNullOrEmpty(baseDirectory) && IsUnder(fullPath, baseDirectory);
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
            {
                return false;
            }

            string full, r;
            try
            {
                full = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
                r = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/');
            }
            catch (Exception)
            {
                return false;
            }

            return string.Equals(full, r, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase);
        }

        // Lowercased scheme if the text begins with "name:" (letter then letters/digits/
        // +/-/.), else null. Mirrors UrlPolicy.GetScheme.
        private static string SchemeOf(string url)
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
                var isTail = (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.';
                if (i == 0 ? !isLetter : (!isLetter && !isTail))
                {
                    return null;
                }
            }

            return url.Substring(0, colon).ToLowerInvariant();
        }
    }
}
