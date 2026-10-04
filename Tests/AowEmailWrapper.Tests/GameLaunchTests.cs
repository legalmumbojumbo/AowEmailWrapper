using System;
using System.IO;
using System.Threading;
using AowEmailWrapper.Classes;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// The tray menu starts a game through StartedTaskWatcher. Since .NET Core a process started
    /// from a bare file name is looked up beside the Wrapper and on the PATH rather than in the
    /// working directory, so the watcher has to hand over the executable's full path.
    /// </summary>
    public class GameLaunchTests : IDisposable
    {
        private readonly string _root;

        public GameLaunchTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        [Fact]
        public void An_executable_marked_as_downloaded_starts_without_a_prompt()
        {
            //A mod's executable came out of a downloaded zip and carries the mark of the web; started through
            //the shell, Windows would put up "Open File - Security Warning" and wait, so the watcher must not
            string folder = Path.Combine(_root, "modded");
            Directory.CreateDirectory(folder);
            string exe = Path.Combine(folder, AowGame.Aow1ExeName);
            File.Copy(Path.Combine(Environment.SystemDirectory, "whoami.exe"), exe);
            File.WriteAllText(exe + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            AowGame game = new AowGame(AowGameType.Aow1, folder, InstallSource.Manual);

            using (ManualResetEvent ended = new ManualResetEvent(false))
            {
                StartedTaskWatcher watcher = new StartedTaskWatcher(game, (sender, gameType) => ended.Set());

                watcher.Start();

                Assert.True(ended.WaitOne(TimeSpan.FromSeconds(30)), "the marked executable did not start and end within 30 seconds");
            }
        }

        [Fact]
        public void The_watcher_starts_the_executable_in_the_game_folder_and_reports_when_it_ends()
        {
            //A real executable that exits at once, under the game's file name, in a folder that is not on any path
            string folder = Path.Combine(_root, "game");
            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(Environment.SystemDirectory, "whoami.exe"), Path.Combine(folder, AowGame.Aow1ExeName));
            AowGame game = new AowGame(AowGameType.Aow1, folder, InstallSource.Manual);
            Assert.True(game.IsInstalled);

            using (ManualResetEvent ended = new ManualResetEvent(false))
            {
                AowGameType reported = AowGameType.Unknown;
                StartedTaskWatcher watcher = new StartedTaskWatcher(game, (sender, gameType) => { reported = gameType; ended.Set(); });

                watcher.Start();

                Assert.True(ended.WaitOne(TimeSpan.FromSeconds(30)), "the started process did not end, or its end was not reported");
                Assert.Equal(AowGameType.Aow1, reported);
            }
        }

        [Fact]
        public void A_late_report_from_an_ended_game_does_not_free_the_slot_of_its_restart()
        {
            //The game ended and was started again from the tray before its watcher reported the end; the
            //old report must leave the new watcher in place, or the next click starts a second copy
            AowGame game = new AowGame(AowGameType.Aow1, _root, InstallSource.Manual);
            StartedTaskWatcher ended = new StartedTaskWatcher(game, null);
            StartedTaskWatcher restarted = new StartedTaskWatcher(game, null);
            StartedTaskWatcher slot = restarted;

            Assert.False(Main.ReleaseWatcher(ref slot, ended));
            Assert.Same(restarted, slot);

            Assert.False(Main.ReleaseWatcher(ref slot, null));
            Assert.Same(restarted, slot);

            Assert.True(Main.ReleaseWatcher(ref slot, restarted));
            Assert.Null(slot);
        }

        /// <summary>A copy of a game with its EmailIn folder, and Save when asked for, made before the copy looks for them.</summary>
        private AowGame Install(string name, AowGameType type, string exe, bool withSave)
        {
            string folder = Path.Combine(_root, name);
            Directory.CreateDirectory(Path.Combine(folder, "EmailIn"));
            if (withSave)
            {
                Directory.CreateDirectory(Path.Combine(folder, "Save"));
            }
            File.Copy(Path.Combine(Environment.SystemDirectory, "whoami.exe"), Path.Combine(folder, exe));
            return new AowGame(type, folder, InstallSource.Manual);
        }

        private static void WriteTurn(string folder, string fileName, DateTime written)
        {
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            File.SetLastWriteTimeUtc(path, written);
        }

        [Fact]
        public void Age_of_Wonders_1_is_started_with_the_waiting_turn_relative_to_its_folder()
        {
            //AoW.exe loads its first argument when it names an existing .asg; relative, because the game copies
            //the argument into a 256-byte buffer without checking its length
            AowGame game = Install("aow1", AowGameType.Aow1, AowGame.Aow1ExeName, false);
            WriteTurn(game.EmailIn.FullName, "Swolterwood 1v1.asg", DateTime.UtcNow);
            Activity turn = new Activity(ActivityState.Received, AowGameType.Aow1, "Swolterwood 1v1.asg", "__Swolterwood", "6");

            string save = Main.SaveToLoad(game, turn);
            System.Diagnostics.ProcessStartInfo start = StartedTaskWatcher.StartInfoFor(game, save, false);

            Assert.Equal(Path.Combine("EmailIn", "Swolterwood 1v1.asg"), save);
            Assert.Equal("\"EmailIn\\Swolterwood 1v1.asg\"", start.Arguments);
            Assert.Equal(game.ExePath, start.FileName);
            Assert.Equal(game.Root.FullName, start.WorkingDirectory);
        }

        [Fact]
        public void A_sent_turn_is_not_loaded_so_it_cannot_be_played_twice()
        {
            AowGame game = Install("sent", AowGameType.Aow1, AowGame.Aow1ExeName, false);
            WriteTurn(game.EmailIn.FullName, "Heulax 26.asg", DateTime.UtcNow);
            Activity turn = new Activity(ActivityState.Sent, AowGameType.Aow1, "Heulax 26.asg", "Spice-Marshes of Heulax", "2");

            Assert.Null(Main.SaveToLoad(game, turn));
            Assert.Null(Main.SaveToLoad(game, null));
            Assert.Equal(string.Empty, StartedTaskWatcher.StartInfoFor(game, null, false).Arguments);
        }

        [Fact]
        public void The_newest_copy_of_the_turn_in_EmailIn_or_Save_is_loaded()
        {
            //Turns are stored in EmailIn or Save as the preferences say, and the setting may have changed since
            AowGame game = Install("both", AowGameType.Aow1, AowGame.Aow1ExeName, true);
            Activity turn = new Activity(ActivityState.Received, AowGameType.Aow1, "Hamardis 2026.asg", "Hamardis Citadel", "3");
            DateTime now = DateTime.UtcNow;

            WriteTurn(game.EmailIn.FullName, "Hamardis 2026.asg", now.AddHours(-2));
            WriteTurn(game.Save.FullName, "Hamardis 2026.asg", now);
            Assert.Equal(Path.Combine("Save", "Hamardis 2026.asg"), Main.SaveToLoad(game, turn));

            WriteTurn(game.EmailIn.FullName, "Hamardis 2026.asg", now.AddHours(1));
            Assert.Equal(Path.Combine("EmailIn", "Hamardis 2026.asg"), Main.SaveToLoad(game, turn));
        }

        [Fact]
        public void A_game_that_reads_no_arguments_is_started_at_its_main_menu()
        {
            AowGame game = Install("sm", AowGameType.AowSm, AowGame.AowSmExeName, false);
            WriteTurn(game.EmailIn.FullName, "Nathyazik 2026.asg", DateTime.UtcNow);
            Activity turn = new Activity(ActivityState.Received, AowGameType.AowSm, "Nathyazik 2026.asg", "Mirrored Oasis of Nathyazik", "2");

            Assert.False(game.LoadsSaveGivenAtStart);
            Assert.Null(Main.SaveToLoad(game, turn));
        }

        [Fact]
        public void A_turn_that_is_gone_or_outside_the_game_folder_is_not_loaded()
        {
            AowGame game = Install("missing", AowGameType.Aow1, AowGame.Aow1ExeName, false);
            WriteTurn(_root, "outside.asg", DateTime.UtcNow);

            Assert.Null(Main.SaveToLoad(game, new Activity(ActivityState.Received, AowGameType.Aow1, "gone.asg", "Map", "1")));
            Assert.Null(Main.SaveToLoad(game, new Activity(ActivityState.Received, AowGameType.Aow1, @"..\..\outside.asg", "Map", "1")));
        }

        [Fact]
        public void Of_several_waiting_turns_the_one_that_has_waited_longest_is_loaded()
        {
            Activity newer = new Activity(ActivityState.Received, AowGameType.Aow1, "b.asg", "B", "4") { DateTicks = "639267520280416076" };
            Activity oldest = new Activity(ActivityState.Received, AowGameType.Aow1, "a.asg", "A", "9") { DateTicks = "639266119301015584" };
            Activity unreadable = new Activity(ActivityState.Received, AowGameType.Aow1, "c.asg", "C", "1") { DateTicks = null };

            Assert.Same(oldest, Main.LongestWaiting(new[] { newer, unreadable, oldest }));
            Assert.Null(Main.LongestWaiting(new Activity[0]));
        }
    }
}
