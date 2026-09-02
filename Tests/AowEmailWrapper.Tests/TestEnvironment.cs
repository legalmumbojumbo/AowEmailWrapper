using System;
using System.IO;
using System.Runtime.CompilerServices;
using EricDaugherty.CSES.Common;
using EricDaugherty.CSES.SmtpServer;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Points the wrapper's AppData folder at a throwaway directory for the whole test run,
    /// so tests never touch a real player's settings, records or turn logs.
    /// </summary>
    internal static class TestEnvironment
    {
        public static readonly string RealAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string TestAppData { get; private set; }

        [ModuleInitializer]
        internal static void Initialize()
        {
            TestAppData = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TestAppData);
            Environment.SetEnvironmentVariable("APPDATA", TestAppData);
        }
    }

    /// <summary>Accepts every recipient, like the wrapper's own AnyRecipientFilter.</summary>
    internal sealed class AcceptAllRecipients : IRecipientFilter
    {
        public bool AcceptRecipient(SMTPContext context, EmailAddress recipient)
        {
            return true;
        }
    }
}
