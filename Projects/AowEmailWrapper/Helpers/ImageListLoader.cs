using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Fills the designer image lists from PNG files embedded under Resources\ImageLists\&lt;list&gt;\NN-key.png.
    /// The designer's ImageStream resources needed BinaryFormatter, which later .NET versions remove.
    /// </summary>
    public static class ImageListLoader
    {
        private const string ResourcePrefix = "AowEmailWrapper.Resources.ImageLists.";

        public static void Load(ImageList list, string listName)
        {
            Assembly assembly = typeof(ImageListLoader).Assembly;
            string prefix = ResourcePrefix + listName + ".";

            string[] names = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            list.Images.Clear();
            list.ColorDepth = ColorDepth.Depth32Bit;
            list.ImageSize = new Size(16, 16);

            foreach (string name in names)
            {
                //"NN-key.png" -> "key"
                string key = name.Substring(prefix.Length);
                key = key.Substring(0, key.Length - 4);
                int dash = key.IndexOf('-');
                if (dash >= 0)
                {
                    key = key.Substring(dash + 1);
                }

                //ImageList keeps the Image object until its handle is created, so hand it an independent
                //copy that outlives the resource stream instead of the stream-backed original
                using (Stream stream = assembly.GetManifestResourceStream(name))
                using (Image image = Image.FromStream(stream))
                {
                    list.Images.Add(key, new Bitmap(image));
                }
            }
        }
    }
}
