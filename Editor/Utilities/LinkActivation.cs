using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Routes an activated rich-text <c>&lt;link&gt;</c> target to the right action:
    /// in-document fragments scroll, external URLs open in the OS browser, relative
    /// paths open/select the target, and blocked schemes are ignored. The routing is
    /// pure (and unit-tested); see <see cref="Activate"/>.
    ///
    /// Wiring the actual click is best-effort: UI Toolkit's Pointer*LinkTagEvent types
    /// are <c>internal</c> in current Unity (6000.3.x), so <see cref="TryRegister"/>
    /// reaches them through reflection and silently degrades to "styled but inert" if
    /// they are unavailable — no regression versus not handling links at all.
    /// </summary>
    public static class LinkActivation
    {
        /// <summary>
        /// Performs the action for an activated link target. Safe to call with any
        /// string; unknown/blocked targets are ignored.
        /// </summary>
        public static void Activate(string linkId, UIMarkdownRenderer renderer)
        {
            if (string.IsNullOrEmpty(linkId))
            {
                return;
            }

            switch (UrlPolicy.Classify(linkId))
            {
                case UrlKind.Fragment:
                    renderer?.ScrollToHeading(linkId.TrimStart('#'));
                    break;

                case UrlKind.External:
                    Application.OpenURL(linkId);
                    break;

                case UrlKind.Relative:
                    OpenRelative(linkId, renderer);
                    break;

                default:
                    // Blocked scheme — ignore.
                    break;
            }
        }

        /// <summary>
        /// Best-effort: registers a link-tag click handler on <paramref name="root"/>
        /// so activated links route through <see cref="Activate"/>. Returns false
        /// (and never throws) when the internal link-tag events aren't reachable.
        /// </summary>
        public static bool TryRegister(VisualElement root, UIMarkdownRenderer renderer)
        {
            if (root == null)
            {
                return false;
            }

            try
            {
                var uiAssembly = typeof(VisualElement).Assembly;
                Type eventType = null;
                foreach (var name in new[]
                {
                    "UnityEngine.UIElements.PointerUpLinkTagEvent",
                    "UnityEngine.UIElements.PointerDownLinkTagEvent",
                })
                {
                    eventType = uiAssembly.GetType(name);
                    if (eventType != null)
                    {
                        break;
                    }
                }

                if (eventType == null)
                {
                    return false;
                }

                var linkIdProperty = eventType.GetProperty(
                    "linkID",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (linkIdProperty == null)
                {
                    return false;
                }

                // void Handle(object evt) binds to EventCallback<TLinkEvent> by relaxed
                // contravariance (TLinkEvent is reference-convertible to object).
                var forwarder = new LinkClickForwarder(renderer, linkIdProperty);
                var callbackType = typeof(EventCallback<>).MakeGenericType(eventType);
                var handle = typeof(LinkClickForwarder).GetMethod(
                    nameof(LinkClickForwarder.Handle),
                    BindingFlags.Instance | BindingFlags.Public);
                var callback = Delegate.CreateDelegate(callbackType, forwarder, handle);

                MethodInfo register = null;
                foreach (var method in typeof(VisualElement).GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (method.Name != "RegisterCallback" || !method.IsGenericMethodDefinition)
                    {
                        continue;
                    }

                    if (method.GetGenericArguments().Length != 1)
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 2 && parameters[1].ParameterType == typeof(TrickleDown))
                    {
                        register = method;
                        break;
                    }
                }

                if (register == null)
                {
                    return false;
                }

                register.MakeGenericMethod(eventType)
                    .Invoke(root, new object[] { callback, TrickleDown.NoTrickleDown });
                return true;
            }
            catch (Exception)
            {
                // Internal link-tag events aren't accessible in this Unity build.
                // Callers can fall back to one clickable Label per link.
                return false;
            }
        }

        private static void OpenRelative(string url, UIMarkdownRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var hash = url.IndexOf('#');
            var pathPart = hash >= 0 ? url.Substring(0, hash) : url;
            if (string.IsNullOrEmpty(pathPart))
            {
                return;
            }

            pathPart = Uri.UnescapeDataString(pathPart);

            string full;
            try
            {
                var baseDir = renderer.BaseDirectory;
                full = Path.GetFullPath(string.IsNullOrEmpty(baseDir)
                    ? pathPart
                    : Path.Combine(baseDir, pathPart));
            }
            catch (Exception)
            {
                return;
            }

            if (!File.Exists(full))
            {
                return;
            }

            // A '..' link can resolve outside the project / document directory. Gate it
            // through the same containment the image path uses, so a crafted relative
            // link can't read out-of-project files or hand them to the OS shell unless
            // the user opted into external content.
            if (!ImagePolicy.IsAllowedLocalPath(full, renderer.BaseDirectory))
            {
                return;
            }

            if (full.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                EditorWindow.GetWindow<MarkdownViewerWindow>().ShowFile(full);
                return;
            }

            // Non-markdown target: ping it in the Project window if it's an asset,
            // otherwise hand it to the OS — but never auto-launch an executable/script.
            var assetPath = ToProjectRelative(full);
            if (assetPath != null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    return;
                }
            }

            if (IsRiskyExecutable(full))
            {
                return;
            }

            Application.OpenURL(new Uri(full).AbsoluteUri);
        }

        private static readonly string[] RiskyExtensions =
        {
            ".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".ps1", ".psm1", ".lnk",
            ".msi", ".msp", ".js", ".jse", ".vbs", ".vbe", ".wsf", ".wsh", ".hta",
            ".reg", ".jar", ".sh", ".app", ".command",
        };

        private static bool IsRiskyExecutable(string path)
        {
            var ext = Path.GetExtension(path);
            foreach (var risky in RiskyExtensions)
            {
                if (string.Equals(ext, risky, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // Project-root-relative path ("Assets/..." or "Packages/...") for an absolute
        // path inside the project, else null.
        private static string ToProjectRelative(string fullPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            var normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            var normalizedFull = fullPath.Replace('\\', '/');
            return normalizedFull.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? normalizedFull.Substring(normalizedRoot.Length)
                : null;
        }

        private sealed class LinkClickForwarder
        {
            private readonly UIMarkdownRenderer _renderer;
            private readonly PropertyInfo _linkIdProperty;

            public LinkClickForwarder(UIMarkdownRenderer renderer, PropertyInfo linkIdProperty)
            {
                _renderer = renderer;
                _linkIdProperty = linkIdProperty;
            }

            public void Handle(object evt)
            {
                var linkId = _linkIdProperty.GetValue(evt) as string;
                Activate(linkId, _renderer);
            }
        }
    }
}
