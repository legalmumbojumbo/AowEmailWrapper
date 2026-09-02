using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Writes the Trace output the code has always produced to a rolling log file under the
    /// Wrapper's AppData folder, so a player can send something useful when asking for help.
    /// </summary>
    public static class LogHelper
    {
        private const string LogFolderName = "Logs";
        private const string LogFileName = "wrapper.log";
        private const string PreviousLogFileName = "wrapper.previous.log";
        private const long MaxLogBytes = 2 * 1024 * 1024;

        private static bool _started;

        public static string LogFolder
        {
            get { return Path.Combine(AppDataHelper.Root.FullName, LogFolderName); }
        }

        public static string LogFile
        {
            get { return Path.Combine(LogFolder, LogFileName); }
        }

        public static string PreviousLogFile
        {
            get { return Path.Combine(LogFolder, PreviousLogFileName); }
        }

        public static void Start()
        {
            if (_started)
            {
                return;
            }
            _started = true;

            try
            {
                Directory.CreateDirectory(LogFolder);
                Rotate();

                TextWriterTraceListener listener = new TextWriterTraceListener(LogFile, "wrapperLog");
                listener.TraceOutputOptions = TraceOptions.DateTime;
                Trace.Listeners.Add(listener);
                Trace.AutoFlush = true;

                Trace.TraceInformation("Age of Wonders Email Wrapper {0} starting on {1}, .NET {2}",
                    ConfigHelper.BuildVersion, Environment.OSVersion, Environment.Version);

                Application.ThreadException += (sender, e) => Trace.TraceError("Unhandled UI exception: {0}", e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => Trace.TraceError("Unhandled exception: {0}", e.ExceptionObject);
                TaskSchedulerHook();
            }
            catch (Exception ex)
            {
                //Logging must never stop the program from starting
                Debug.WriteLine(ex.ToString());
            }
        }

        public static void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                Process.Start(new ProcessStartInfo(LogFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        private static void Rotate()
        {
            FileInfo current = new FileInfo(LogFile);
            if (current.Exists && current.Length > MaxLogBytes)
            {
                File.Delete(PreviousLogFile);
                File.Move(LogFile, PreviousLogFile);
            }
        }

        private static void TaskSchedulerHook()
        {
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Trace.TraceError("Unobserved task exception: {0}", e.Exception);
                e.SetObserved();
            };
        }
    }
}
