// Partial: table rendering for MarkdownBlockRenderer.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed partial class MarkdownBlockRenderer
    {
        private static VisualElement RenderTable(in MdBlock b)
        {
            var table = new VisualElement(); table.AddToClassList("md-table");

            if (b.TableRows == null || b.TableRows.Count == 0) return table;

            // First row is the header.
            var headerRow = new VisualElement(); headerRow.AddToClassList("md-table-row");
            foreach (var cell in b.TableRows[0])
            {
                var lbl = new Label(MarkdownInline.ToRichText(cell));
                lbl.enableRichText = true;
                lbl.AddToClassList("md-th");
                headerRow.Add(lbl);
            }
            table.Add(headerRow);

            // Remaining rows are data rows.
            for (int i = 1; i < b.TableRows.Count; i++)
            {
                var row = new VisualElement(); row.AddToClassList("md-table-row");
                foreach (var cell in b.TableRows[i])
                {
                    var lbl = new Label(MarkdownInline.ToRichText(cell));
                    lbl.enableRichText = true;
                    lbl.AddToClassList("md-td");
                    row.Add(lbl);
                }
                table.Add(row);
            }

            return table;
        }
    }
}
