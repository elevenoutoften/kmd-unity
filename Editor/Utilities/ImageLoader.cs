using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Loads Markdown image references into a UIToolkit Image element. Supports
    /// http/https (UnityWebRequestTexture, gated by <see cref="ImagePolicy"/>),
    /// relative/absolute on-disk paths (resolved against the document directory,
    /// loaded via file://), and a "search:" prefix that looks an asset up by name in
    /// the AssetDatabase.
    ///
    /// Downloaded textures are cached by normalized URI for the editor session, so the
    /// frequent full re-renders (live file refresh, theme change) reuse a single
    /// native texture per image instead of refetching and leaking a new one each time.
    /// </summary>
    public static class ImageLoader
    {
        private const int MaxCacheSize = 64;
        private const int MaxTexturePixels = 4096 * 4096;
        private const long MaxCachedTexturePixels = MaxTexturePixels * 2L;
        private const int TimeoutSeconds = 30;

        // Texture per normalized source key, reused across re-renders.
        private static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();

        // Failed-but-stable results keyed by normalized source. This avoids repeatedly
        // downloading/decoding an oversized image on every re-render just to reject it
        // again.
        private static readonly Dictionary<string, string> Rejected = new Dictionary<string, string>();

        // Most-recently-used keys are at the front; least-recently-used keys are
        // evicted from the back when the cache grows beyond MaxCacheSize.
        private static readonly LinkedList<string> LruKeys = new LinkedList<string>();
        private static readonly Dictionary<string, LinkedListNode<string>> LruNodes = new Dictionary<string, LinkedListNode<string>>();

        // Textures THIS loader created (downloaded via UnityWebRequest); these are
        // owned native objects and must be destroyed on clear. AssetDatabase textures
        // (the "search:" path) are borrowed and must NEVER be destroyed.
        private static readonly HashSet<Texture> Owned = new HashSet<Texture>();
        private static long CachedTexturePixels;

        // In-flight requests keyed by source key; the same image referenced twice (or
        // re-requested by a newer render before the first finishes) fetches only once.
        private static readonly Dictionary<string, List<Image>> Pending = new Dictionary<string, List<Image>>();

        // Latest composite cache key per local file uri, so a changed file evicts its
        // previous (now-stale) texture instead of leaking it for the session.
        private static readonly Dictionary<string, string> LocalKeyByUri = new Dictionary<string, string>();

        [InitializeOnLoadMethod]
        private static void Init()
        {
            // Free owned native textures before the static state is wiped, so they do
            // not linger as leaked native objects across a domain reload.
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }

        public static void Load(Image image, string url, string baseDirectory)
        {
            if (image == null)
            {
                return;
            }

            switch (ImagePolicy.Classify(url))
            {
                case ImageSourceKind.Asset:
                    LoadAsset(image, url.Trim().Substring("search:".Length));
                    return;

                case ImageSourceKind.Remote:
                    if (!ImagePolicy.AllowExternalImages)
                    {
                        SetError(image, "remote image blocked — enable in Preferences ▸ Kmd Markdown");
                        return;
                    }

                    var remote = url.Trim();
                    if (!TryApplyCached(image, remote))
                    {
                        Fetch(image, remote, remote);
                    }

                    return;

                case ImageSourceKind.Local:
                    LoadLocal(image, url.Trim(), baseDirectory);
                    return;

                default:
                    SetError(image, "blocked or invalid image url");
                    return;
            }
        }

        /// <summary>Destroys owned textures and empties the session cache.</summary>
        public static void ClearCache()
        {
            foreach (var texture in Owned)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Owned.Clear();
            Cache.Clear();
            Rejected.Clear();
            CachedTexturePixels = 0;
            LruKeys.Clear();
            LruNodes.Clear();
            Pending.Clear();
            LocalKeyByUri.Clear();
        }

        private static void LoadAsset(Image image, string name)
        {
            var key = "search:" + name;
            if (TryApplyCached(image, key))
            {
                return;
            }

            var texture = FindAssetTexture(name);
            if (texture != null)
            {
                if (PixelCount(texture) > MaxTexturePixels)
                {
                    var message = PixelBudgetMessage(texture);
                    AddRejected(key, message);
                    SetError(image, message);
                    return;
                }

                AddToCache(key, texture); // borrowed (AssetDatabase-owned) — not added to Owned
                Apply(image, texture);
            }
            else
            {
                SetError(image, "asset not found: " + name);
            }
        }

        private static void LoadLocal(Image image, string url, string baseDirectory)
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

            if (!ImagePolicy.IsAllowedLocalPath(path, baseDirectory))
            {
                SetError(image, "image outside project blocked — enable in Preferences ▸ Kmd Markdown");
                return;
            }

            if (!File.Exists(path))
            {
                SetError(image, "not found: " + url);
                return;
            }

            var uri = new Uri(path).AbsoluteUri; // file:///...

            // Fold the file's write-time + size into the cache key so editing the image
            // (same path, new bytes) misses the cache and re-fetches, instead of showing
            // the stale texture for the rest of the session. An unchanged file keeps the
            // same key, so re-renders still reuse the texture.
            long stamp = 0, length = 0;
            try
            {
                var info = new FileInfo(path);
                stamp = info.LastWriteTimeUtc.Ticks;
                length = info.Length;
            }
            catch (Exception)
            {
                // Couldn't stat — fall back to the bare uri key.
            }

            var cacheKey = uri + "#" + stamp + "-" + length;
            if (TryApplyCached(image, cacheKey))
            {
                return;
            }

            // The file changed since we last cached it: drop the superseded texture so
            // owned textures don't accumulate until the next domain reload.
            if (LocalKeyByUri.TryGetValue(uri, out var previousKey) && previousKey != cacheKey)
            {
                Evict(previousKey);
            }

            LocalKeyByUri[uri] = cacheKey;
            Fetch(image, uri, cacheKey);
        }

        private static bool TryApplyCached(Image image, string key)
        {
            if (Rejected.TryGetValue(key, out var message))
            {
                Touch(key);
                SetError(image, message);
                return true;
            }

            if (Cache.TryGetValue(key, out var texture))
            {
                if (texture != null)
                {
                    Touch(key);
                    Apply(image, texture);
                    return true;
                }

                Evict(key); // texture was destroyed out from under us
            }

            return false;
        }

        private static void Fetch(Image image, string requestUri, string cacheKey)
        {
            if (Pending.TryGetValue(cacheKey, out var waiters))
            {
                waiters.Add(image); // coalesce onto the in-flight request
                SetLoading(image);
                return;
            }

            Pending[cacheKey] = new List<Image> { image };
            SetLoading(image);

            var request = UnityWebRequestTexture.GetTexture(requestUri);
            request.timeout = TimeoutSeconds;
            request.SendWebRequest().completed += _ =>
            {
                var targets = Pending.TryGetValue(cacheKey, out var list) ? list : null;
                Pending.Remove(cacheKey);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);
                    if (texture != null)
                    {
                        var pixels = (long)texture.width * texture.height;
                        if (pixels <= MaxTexturePixels)
                        {
                            AddToCache(cacheKey, texture);
                            Owned.Add(texture);
                            ApplyToAttached(targets, texture);
                        }
                        else
                        {
                            var message = PixelBudgetMessage(texture);
                            AddRejected(cacheKey, message);
                            UnityEngine.Object.DestroyImmediate(texture);
                            SetErrorOnAttached(targets, message);
                        }
                    }
                    else
                    {
                        SetErrorOnAttached(targets, "download did not produce a texture");
                    }
                }
                else
                {
                    SetErrorOnAttached(targets, request.error);
                }

                request.Dispose();
            };
        }

        // Drop a single cache entry, destroying its texture if we own it.
        private static void Evict(string key)
        {
            if (Cache.TryGetValue(key, out var texture))
            {
                Cache.Remove(key);
                CachedTexturePixels -= PixelCount(texture);
                if (texture != null && Owned.Remove(texture))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Rejected.Remove(key);

            if (LruNodes.TryGetValue(key, out var node))
            {
                LruKeys.Remove(node);
                LruNodes.Remove(key);
            }
        }

        private static void AddToCache(string key, Texture texture)
        {
            Cache.TryGetValue(key, out var previous);
            if (previous != null && previous != texture)
            {
                CachedTexturePixels -= PixelCount(previous);
                if (Owned.Remove(previous))
                {
                    UnityEngine.Object.DestroyImmediate(previous);
                }
            }

            Rejected.Remove(key);
            Cache[key] = texture;
            if (previous != texture)
            {
                CachedTexturePixels += PixelCount(texture);
            }

            Touch(key);

            TrimRememberedResults();
        }

        private static void AddRejected(string key, string message)
        {
            if (Cache.TryGetValue(key, out var texture))
            {
                Cache.Remove(key);
                CachedTexturePixels -= PixelCount(texture);
                if (texture != null && Owned.Remove(texture))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Rejected[key] = message;
            Touch(key);
            TrimRememberedResults();
        }

        private static void TrimRememberedResults()
        {
            while ((Cache.Count + Rejected.Count > MaxCacheSize
                    || CachedTexturePixels > MaxCachedTexturePixels)
                && LruKeys.Last != null)
            {
                Evict(LruKeys.Last.Value);
            }
        }

        private static long PixelCount(Texture texture)
        {
            return texture == null ? 0 : (long)texture.width * texture.height;
        }

        private static string PixelBudgetMessage(Texture texture)
        {
            return "image exceeds pixel budget: "
                + texture.width + "x" + texture.height
                + " > " + MaxTexturePixels + " pixels";
        }

        private static void Touch(string key)
        {
            if (LruNodes.TryGetValue(key, out var node))
            {
                LruKeys.Remove(node);
                LruKeys.AddFirst(node);
                return;
            }

            LruNodes[key] = LruKeys.AddFirst(key);
        }

        private static void ApplyToAttached(List<Image> targets, Texture texture)
        {
            if (targets == null || texture == null)
            {
                return;
            }

            foreach (var image in targets)
            {
                // Skip elements dropped by a newer render — applying to a detached
                // element is wasted work and keeps a dead element referenced.
                if (image != null && image.panel != null)
                {
                    Apply(image, texture);
                }
            }
        }

        private static void SetErrorOnAttached(List<Image> targets, string message)
        {
            if (targets == null)
            {
                return;
            }

            foreach (var image in targets)
            {
                if (image != null && image.panel != null)
                {
                    SetError(image, message);
                }
            }
        }

        private static void Apply(Image image, Texture texture)
        {
            image.image = texture;
            image.RemoveFromClassList("md-image-loading");
            image.RemoveFromClassList("md-image-error");
            image.tooltip = string.Empty;
        }

        private static void SetLoading(Image image)
        {
            image.AddToClassList("md-image-loading");
        }

        private static void SetError(Image image, string message)
        {
            image.image = null;
            image.RemoveFromClassList("md-image-loading");
            image.AddToClassList("md-image-error");
            image.tooltip = "Image failed to load: " + message;
        }

        private static Texture FindAssetTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }
    }
}
