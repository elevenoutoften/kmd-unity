using Markdig.Extensions.Tables;
using Markdig.Renderers;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class TableBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, Table>
    {
        protected override void Write(UIMarkdownRenderer renderer, Table table)
        {
            renderer.FlushText();

            // Wrap in a horizontal ScrollView so wide tables scroll instead of
            // stretching the document.
            var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "md-table-scroll" };
            scroll.AddToClassList("md-table-scroll");
            renderer.StartBlock(scroll);

            var tableEl = new VisualElement { name = "md-table" };
            tableEl.AddToClassList("md-table");
            renderer.StartBlock(tableEl);

            foreach (var rowObj in table)
            {
                if (!(rowObj is TableRow row))
                {
                    continue;
                }

                var rowEl = new VisualElement { name = "md-table-row" };
                rowEl.AddToClassList(row.IsHeader ? "md-table-header" : "md-table-row");
                renderer.StartBlock(rowEl);

                foreach (var cellObj in row)
                {
                    if (!(cellObj is TableCell cell))
                    {
                        continue;
                    }

                    var cellEl = new VisualElement();
                    cellEl.AddToClassList(row.IsHeader ? "md-th" : "md-td");
                    renderer.StartBlock(cellEl);
                    renderer.WriteChildren(cell);
                    renderer.FinishBlock(); // cell
                }

                renderer.FinishBlock(); // row
            }

            renderer.FinishBlock(); // table
            renderer.FinishBlock(); // scroll
        }
    }
}
