using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

namespace Consol
{
    [Serializable]
    internal class TooManyValuesException : Exception
    {
        public int expected;
        public int actual;

        public TooManyValuesException(int expected, int actual) : base($"Too many values found, expected {expected}, got {actual}")
        {
            this.expected = expected;
            this.actual = actual;
        }
    }

    [Serializable]
    internal class NoMatchFoundException : Exception
    {
        public string key;

        public NoMatchFoundException(string key) : base($"No match found for key: {key}")
        {
            this.key = key;
        }
    }

    /// <summary>
    /// Collection of utility functions to find players, trim text, and do basic tasks.
    /// </summary>
    internal static class Util
    {
        private static readonly Regex s_tagStripPattern = new Regex(@"<((?:b)|(?:i)|(?:size)|(?:color)|(?:quad)|(?:material)).*?>(.*?)<\/\1>");

        /// <summary>
        /// Find a <see cref="Player"/> by their name. Case insensitive, and allows partial matches.
        /// </summary>
        /// <param name="name">Name of the player to lookup, or the start of their name to try for a partial match.</param>
        /// <param name="noThrow">True to not throw an exception if the search fails, false if you'd like failure info.</param>
        /// <returns>
        /// <see cref="Player"/> found by the search, or <see langword="null"/> if no player with that name could be found or there were
        /// too many matches found.
        /// </returns>
        /// <exception cref="NoMatchFoundException"></exception>
        /// <exception cref="TooManyValuesException"></exception>
        public static Player GetPlayerByName(string name, bool noThrow = false)
        {
            try
            {
                var query =
                    from player in Player.GetAllPlayers()
                    where player.GetPlayerName().Simplified().StartsWith(name.ToLower(), StringComparison.OrdinalIgnoreCase)
                    select player;

                if (query.Count() > 1)
                {
                    // If there were multiple matches (e.g. two players named "Ben" and "Benjamin"), then try
                    // to find the exact match. If there's no exact match, the intent is unclear and we shouldn't process it.
                    foreach (Player player in query)
                    {
                        if (player.GetPlayerName().Simplified().Equals(name.ToLower(), StringComparison.OrdinalIgnoreCase))
                            return player;
                    }

                    if (!noThrow)
                        throw new TooManyValuesException(1, query.Count());

                    return null;
                }

                return query.First();
            }
            catch (SystemException)
            {
                if (!noThrow)
                    throw new NoMatchFoundException(name);

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
        /// Find a prefab by name. Case insensitive. Prioritizes exact matches, but will find partials.
        /// </summary>
        /// <param name="name">Name to find.</param>
        /// <param name="noThrow">True to disable exceptions, false otherwise.</param>
        /// <returns>A <see cref="GameObject"/> if one was foumdo otherwise <see langword="null"/>.</returns>
        /// <exception cref="NoMatchFoundException"></exception>
        /// <exception cref="TooManyValuesException"></exception>
        public static GameObject GetPrefabByName(string name, bool noThrow = false)
        {
            IEnumerable<string> query =
                from prefabName in ZNetScene.instance.GetPrefabNames()
                where prefabName.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                orderby prefabName.Length
                select prefabName;

            int count = query.Count();

            if (count <= 0)
            {
                if (!noThrow)
                    throw new NoMatchFoundException(name);
            }
            else
            {
                string first = query.First();

                // Test if we have just one result, or the first result is an exact match.
                if (count == 1 || first.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return ZNetScene.instance.GetPrefab(first);
                else if (!noThrow)
                    throw new TooManyValuesException(1, count);
            }

            return null;
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
        /// <param name="noThrow">Whether to throw conversion exceptions or not.</param>
        /// <returns>An <see langword="object"/> reference to the converted type.</returns>
        /// <exception cref="InvalidCastException"/>
        /// <exception cref="FormatException"/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="TooManyValuesException"/>
        /// <exception cref="NoMatchFoundException"/>
        public static object StringToObject(string value, Type toType, bool noThrow = false)
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
                        return GetPlayerByName(value, noThrow);
                    }
                }
                else if (toType == typeof(string))
                    return value;
                else if (toType == typeof(bool))
                {
                    if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    else
                        return false;
                }

                return Convert.ChangeType(value, toType);
            }
            catch (Exception)
            {
                if (!noThrow)
                    throw;

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

        /// <summary>
        /// Strips away any markdown tags such as b, i, color, etc. from Text label input.
        /// </summary>
        /// <param name="input">Text to sanitize.</param>
        /// <returns>A <see langword="string"/> containing the sanitized text.</returns>
        public static string StripTags(string input)
        {
            return s_tagStripPattern.Replace(input, (Match match) =>
            {
                return match.Groups[2].Value;
            });
        }

        /// <summary>
        /// Extension method to convert a list object to a nicely formatted string, since <see cref="List{T}.ToString"/> isn't helpful.
        /// </summary>
        /// <typeparam name="T">List type.</typeparam>
        /// <param name="input">List to convert to text.</param>
        /// <returns>A formatted <see langword="string"/> of the list's contents and type.</returns>
        public static string AsText<T>(this List<T> input)
        {
            string value = $"List<{typeof(T).Name}>(";

            foreach (var item in input)
                value += item.ToString() + ",";

            value = value.Remove(value.Length - 1);
            value += ")";

            return value;
        }

        /// <summary>
        /// Extension method to create markup color tags around a string.
        /// </summary>
        /// <param name="text">Text to wrap with color tags.</param>
        /// <param name="color"><see cref="Color"/> to use for the tag. Alpha is ignored.</param>
        /// <returns>A <see langword="string"/> wrapped with color tags.</returns>
        public static string WithColor(this string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }
    }
}
