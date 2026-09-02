using System;
using System.Diagnostics;
using System.IO;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Opens the HTML documentation. The installer puts it in a Docs folder next to the executable;
    /// a build run from a source checkout finds the same folder a few levels up.
    /// </summary>
    public static class DocsHelper
    {
        public const string QuickStartFile = "QuickStart.html";
        public const string ManualFile = "Manual.html";

        private const string DocsFolderName = "Docs";
        private const int MaxLevelsUp = 6;

        /// <summary>The full path of a document, or null when it is not installed.</summary>
        public static string Find(string fileName)
        {
            DirectoryInfo folder = new DirectoryInfo(AppContext.BaseDirectory);
            for (int level = 0; folder != null && level < MaxLevelsUp; level++, folder = folder.Parent)
            {
                string candidate = Path.Combine(folder.FullName, DocsFolderName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Opens a document in the default browser. Returns false when it is not installed.</summary>
        public static bool Open(string fileName)
        {
            string path = Find(fileName);
            if (path == null)
            {
                Trace.TraceWarning("Document {0} not found under {1}", fileName, AppContext.BaseDirectory);
                return false;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
    }
}
