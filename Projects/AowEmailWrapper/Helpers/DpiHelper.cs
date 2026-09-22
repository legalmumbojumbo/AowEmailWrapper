using System;
using System.Drawing;
using System.Windows.Forms;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// The dialogs built in code are laid out in 96 dpi units, while their text is drawn at the
    /// screen's dpi. Every box around the text has to grow the same way, or on a display set to
    /// 125% or more the text is cut off.
    /// </summary>
    public static class DpiHelper
    {
        /// <summary>
        /// The screen's dpi as this process sees it, asked of the screen itself so it is right
        /// for a form that has no window handle yet, which is when these dialogs lay themselves out.
        /// </summary>
        public static int ScreenDpi
        {
            get
            {
                using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return Math.Max(96, (int)Math.Round(screen.DpiX));
                }
            }
        }

        public static int Scale(int value)
        {
            return (int)Math.Round(value * ScreenDpi / 96f);
        }

        public static Size Scale(int width, int height)
        {
            return new Size(Scale(width), Scale(height));
        }

        /// <summary>
        /// The font the text will be drawn in: the theme swaps the font in OnLoad, after the
        /// dialog has laid itself out, so measuring has to use the font the theme will pick.
        /// </summary>
        public static Font MeasureFont(Control control)
        {
            return Theme.Enabled ? Theme.BodyFont : control.Font;
        }
    }
}
