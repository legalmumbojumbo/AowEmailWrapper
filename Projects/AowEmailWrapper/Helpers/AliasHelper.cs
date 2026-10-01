using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AowEmailWrapper.ConfigFramework;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Shows the players the player has named on the Aliases tab by those names wherever the Wrapper
    /// would show their email address. Addresses without a name are shown as they are.
    /// </summary>
    public static class AliasHelper
    {
        private static AliasList _current = new AliasList();

        /// <summary>The list in use; the Aliases tab replaces it whenever the player changes it.</summary>
        public static AliasList Current
        {
            get { return _current; }
            set { _current = value ?? new AliasList(); }
        }

        /// <summary>The name for the address, or the address itself when it has none.</summary>
        public static string Display(string address)
        {
            return Current.NameFor(address) ?? address;
        }

        /// <summary>"Bob (bob@example.com)" when the address has a name, the address alone otherwise.</summary>
        public static string DisplayWithAddress(string address)
        {
            string name = Current.NameFor(address);
            return name != null ? string.Format("{0} ({1})", name, address.Trim()) : address;
        }

        /// <summary>A ';' separated list of addresses, each as DisplayWithAddress shows it, separated by commas.</summary>
        public static string DisplayListWithAddresses(string addresses)
        {
            return string.Join(", ", (addresses ?? string.Empty)
                .Split(ActivityList.AddressSeparator)
                .Select(address => address.Trim())
                .Where(address => address.Length > 0)
                .Select(DisplayWithAddress));
        }

        /// <summary>The most names one turn carries; a game has far fewer players.</summary>
        public const int MaxSharedNames = 32;

        /// <summary>
        /// The names a turn carries to the other players: the player's own name for the address the turn goes out
        /// from, and the name this PC has for each other player of the game (the players the save lists and the
        /// turn's recipients), whether the player typed it or learned it. Only players of that game are named, and
        /// their addresses are in the save already, so nothing reaches anyone who did not have it.
        /// </summary>
        public static List<PlayerAlias> NamesToShare(AliasList list, string playerName, string from, IEnumerable<string> players)
        {
            List<PlayerAlias> names = new List<PlayerAlias>();
            string own = (from ?? string.Empty).Trim();
            if (own.Length > 0 && !string.IsNullOrWhiteSpace(playerName) && playerName.Trim().Length <= PlayerAlias.MaxNameLength)
            {
                names.Add(new PlayerAlias(playerName.Trim(), own));
            }

            foreach (string address in (players ?? Enumerable.Empty<string>())
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Select(address => address.Trim())
                .Where(address => !string.Equals(address, own, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string name = list != null ? list.NameFor(address) : null;
                if (name != null && names.Count < MaxSharedNames)
                {
                    names.Add(new PlayerAlias(name, address));
                }
            }
            return names;
        }

        /// <summary>
        /// Takes the names a received turn carried into the list, and returns how many it took. Only a sender the
        /// player already knows is listened to, so a stranger's turn names nobody; only the players of that game
        /// (the sender and the players the save lists) can be named, never the player's own addresses; and a name
        /// already in the list is never replaced. The names taken are marked as learned from the sender: they show
        /// like the player's own, but do not make an address a known sender.
        /// </summary>
        public static int LearnFromTurn(AliasList list, IEnumerable<PlayerAlias> shared, string sender, bool senderKnown,
            IEnumerable<string> players, IEnumerable<string> ownAddresses)
        {
            if (list == null || shared == null || !senderKnown || string.IsNullOrWhiteSpace(sender))
            {
                return 0;
            }

            HashSet<string> game = new HashSet<string>((players ?? Enumerable.Empty<string>()).Where(address => !string.IsNullOrWhiteSpace(address)).Select(address => address.Trim()), StringComparer.OrdinalIgnoreCase);
            game.Add(sender.Trim());
            HashSet<string> own = new HashSet<string>((ownAddresses ?? Enumerable.Empty<string>()).Where(address => !string.IsNullOrWhiteSpace(address)).Select(address => address.Trim()), StringComparer.OrdinalIgnoreCase);

            int learned = 0;
            foreach (PlayerAlias alias in shared.Where(alias => alias != null && !string.IsNullOrWhiteSpace(alias.Address)).Take(MaxSharedNames))
            {
                string address = alias.Address.Trim();
                if (game.Contains(address) && !own.Contains(address) && list.Learn(alias.Name, address, sender))
                {
                    learned++;
                }
            }
            return learned;
        }

        /// <summary>
        /// The text with every address that has a name replaced by the name, for messages built before the
        /// names were looked at. Only whole addresses are replaced: one address inside a longer one is not.
        /// </summary>
        public static string InText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            foreach (PlayerAlias alias in Current.Aliases.Where(alias => alias != null && !string.IsNullOrWhiteSpace(alias.Address) && !string.IsNullOrWhiteSpace(alias.Name)))
            {
                string pattern = @"(?<![\w.+-])" + Regex.Escape(alias.Address.Trim()) + @"(?![\w-]|\.\w)";
                string name = alias.Name.Trim();
                text = Regex.Replace(text, pattern, match => name, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            return text;
        }
    }
}
