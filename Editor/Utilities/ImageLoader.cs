using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Loads Markdown image references into a UIToolkit Image element. Supports
    /// http/https (UnityWebRequestTexture), relative/absolute on-disk paths
    /// (resolved against the document directory, loaded via file://), and a
    /// "search:" prefix that looks an asset up by name in the AssetDatabase.
    /// </summary>
    public static class ImageLoader
    {
        public static void Load(Image image, string url, string baseDirectory)
        {
            if (image == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                SetError(image, "missing image url");
                return;
            }

            url = url.Trim();

            if (url.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            {
                var name = url.Substring("search:".Length);
                var tex = FindAssetTexture(name);
                if (tex != null)
                {
                    Apply(image, tex);
                }
                else
                {
                    SetError(image, "asset not found: " + name);
                }

                return;
            }

            string uri;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                uri = url;
            }
            else
            {
                var path = url;
                if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(baseDirectory))
                {
                    path = Path.Combine(baseDirectory, url);
                }

                try
                {
                    path = Path.GetFullPath(path);
                }
                catch (Exception)
                {
                    SetError(image, "bad path: " + url);
                    return;
                }

                if (!File.Exists(path))
                {
                    SetError(image, "not found: " + url);
                    return;
                }

                uri = new Uri(path).AbsoluteUri; // file:///...
            }

            var request = UnityWebRequestTexture.GetTexture(uri);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Apply(image, DownloadHandlerTexture.GetContent(request));
                }
                else
                {
                    SetError(image, request.error);
                }

                request.Dispose();
            };
        }

        private static void Apply(Image image, Texture texture)
        {
            image.image = texture;
            image.RemoveFromClassList("md-image-error");
            image.tooltip = string.Empty;
        }

        private static void SetError(Image image, string message)
        {
            image.AddToClassList("md-image-error");
            image.tooltip = "Image failed to load: " + message;
        }

        private static Texture FindAssetTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    return tex;
                }
            }

            return null;
        }
    }
}
