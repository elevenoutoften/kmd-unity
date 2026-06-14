using System;
using System.Collections.Generic;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using UnityEngine;
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

            var grid = new List<List<VisualElement>>();

            foreach (var rowObj in table)
            {
                if (!(rowObj is TableRow row))
                {
                    continue;
                }

                var rowEl = new VisualElement();
                rowEl.AddToClassList(row.IsHeader ? "md-table-header" : "md-table-row");
                renderer.StartBlock(rowEl);

                var cells = new List<VisualElement>();
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
                    cells.Add(cellEl);
                }

                grid.Add(cells);
                renderer.FinishBlock(); // row
            }

            renderer.FinishBlock(); // table
            renderer.FinishBlock(); // scroll

            AlignColumnsWhenLaidOut(tableEl, grid);
        }

        // UI Toolkit has no table layout: each flex row sizes its own cells to
        // their content, so columns don't line up between rows. Once the table has
        // been laid out, measure the widest cell in each column and pin every cell
        // in that column to it. Runs once.
        private static void AlignColumnsWhenLaidOut(VisualElement tableEl, List<List<VisualElement>> grid)
        {
            EventCallback<GeometryChangedEvent> callback = null;
            callback = _ =>
            {
                tableEl.UnregisterCallback(callback);

                var columns = 0;
                foreach (var rowCells in grid)
                {
                    columns = Math.Max(columns, rowCells.Count);
                }

                if (columns == 0)
                {
                    return;
                }

                var widths = new float[columns];
                foreach (var rowCells in grid)
                {
                    for (var i = 0; i < rowCells.Count; i++)
                    {
                        var w = rowCells[i].resolvedStyle.width;
                        if (!float.IsNaN(w))
                        {
                            widths[i] = Mathf.Max(widths[i], w);
                        }
                    }
                }

                foreach (var rowCells in grid)
                {
                    for (var i = 0; i < rowCells.Count; i++)
                    {
                        if (widths[i] > 0f)
                        {
                            rowCells[i].style.width = widths[i];
                        }
                    }
                }
            };

            tableEl.RegisterCallback(callback);
        }
    }
}
