using System;
using System.IO;
using System.IO.Compression;
using AowEmailWrapper.ASG;
using AowEmailWrapper.Games;
using AowEmailWrapper.Helpers;
using MimeKit;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// An attachment can be as large as the sender likes and can inflate to far more than its
    /// size, so both the decode and the inflate must stop at a ceiling instead of taking all memory.
    /// </summary>
    public class DecompressionLimitTests
    {
        private const int OneMegabyte = 1024 * 1024;

        //The "CFS" marker that introduces the compressed section of an AoW1 save
        private static readonly byte[] CompressedSectionSignature = { 0x43, 0x46, 0x53, 0x00, 0x02 };

        [Fact]
        public void Bounded_stream_refuses_to_grow_past_its_limit()
        {
            using (BoundedMemoryStream stream = new BoundedMemoryStream(10))
            {
                stream.Write(new byte[10], 0, 10);
                Assert.Equal(10, stream.Length);

                Assert.Throws<InvalidDataException>(() => stream.WriteByte(1));
                Assert.Throws<InvalidDataException>(() => stream.Write(new byte[1], 0, 1));
                Assert.Throws<InvalidDataException>(() => stream.SetLength(11));
                Assert.Equal(10, stream.Length);
            }
        }

        [Fact]
        public void Inflating_a_normal_size_section_still_works()
        {
            byte[] compressed = ZlibOf(OneMegabyte);

            using (BinaryReader reader = new BinaryReader(new MemoryStream(compressed)))
            {
                DataCompressor section = new DataCompressor(reader, true);
                section.Compressed = false;
                Assert.Equal(OneMegabyte, section.Data.Length);
            }
        }

        [Fact]
        public void Inflating_a_bomb_stops_at_the_ceiling()
        {
            //A few hundred kilobytes that would inflate to well past the limit
            byte[] bomb = ZlibOf(DataCompressor.MaxDecompressedBytes + 8 * OneMegabyte);
            Assert.True(bomb.Length < OneMegabyte, "the compressed bomb should be small");

            using (BinaryReader reader = new BinaryReader(new MemoryStream(bomb)))
            {
                DataCompressor section = new DataCompressor(reader, true);
                Assert.Throws<InvalidDataException>(() => section.Compressed = false);
            }
        }

        [Fact]
        public void A_bomb_dressed_as_a_save_game_is_parsed_as_unknown_without_crashing()
        {
            byte[] bomb = ZlibOf(DataCompressor.MaxDecompressedBytes + 8 * OneMegabyte);
            byte[] fake = new byte[CompressedSectionSignature.Length + bomb.Length];
            Buffer.BlockCopy(CompressedSectionSignature, 0, fake, 0, CompressedSectionSignature.Length);
            Buffer.BlockCopy(bomb, 0, fake, CompressedSectionSignature.Length, bomb.Length);

            using (ASGFileInfo info = new ASGFileInfo("Bomb.asg", fake))
            {
                Assert.Equal(AowGameType.Unknown, info.GameType);
                Assert.Null(info.GameTitle);
                Assert.Equal("Bomb.asg", info.FileNameTrue);
            }
        }

        [Fact]
        public void An_oversized_attachment_is_skipped_rather_than_decoded()
        {
            byte[] tooBig = new byte[MailHelper.MaxAttachmentBytes + 1];

            using (MemoryStream content = new MemoryStream(tooBig))
            {
                MimePart part = new MimePart("application", "octet-stream")
                {
                    Content = new MimeContent(content),
                    FileName = "Huge.asg"
                };

                Assert.Empty(MailHelper.GetAttachmentBytes(part));
            }
        }

        [Fact]
        public void An_attachment_within_the_limit_is_decoded_in_full()
        {
            byte[] payload = new byte[OneMegabyte];
            new Random(7).NextBytes(payload);

            using (MemoryStream content = new MemoryStream(payload))
            {
                MimePart part = new MimePart("application", "octet-stream")
                {
                    Content = new MimeContent(content),
                    FileName = "Fine.asg"
                };

                Assert.Equal(payload, MailHelper.GetAttachmentBytes(part));
            }
        }

        /// <summary>zlib data that inflates to the given number of zero bytes.</summary>
        private static byte[] ZlibOf(long inflatedLength)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (ZLibStream deflate = new ZLibStream(output, CompressionLevel.Optimal, true))
                {
                    byte[] zeros = new byte[OneMegabyte];
                    long remaining = inflatedLength;
                    while (remaining > 0)
                    {
                        int chunk = (int)Math.Min(zeros.Length, remaining);
                        deflate.Write(zeros, 0, chunk);
                        remaining -= chunk;
                    }
                }
                return output.ToArray();
            }
        }
    }
}
