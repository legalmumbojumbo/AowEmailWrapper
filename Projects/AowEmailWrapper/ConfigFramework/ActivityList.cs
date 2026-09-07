using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AowEmailWrapper.ASG;
using AowEmailWrapper.Games;

namespace AowEmailWrapper.ConfigFramework
{
    [XmlRoot("activities")]
    public class ActivityList
    {
        private List<Activity> _activities;
        private const int MaxActivities = 100;

        [XmlElement("activity")]
        public List<Activity> Activities
        {
            get 
            {
                //Only keep 100 Activities, let them drop off the bottom after that
                if (_activities.Count > MaxActivities)
                {
                    _activities.Sort((a, b) => long.Parse(b.DateTicks).CompareTo(long.Parse(a.DateTicks)));
                    _activities.RemoveAll(item => long.Parse(item.DateTicks) < long.Parse(_activities[MaxActivities - 1].DateTicks));
                }
                return _activities;
            }
            set { _activities = value; }
        }

        public ActivityList()
        {
            _activities = new List<Activity>();
        }

        public Activity GetLastActivityByFileName(string fileName)
        {
            Activity returnVal = null;

            if (_activities != null)
            {
                List<Activity> fileNameMatches = _activities.FindAll(item =>
                    item.FileName.Contains(ASGFileInfo.SafeSearchFileName(fileName)) && ASGFileInfo.IsAsg(item.FileName));

                if (fileNameMatches != null && fileNameMatches.Count > 0)
                {
                    returnVal = fileNameMatches.Find(item => item.DateTicks.Equals(fileNameMatches.Max(maxTicks => maxTicks.DateTicks)));
                }
            }

            return returnVal;
        }

        public const char AddressSeparator = ';';

        private List<string> _contacts = new List<string>();

        /// <summary>
        /// Every address a turn has come from or gone to. Kept apart from the activities, which are
        /// capped at 100, so an opponent from an old game is still known.
        /// </summary>
        [XmlArray("contacts")]
        [XmlArrayItem("address")]
        public List<string> Contacts
        {
            get { return _contacts; }
            set { _contacts = value ?? new List<string>(); }
        }

        /// <summary>True once the mailbox has been looked through for opponents from before the contact list existed.</summary>
        [XmlAttribute("history_imported")]
        public bool HistoryImported { get; set; }

        public bool ShouldSerializeHistoryImported()
        {
            return HistoryImported;
        }

        /// <summary>
        /// True when a turn has come from this address before, or one has been sent to it.
        /// </summary>
        public bool IsKnownAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            string wanted = address.Trim();

            if (_contacts.Any(contact => string.Equals(contact, wanted, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (_activities != null)
            {
                foreach (Activity activity in _activities)
                {
                    if (ContainsAddress(activity.Sender, wanted) || ContainsAddress(activity.Recipients, wanted))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Adds an address to the contacts; returns true when it was not there yet.</summary>
        public bool AddContact(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            string trimmed = address.Trim();
            if (_contacts.Any(contact => string.Equals(contact, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _contacts.Add(trimmed);
            return true;
        }

        /// <summary>Adds every address in a ';' separated list, or a sequence of them; returns how many were new.</summary>
        public int AddContacts(IEnumerable<string> addresses)
        {
            int added = 0;
            if (addresses != null)
            {
                foreach (string entry in addresses)
                {
                    foreach (string address in (entry ?? string.Empty).Split(AddressSeparator))
                    {
                        if (AddContact(address))
                        {
                            added++;
                        }
                    }
                }
            }
            return added;
        }

        private static bool ContainsAddress(string list, string wanted)
        {
            if (string.IsNullOrEmpty(list))
            {
                return false;
            }
            foreach (string entry in list.Split(AddressSeparator))
            {
                if (string.Equals(entry.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public int GetUnSentActivitiesCount()
        {
            int returnVal = 0;

            List<Activity> unSentMatches = _activities.FindAll(item => item.Status.Equals(ActivityState.Received));

            if (unSentMatches != null & unSentMatches.Count > 0)
            {
                returnVal = unSentMatches.Count;
            }

            return returnVal;
        }

        public int GetUnSentActivitiesCountByGameType(AowGameType theType)
        {
            int returnVal = 0;

            List<Activity> unSentMatches = _activities.FindAll(item =>
                item.GameType.Equals(theType) && item.Status.Equals(ActivityState.Received));

            if (unSentMatches != null & unSentMatches.Count > 0)
            {
                returnVal = unSentMatches.Count;
            }

            return returnVal;
        }

        public int GetUnknownGameTypeActivitiesCount()
        {
            List<Activity> unknownMatches = _activities.FindAll(item => item.GameType.Equals(AowGameType.Unknown));
            return unknownMatches.Count;
        }

        public List<Activity> GetRetryActivities()
        {
            return _activities.FindAll(item => item.Status.Equals(ActivityState.Pending));
        }
    }
}
