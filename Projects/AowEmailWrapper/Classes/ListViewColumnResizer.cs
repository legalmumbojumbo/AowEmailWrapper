using System;
using System.Windows.Forms;
using AowEmailWrapper.Helpers;

namespace AowEmailWrapper.Classes
{
    public enum ColumnHeaderResizeStyle
    {
        None,
        ColumnContent,
        HeaderSize,
        ContentHeaderMax,
        Fixed,
        Fill
    }

    public static class ListViewColumnResizer
    {
        private const char SplitChar = ';';
        private const int MinimumFillWidth = 60;

        public static void ResizeColumns(ListView theListView)
        {
            if (theListView.Columns.Count > 0 &&
                theListView.Items.Count > 0)
            {
                ColumnHeader fillColumn = null;
                int totalColumnWidth = 0;

                foreach (ColumnHeader column in theListView.Columns)
                {
                    if (column.Tag != null)
                    {
                        string style = column.Tag.ToString();
                        string value = string.Empty;

                        if (style.Contains(SplitChar))
                        {
                            string[] split = style.Split(SplitChar);
                            style = split[0];
                            value = split[1];
                        }

                        ColumnHeaderResizeStyle theStyle = ConfigHelper.ParseEnumString<ColumnHeaderResizeStyle>(style);

                        switch (theStyle)
                        {
                            case ColumnHeaderResizeStyle.ColumnContent:
                                column.Width = ContentWidth(theListView, column);
                                totalColumnWidth += column.Width;
                                break;
                            case ColumnHeaderResizeStyle.HeaderSize:
                                AutoResizeColumn(column, ColumnHeaderAutoResizeStyle.HeaderSize);
                                totalColumnWidth += column.Width;
                                break;
                            case ColumnHeaderResizeStyle.ContentHeaderMax:
                                AutoResizeColumn(column, ColumnHeaderAutoResizeStyle.HeaderSize);
                                int headerSize = column.Width;
                                int columnContentSize = ContentWidth(theListView, column);

                                column.Width = (headerSize > columnContentSize) ? headerSize : columnContentSize;
                                totalColumnWidth += column.Width;
                                break;
                            case ColumnHeaderResizeStyle.Fill:
                                fillColumn = column;
                                break;
                            case ColumnHeaderResizeStyle.Fixed:
                                int width = 0;
                                if (int.TryParse(value, out width))
                                {
                                    column.Width = width;
                                }
                                totalColumnWidth += column.Width;
                                break;
                            default:
                                totalColumnWidth += column.Width;
                                break;
                        }
                    }
                }

                if (fillColumn != null)
                {
                    //Never below a readable minimum: a negative width means "auto size" to the ListView and starts a resize loop
                    fillColumn.Width = Math.Max(MinimumFillWidth, theListView.ClientSize.Width - totalColumnWidth);
                }
            }
        }
        
        /// <summary>
        /// The width the column needs for its rows, measured with each row's own font. The ListView's own
        /// content autosize measures with the control font, which is narrower than the bold rows the
        /// accounts and activity lists use, so it came out a few pixels short and the text was cut.
        /// </summary>
        private static int ContentWidth(ListView theListView, ColumnHeader column)
        {
            const int CellPadding = 12;
            int iconWidth = column.Index == 0 && theListView.SmallImageList != null ? theListView.SmallImageList.ImageSize.Width + 4 : 0;
            int widest = 0;
            foreach (ListViewItem item in theListView.Items)
            {
                if (column.Index >= item.SubItems.Count) continue;
                ListViewItem.ListViewSubItem cell = item.SubItems[column.Index];
                System.Drawing.Font font = item.UseItemStyleForSubItems ? item.Font : cell.Font;
                int width = TextRenderer.MeasureText(cell.Text, font, System.Drawing.Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                if (width > widest) widest = width;
            }
            return widest + iconWidth + CellPadding;
        }

        private static void AutoResizeColumn(ColumnHeader theColumn, ColumnHeaderAutoResizeStyle style)
        {
            try
            {
                //This method seems to sometimes throw a random null ref exception
                theColumn.AutoResize(style);
            }
            catch { }
        }
    }
}
