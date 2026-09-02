using System.Collections.Generic;
using System.Linq;
using AowEmailWrapper.Pollers.MessageStore;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class MessageStoreTests
    {
        [Fact]
        public void OnlyUnknownServerUidsNeedChecking()
        {
            MessageStoreCollection local = new MessageStoreCollection(new List<long> { 1, 2, 3 });

            List<long> toCheck = MessageStoreManager.GetMessagesToCheck(local, new List<long> { 2, 3, 4, 5 });

            Assert.Equal(new List<long> { 4, 5 }, toCheck);
        }

        [Fact]
        public void RecordsForMessagesGoneFromTheServerAreDropped()
        {
            MessageStoreCollection local = new MessageStoreCollection(new List<long> { 1, 2, 3 });

            MessageStoreManager.RemoveMessagesNoLongerOnServer(ref local, new List<long> { 2, 3 });

            Assert.Equal(new[] { "2", "3" }, local.Messages.Select(m => m.UID).ToArray());
        }

        [Fact]
        public void LargeMailboxesAreHandledInLinearTime()
        {
            //18,000 unread messages, 15,000 already recorded: the old nested scan took minutes
            List<long> server = Enumerable.Range(1, 18000).Select(i => (long)i).ToList();
            MessageStoreCollection local = new MessageStoreCollection(server.Take(15000).ToList());

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            List<long> toCheck = MessageStoreManager.GetMessagesToCheck(local, server);
            MessageStoreManager.RemoveMessagesNoLongerOnServer(ref local, server);
            watch.Stop();

            Assert.Equal(3000, toCheck.Count);
            Assert.True(watch.ElapsedMilliseconds < 2000, "took " + watch.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void StringUidsWorkTheSameWayForPop3()
        {
            MessageStoreCollection local = new MessageStoreCollection(new List<string> { "a", "b" });

            List<string> toCheck = MessageStoreManager.GetMessagesToCheck(local, new List<string> { "b", "c" });

            Assert.Equal(new List<string> { "c" }, toCheck);
        }
    }
}
