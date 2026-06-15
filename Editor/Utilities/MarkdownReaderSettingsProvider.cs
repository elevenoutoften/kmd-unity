using UnityEditor;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Exposes the Markdown reader's preferences under Preferences ▸ Kmd Markdown.
    /// Currently the only setting is the external-image opt-in: rendering a document
    /// never reaches out to the network or reads files outside the project unless the
    /// user turns this on.
    /// </summary>
    internal static class MarkdownReaderSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Preferences/Kmd Markdown", SettingsScope.User)
            {
                label = "Kmd Markdown",
                keywords = new[] { "markdown", "kmd", "image", "remote", "external" },
                activateHandler = (_, root) =>
                {
                    var title = new Label("Markdown Reader") { name = "md-settings-title" };
                    title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                    title.style.marginTop = 6;
                    title.style.marginLeft = 9;
                    title.style.marginBottom = 6;
                    root.Add(title);

                    var allowExternal = new Toggle("Allow external images")
                    {
                        value = ImagePolicy.AllowExternalImages,
                        tooltip = "Permit remote http(s) images and local image paths "
                                  + "outside the project / document directory. Off by "
                                  + "default so opening a Markdown file never performs "
                                  + "unsolicited network requests or file reads.",
                    };
                    allowExternal.style.marginLeft = 9;
                    allowExternal.RegisterValueChangedCallback(evt => ImagePolicy.AllowExternalImages = evt.newValue);
                    root.Add(allowExternal);
                },
            };
        }
    }
}
