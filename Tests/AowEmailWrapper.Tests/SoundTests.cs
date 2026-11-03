using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class SoundTests
    {
        /// <summary>
        /// Every sound the Wrapper's App.config names ships next to the executable and is a wave file the player
        /// takes: SoundPlayer plays nothing else, so an mp3 would stay silent without a word.
        /// </summary>
        [Fact]
        public void Every_sound_in_the_config_ships_as_a_wave_file()
        {
            XDocument config = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AowEmailWrapper.dll.config"));
            var sounds = config.Descendants("appSettings").Elements("add")
                .Where(setting => ((string)setting.Attribute("key")).EndsWith("Sound", StringComparison.Ordinal))
                .ToDictionary(setting => (string)setting.Attribute("key"), setting => (string)setting.Attribute("value"));

            Assert.Equal(new[] { "NotifySound", "RestartSound", "SentSound" }, sounds.Keys.OrderBy(key => key, StringComparer.Ordinal));
            foreach (string file in sounds.Values)
            {
                string path = Path.Combine(AppContext.BaseDirectory, file);
                Assert.True(File.Exists(path), file + " is not shipped");
                Assert.True(IsPcmWave(File.ReadAllBytes(path)), file + " is not an uncompressed wave file");
            }
        }

        /// <summary>RIFF/WAVE whose format chunk says PCM, as SoundPlayer plays.</summary>
        private static bool IsPcmWave(byte[] bytes)
        {
            if (bytes.Length < 12 || Text(bytes, 0) != "RIFF" || Text(bytes, 8) != "WAVE")
            {
                return false;
            }
            for (int chunk = 12; chunk + 8 <= bytes.Length; chunk += 8 + BitConverter.ToInt32(bytes, chunk + 4) + (BitConverter.ToInt32(bytes, chunk + 4) & 1))
            {
                if (Text(bytes, chunk) == "fmt ")
                {
                    return chunk + 10 <= bytes.Length && BitConverter.ToInt16(bytes, chunk + 8) == 1;
                }
            }
            return false;
        }

        private static string Text(byte[] bytes, int offset)
        {
            return System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
        }
    }
}
