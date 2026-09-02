using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using AowEmailWrapper.Games;
using AowEmailWrapper.Helpers;
using System.Xml;

namespace AowEmailWrapper.Pollers.MessageStore
{
    public class MessageStoreManager
    {
        #region Public Methods

        public static MessageStoreCollection LoadLocalMessageStore(string username, string host)
        {
            return DataManagerHelper.LoadLocalMessageStore(username, host);
        }

        public static void SaveLocalMessageStore(string username, string host, MessageStoreCollection localMessageStore)
        {
            DataManagerHelper.SaveLocalMessageStore(username, host, localMessageStore);
        }

        public static void RemoveMessagesNoLongerOnServer(ref MessageStoreCollection localMessageStore, List<long> remoteMessageStore)
        {
            RemoveMessagesNoLongerOnServer(ref localMessageStore, LongToStringList(remoteMessageStore));
        }

        public static void RemoveMessagesNoLongerOnServer(ref MessageStoreCollection localMessageStore, List<string> remoteMessageStore)
        {
            HashSet<string> remote = new HashSet<string>(remoteMessageStore);
            localMessageStore.Messages.RemoveAll(msg => !remote.Contains(msg.UID));
        }

        public static List<long> GetMessagesToCheck(MessageStoreCollection localMessageStore, List<long> remoteMessageStore)
        {
            HashSet<string> known = KnownUids(localMessageStore);
            return remoteMessageStore.FindAll(uid => !known.Contains(uid.ToString()));
        }

        public static List<string> GetMessagesToCheck(MessageStoreCollection localMessageStore, List<string> remoteMessageStore)
        {
            HashSet<string> known = KnownUids(localMessageStore);
            return remoteMessageStore.FindAll(uid => !known.Contains(uid));
        }

        #endregion

        #region Private Methods

        //Mailboxes with tens of thousands of unread messages need set lookups, not nested List.Find scans
        private static HashSet<string> KnownUids(MessageStoreCollection localMessageStore)
        {
            HashSet<string> known = new HashSet<string>();
            foreach (MessageStoreMessage msg in localMessageStore.Messages)
            {
                if (msg.UID != null)
                {
                    known.Add(msg.UID);
                }
            }
            return known;
        }

        public static List<string> LongToStringList(List<long> input)
        {
            return input.ConvertAll(new Converter<long, string>(LongToString));
        }

        private static string LongToString(long value)
        {
            return value.ToString();
        }

        #endregion
    }
}
