using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Consol
{
    /// <summary>
    /// Collection of utility functions to find players, trim text, and do basic tasks.
    /// </summary>
    internal static class Util
    {
        /// <summary>
        /// Find a <see cref="Player"/> by their name. Case insensitive, and allows partial matches.
        /// </summary>
        /// <param name="name">Name of the player to lookup, or the start of their name to try for a partial match.</param>
        /// <param name="foundMultiple"><see langword="true"/> if there were multiple matches for the query, <see langword="false"/> if not.</param>
        /// <returns>
        /// <see cref="Player"/> found by the search, or <see langword="null"/> if no player with that name could be found or there were
        /// too many matches found.
        /// </returns>
        public static Player GetPlayerByName(string name, out bool foundMultiple)
        {
            foundMultiple = false;

            try
            {
                var query =
                    from player in Player.GetAllPlayers()
                    where player.GetPlayerName().ToLower().Simplified().StartsWith(name.ToLower())
                    select player;

                if (query.Count() > 1)
                {
                    // If there were multiple matches (e.g. two players named "Ben" and "Benjamin"), then try
                    // to find the exact match. If there's no exact match, the intent is unclear and we shouldn't process it.
                    foreach (Player player in query)
                    {
                        if (player.GetPlayerName().ToLower().Simplified().Equals(name.ToLower()))
                            return player;
                    }

                    foundMultiple = true;
                    return null;
                }

                return query.First();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Find a <see cref="Player"/> by their ID number.
        /// </summary>
        /// <param name="id">Player ID to find.</param>
        /// <returns><see cref="Player"/> object with the ID number <paramref name="id"/>.</returns>
        public static Player GetPlayerByID(long id)
        {
            try
            {
                return (from player in Player.GetAllPlayers() where player.GetPlayerID() == id select player).First();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Trims leading and trailing spaces, and collapses repeating spaces to a single space.
        /// </summary>
        /// <param name="value"><see langword="string"/> to clean up.</param>
        /// <returns>Simplified version of the string.</returns>
        public static string Simplified(this string value)
        {
            return Regex.Replace(value.Trim(), @"\s{2,}", " ");
        }

        /// <summary>
        /// Converts a text value to the corresponding type and returns it as a generic <see langword="object"/>.
        /// </summary>
        /// <param name="value">Text to convert to some object.</param>
        /// <param name="toType"><see cref="Type"/> value to convert to.</param>
        /// <returns>An <see langword="object"/> reference to the converted type.</returns>
        public static object StringToObject(string value, Type toType)
        {
            try
            {
                if (toType == typeof(Player))
                {
                    if (long.TryParse(value, out long id))
                    {
                        return GetPlayerByID(id);
                    }
                    else
                    {
                        Player player = GetPlayerByName(value, out bool foundMultiple);

                        if (player != null)
                            return player;
                        else if (foundMultiple)
                        {
                            Logger.Error($"Found multiple players with the name '{value}'", true);
                            return null;
                        }
                        else
                        {
                            Logger.Error($"No player named '{value}'", true);
                            return null;
                        }
                    }
                }
                else if (toType == typeof(string))
                    return value;

                return Convert.ChangeType(value, toType);
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to convert '{value}' to type '{toType}': {e.Message}", true);
                return null;
            }
        }

        /// <summary>
        /// Translates a <see cref="Type"/> to a nice user-friendly name.
        /// </summary>
        /// <param name="type"><see cref="Type"/> to translate</param>
        /// <returns><see langword="string"/> containing the type name.</returns>
        public static string GetSimpleTypeName(Type type)
        {
            switch (type.Name)
            {
                case nameof(Int32):
                case nameof(Int64):
                {
                    return "Number";
                }
                case nameof(UInt32):
                case nameof(UInt64):
                {
                    return "(+)Number";
                }
                case nameof(Single):
                case nameof(Double):
                {
                    return "Decimal";
                }
                default:
                    return type.Name;
            }
        }
    }
}
