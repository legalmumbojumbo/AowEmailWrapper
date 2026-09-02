using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Grants the current Windows account write access to game folders that carry Program Files
    /// style permissions (read-only for users). Runs icacls once, elevated, so Windows shows a
    /// single permission prompt however many folders there are.
    /// </summary>
    public static class PermissionHelper
    {
        private const string ScriptFileName = "fix-permissions.cmd";

        /// <summary>
        /// Returns true when the elevated command ran and reported success, false when the player
        /// declined the elevation prompt or icacls failed. The caller re-checks the folders anyway.
        /// </summary>
        public static bool GrantWriteAccess(IEnumerable<string> folders)
        {
            List<string> targets = (folders ?? Enumerable.Empty<string>()).Where(folder => !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)).ToList();
            if (targets.Count == 0)
            {
                return true;
            }

            string account = string.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName);
            StringBuilder script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("set RESULT=0");
            foreach (string folder in targets)
            {
                //(OI)(CI)M: modify rights on the folder, its files and its sub folders; /T applies to what exists, /C keeps going on errors
                script.AppendFormat("icacls \"{0}\" /grant \"{1}:(OI)(CI)M\" /T /C >nul", folder.TrimEnd('\\'), account);
                script.AppendLine();
                script.AppendLine("if errorlevel 1 set RESULT=1");
            }
            script.AppendLine("exit /b %RESULT%");

            string scriptPath = Path.Combine(AppDataHelper.Root.FullName, ScriptFileName);
            File.WriteAllText(scriptPath, script.ToString());

            Trace.TraceInformation("Granting {0} write access to: {1}", account, string.Join("; ", targets));

            try
            {
                ProcessStartInfo start = new ProcessStartInfo(scriptPath);
                start.Verb = "runas";
                start.UseShellExecute = true;
                start.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return false;
                    }
                    process.WaitForExit();
                    Trace.TraceInformation("icacls finished with code {0}", process.ExitCode);
                    return process.ExitCode == 0;
                }
            }
            catch (Win32Exception ex)
            {
                //The player said No to the elevation prompt
                Trace.TraceInformation("Permission fix not run: {0}", ex.Message);
                return false;
            }
            finally
            {
                try { File.Delete(scriptPath); } catch { }
            }
        }
    }
}
