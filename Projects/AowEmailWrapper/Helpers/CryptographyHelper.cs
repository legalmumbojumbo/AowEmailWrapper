using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AowEmailWrapper.Helpers
{
    public class CryptographyHelper
    {
        /// <summary>Marks a value stored with Windows DPAPI rather than the old reversible obfuscation.</summary>
        private const string ProtectedPrefix = "dpapi:";

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AowEmailWrapper.PasswordStore.v1");

        #region Passwords (Windows DPAPI, bound to the current Windows user)

        public static string ProtectPassword(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(input), Entropy, DataProtectionScope.CurrentUser);
            return ProtectedPrefix + Convert.ToBase64String(protectedBytes);
        }

        /// <summary>
        /// Reads a stored password. Values written by versions before 2.0 used reversible obfuscation
        /// and are still understood so that existing configurations keep working.
        /// </summary>
        public static string UnprotectPassword(string stored)
        {
            if (string.IsNullOrEmpty(stored))
            {
                return string.Empty;
            }

            if (!IsProtected(stored))
            {
                return Deobfuscate(stored);
            }

            try
            {
                byte[] protectedBytes = Convert.FromBase64String(stored.Substring(ProtectedPrefix.Length));
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                //Typically the config file was copied from another Windows user or machine
                Trace.TraceError("Stored password could not be decrypted for this Windows user: " + ex.Message);
                Trace.Flush();
                return string.Empty;
            }
        }

        public static bool IsProtected(string stored)
        {
            return !string.IsNullOrEmpty(stored) && stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
        }

        #endregion

        #region Reversible obfuscation (turn logs and pre-2.0 passwords)

        public static string Obfuscate(string input)
        {
            string returnVal = string.Empty;
            if (!string.IsNullOrEmpty(input))
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(input);
                returnVal = StringHelper.ReverseString(Convert.ToBase64String(textBytes));
            }
            return returnVal;
        }

        public static string Deobfuscate(string input)
        {
            string returnVal = string.Empty;
            if (!string.IsNullOrEmpty(input))
            {
                try
                {
                    byte[] todecode = Convert.FromBase64String(StringHelper.ReverseString(input));
                    returnVal = Encoding.UTF8.GetString(todecode);
                }
                catch (FormatException ex)
                {
                    Trace.TraceError("Stored value is not in the expected format: " + ex.Message);
                    Trace.Flush();
                }
            }

            return returnVal;
        }

        public static byte[] DecodeBase64String(string input)
        {
            return Convert.FromBase64String(input.Replace("\r\n", string.Empty).Trim());
        }

        #endregion
    }
}
