using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    // A small "Copy" button overlaid on a fenced code block. Copies the raw code
    // text to the system clipboard and briefly shows confirmation.
    internal static class CodeBlockCopyButton
    {
        public static Button Create(string codeText)
        {
            var button = new Button { name = "md-copy-button", text = "Copy" };
            button.AddToClassList("md-copy-button");
            button.clicked += () =>
            {
                GUIUtility.systemCopyBuffer = codeText;
                button.text = "✓ Copied";
                button.schedule.Execute(() => button.text = "Copy").StartingIn(1200);
            };
            return button;
        }
    }
}
