using System.Windows.Forms;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Controls;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// The Activity Log's Who has it column: the result of "Who has the turn?" kept on each game's line, apart
    /// from its status, so games asked about together each keep their own.
    /// </summary>
    [Collection(nameof(ProcessStateCollection))]
    public class ActivityLogColumnTests
    {
        private const string Bob = "bob@example.com";
        private const string Carol = "carol@example.org";

        private static Activity Sent()
        {
            return new Activity(ActivityState.Sent, AowGameType.Aow1, "Heulax 26.asg", "Spice-Marshes of Heulax", "2");
        }

        [Fact]
        public void A_claimed_turn_names_its_holder_and_a_guess_says_probably()
        {
            Activity held = Sent();
            held.Holder = Bob;
            held.LikelyHolder = Carol;
            Assert.Equal(Bob, ActivityListView.HolderLabel(held));

            Activity guessed = Sent();
            guessed.LikelyHolder = Carol;
            string label = ActivityListView.HolderLabel(guessed);
            Assert.Contains(Carol, label);
            Assert.NotEqual(Carol, label);
        }

        [Fact]
        public void The_column_is_blank_until_asked_and_for_a_turn_not_out_with_the_others()
        {
            Assert.Equal(string.Empty, ActivityListView.HolderLabel(Sent()));

            Activity received = Sent();
            received.Status = ActivityState.Received;
            received.Holder = Bob;
            Assert.Equal(string.Empty, ActivityListView.HolderLabel(received));
        }

        [Theory]
        [InlineData("7|0=Fixed;225|1=Fixed;268|3=Fixed;84", "8|0=Fixed;225|1=Fixed;268|3=Fixed;84")]
        [InlineData("7|4=Fixed;100|5=Fixed;90", "8|4=Fixed;100|6=Fixed;90")]
        [InlineData("8|5=Fixed;90", "8|5=Fixed;90")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void Widths_saved_before_the_column_came_keep_their_columns(string saved, string upgraded)
        {
            Assert.Equal(upgraded, ActivityListView.UpgradeSavedWidths(saved));
        }

        [Fact]
        public void The_widths_a_player_dragged_before_the_column_came_still_apply()
        {
            using (Form form = new Form())
            {
                ActivityListView log = new ActivityListView { Dock = DockStyle.Fill };
                form.Controls.Add(log);
                form.CreateControl();
                System.IntPtr handle = log.Handle;

                log.ColumnWidths = "7|0=Fixed;225|1=Fixed;268|3=Fixed;84";

                Assert.Equal("8|0=Fixed;225|1=Fixed;268|3=Fixed;84", log.ColumnWidths);
            }
        }
    }
}
