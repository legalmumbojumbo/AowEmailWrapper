using System;
using System.IO;
using System.Text;
using AowEmailWrapper.ASG;
using AowEmailWrapper.Games;
using MimeKit;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// An attachment's file name is chosen by whoever sent the email, so nothing derived from it
    /// may reach outside the folder the wrapper meant to write into.
    /// </summary>
    public class AttachmentSafetyTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;

        public AttachmentSafetyTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "EmailIn");
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        [Theory]
        [InlineData("Game.asg", true)]
        [InlineData("GAME.ASG", true)]
        [InlineData("Midwinter (Dave, Fred) Upatch 1.4.asg", true)]
        [InlineData("Game.asg ", true)]
        [InlineData("tasg.bat", false)]
        [InlineData("Game.asg.exe", false)]
        [InlineData("lasagne.txt", false)]
        [InlineData("asg", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsAsg_only_accepts_the_asg_extension(string name, bool expected)
        {
            Assert.Equal(expected, ASGFileInfo.IsAsg(name));
        }

        [Theory]
        [InlineData("Game.asg", "Game.asg")]
        [InlineData(@"..\..\Startup\Game.asg", "Game.asg")]
        [InlineData("../../Startup/Game.asg", "Game.asg")]
        [InlineData(@"C:\Users\Someone\Startup\Game.asg", "Game.asg")]
        [InlineData(@"\server\share\Game.asg", "Game.asg")]
        [InlineData("Game:stream.asg", "Gamestream.asg")]
        [InlineData("Ga<me>.asg", "Game.asg")]
        [InlineData("..", null)]
        [InlineData(@"..\..\", null)]
        [InlineData("   ", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void SanitizeFileName_keeps_only_a_plain_file_name(string input, string expected)
        {
            Assert.Equal(expected, ASGFileInfo.SanitizeFileName(input));
        }

        [Fact]
        public void GetPathInside_never_leaves_the_folder()
        {
            string inside = ASGFileInfo.GetPathInside(_folder, "Game.asg");
            Assert.Equal(Path.Combine(_folder, "Game.asg"), inside);

            Assert.Equal(Path.Combine(_folder, "Game.asg"), ASGFileInfo.GetPathInside(_folder, @"..\..\..\Game.asg"));
            Assert.Equal(Path.Combine(_folder, "Game.asg"), ASGFileInfo.GetPathInside(_folder, @"C:\Windows\Game.asg"));
            Assert.Null(ASGFileInfo.GetPathInside(_folder, ".."));
            Assert.Null(ASGFileInfo.GetPathInside(_folder, null));
            Assert.Null(ASGFileInfo.GetPathInside(null, "Game.asg"));
        }

        [Fact]
        public void Unrecognised_attachment_with_a_traversal_name_is_saved_inside_the_folder()
        {
            //Not a save game at all: the parser leaves it as Unknown, so the sender's name would be used as-is
            byte[] script = Encoding.ASCII.GetBytes("@echo off\r\necho owned\r\n");
            string outside = Path.Combine(_root, "Startup");
            Directory.CreateDirectory(outside);

            string attack = @"..\Startup\lasg.cmd.asg";
            using (ASGFileInfo info = new ASGFileInfo(attack, script))
            {
                Assert.True(info.IsValid);
                Assert.Equal(AowGameType.Unknown, info.GameType);
                Assert.Equal("lasg.cmd.asg", info.FileNameTrue);
                Assert.True(info.SaveToFolder(_folder));
            }

            Assert.Empty(Directory.GetFiles(outside));
            Assert.Equal(script, File.ReadAllBytes(Path.Combine(_folder, "lasg.cmd.asg")));
        }

        [Fact]
        public void Rooted_attachment_name_cannot_pick_the_destination()
        {
            string elsewhere = Path.Combine(_root, "Elsewhere");
            Directory.CreateDirectory(elsewhere);
            string attack = Path.Combine(elsewhere, "Game.asg");

            using (ASGFileInfo info = new ASGFileInfo(attack, new byte[] { 1, 2, 3 }))
            {
                Assert.True(info.SaveToFolder(_folder));
            }

            Assert.False(File.Exists(attack));
            Assert.True(File.Exists(Path.Combine(_folder, "Game.asg")));
        }

        [Fact]
        public void Attachment_name_with_nothing_usable_is_refused()
        {
            using (ASGFileInfo info = new ASGFileInfo(@"..\..\", new byte[] { 1, 2, 3 }))
            {
                Assert.False(info.SaveToFolder(_folder));
            }

            Assert.Empty(Directory.GetFiles(_folder));
            Assert.Empty(Directory.GetFiles(_root));
        }

        [Fact]
        public void CopyToEmailOut_keeps_the_file_inside_the_game_folder()
        {
            string gameFolder = Path.Combine(_root, "Game");
            Directory.CreateDirectory(gameFolder);
            File.WriteAllBytes(Path.Combine(gameFolder, AowGame.Aow1ExeName), new byte[] { 1 });
            Directory.CreateDirectory(Path.Combine(gameFolder, "EmailOut"));
            AowGame game = new AowGame(AowGameType.Aow1, gameFolder, InstallSource.Manual);
            Assert.True(game.IsInstalled);
            Assert.Equal(Path.Combine(gameFolder, "EmailOut"), game.EmailOut.FullName);

            AowGameManager manager = new AowGameManager(_folder, new[] { game }, null);

            MimePart attachment = new MimePart("application", "octet-stream")
            {
                Content = new MimeContent(new MemoryStream(new byte[] { 9, 9, 9 })),
                FileName = @"..\..\..\Escaped.asg"
            };

            manager.CopyToEmailOut(attachment, game);

            Assert.True(File.Exists(Path.Combine(game.EmailOut.FullName, "Escaped.asg")));
            Assert.False(File.Exists(Path.Combine(_root, "Escaped.asg")));
            Assert.False(File.Exists(Path.Combine(gameFolder, "Escaped.asg")));
        }
    }
}
