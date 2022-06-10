using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;

namespace Consol
{
    /// <summary>
    /// Attribute to be applied to a method for use by the command handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class Command : Attribute
    {
        public readonly string keyword;
        public readonly string description;
        public readonly string autoCompleteTarget;

        public Command(string keyword, string description, string autoComplete = null)
        {
            this.keyword = keyword;
            this.description = description;
            this.autoCompleteTarget = autoComplete;
        }
    }

    /// <summary>
    /// Stores metadata information about a command, including a reference to the method in question.
    /// </summary>
    internal class CommandMeta
    {
        public delegate List<string> AutoCompleteDelegate();

        /// <summary>
        /// <see cref="Command"/> attribute data to access the command name and description.
        /// </summary>
        public readonly Command data;
        /// <summary>
        /// The actual command function.
        /// </summary>
        public readonly MethodBase method;
        /// <summary>
        /// A <see cref="List{ParameterInfo}"/> of argument information.
        /// </summary>
        public readonly List<ParameterInfo> arguments;
        /// <summary>
        /// A <see langword="string"/> representing argument types, names, and whether they are required
        /// parameters, e.g. <c>&lt;number amount&gt; [Player player]</c>
        /// </summary>
        public readonly string hint;
        /// <summary>
        /// Number of required arguments for the command to run.
        /// </summary>
        public readonly int requiredArguments;
        /// <summary>
        /// Delegate to return potential autocomplete topics.
        /// </summary>
        public readonly AutoCompleteDelegate AutoComplete;

        public CommandMeta(Command data, MethodBase method, List<ParameterInfo> arguments, AutoCompleteDelegate autoCompleteDelegate = null)
        {
            this.data = data;
            this.method = method;
            this.arguments = arguments;
            this.AutoComplete = autoCompleteDelegate;

            // If we have any arguments, attempt to build the argument hint string.
            if (arguments.Count > 0)
            {
                StringBuilder builder = new StringBuilder();

                foreach (ParameterInfo info in arguments)
                {
                    bool optional = info.HasDefaultValue;

                    requiredArguments += optional ? 0 : 1;

                    // Required parameters use chevrons, and optionals use brackets.
                    if (!optional)
                    {
                        builder.Append($"<{Util.GetSimpleTypeName(info.ParameterType)} {info.Name}> ");
                    }
                    else
                    {
                        string defaultValue = info.DefaultValue == null ? "none" : info.DefaultValue.ToString();
                        builder.Append($"[{Util.GetSimpleTypeName(info.ParameterType)} {info.Name}={defaultValue}] ");
                    }
                }

                // Remove trailing space.
                builder.Remove(builder.Length - 1, 1);

                hint = builder.ToString();
            }
            else
            {
                requiredArguments = 0;
            }
        }
    }

    /// <summary>
    /// This class is responsible for parsing and running commands. All commands are
    /// defined here as public methods and annotated with the <see cref="Command"/> attribute.
    /// </summary>
    internal class CommandHandler
    {
        private Dictionary<string, CommandMeta> m_actions = new Dictionary<string, CommandMeta>();
        private const string CommandPattern = @"(?:(?<="").+(?=""))|(?:[^""\s]+)";
        private const BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private CustomConsole m_console;

        public CustomConsole Console { get => m_console; set => m_console = value; }

        /// <summary>
        /// Helper function for the give command's autocomplete feature.
        /// </summary>
        /// <returns>A <see cref="List{string}"/> of all prefab names if the Scene is loaded, otherwise <see langword="null"/>.</returns>
        private static List<string> GetPrefabNames()
        {
            if (ZNetScene.instance != null)
                return ZNetScene.instance.GetPrefabNames();
            else
                return null;
        }

        [Command("clear", "Clears the console's output")]
        public void ClearConsole()
        {
            Console?.ClearScreen();
        }

        [Command("give", "Give an item to yourself or another player.", nameof(GetPrefabNames))]
        public void Give(string itemName, int amount = 1, Player player = null, int level = 1)
        {
            if (amount <= 0)
            {
                Logger.Error("Amount must be greater than 0", true);
                return;
            }

            if (level <= 0)
            {
                Logger.Error("Level must be greater than 0", true);
                return;
            }

            if (player == null)
                player = Player.m_localPlayer;

            string prefab = null;

            /// TODO: Refactor the core of this into a helper method to find items.
            foreach (string prefabName in ZNetScene.instance.GetPrefabNames())
            {
                // Check for exact match first, and then find a best case match if possible.
                if (prefabName.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                {
                    prefab = prefabName;
                    break;
                }
                else if (prefabName.IndexOf(itemName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (prefab != null)
                    {
                        Logger.Error($"Found more than one item containing the text '<color=white>{itemName}</color>', please be more specific", true);
                        return;
                    }

                    prefab = prefabName;
                }
            }

            GameObject prefabObject = ZNetScene.instance.GetPrefab(prefab);

            if (prefabObject.GetComponent<ItemDrop>() == null)
            {
                Logger.Error($"Found a prefab named '<color=white>{prefab}</color>', but that isn't an item", true);
                return;
            }

            Vector3 vector = UnityEngine.Random.insideUnitSphere * 0.5f;
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Spawning object " + prefab);

            GameObject spawned = UnityEngine.Object.Instantiate(prefabObject, player.transform.position + player.transform.forward * 2f + Vector3.up + vector, Quaternion.identity);

            if (!spawned)
            {
                Logger.Error("Something went wrong!", true);
                return;
            }
            else
            {
                string green = ColorUtility.ToHtmlStringRGB(Logger.GoodColor);
                Logger.Log(
                    $"Gave <color=#{green}>{amount}</color> of <color=#{green}>{prefab}</color> to <color=#{green}>{player.GetPlayerName()}</color>", true);
            }

            ItemDrop item = spawned.GetComponent<ItemDrop>();

            item.m_itemData.m_quality = level;
            item.m_itemData.m_stack = amount;
            item.m_itemData.m_durability = item.m_itemData.GetMaxDurability();

            player.Pickup(spawned, autoequip: false, autoPickupDelay: false);
        }

        // Please do not edit below this line unless you really need to.
        // Thanks.

        public CommandHandler() : base()
        {
            Register();
        }

        /// <summary>
        /// Helper method to create a <see cref="CommandMeta.AutoCompleteDelegate"/> function for autocomplete handlers.
        /// </summary>
        /// <param name="method">Name of the method attached to the <see cref="CommandHandler"/> class that will provide autocomplete values.</param>
        /// <returns>A <see cref="CommandMeta.AutoCompleteDelegate"/> if the <paramref name="method"/> exists, otherwise <see langword="null"/>.</returns>
        private static CommandMeta.AutoCompleteDelegate MakeAutoCompleteDelegate(string method)
        {
            if (string.IsNullOrEmpty(method))
                return null;

            var target = typeof(CommandHandler).GetMethod(method, s_bindingFlags);

            if (target != null)
                return target.CreateDelegate(typeof(CommandMeta.AutoCompleteDelegate)) as CommandMeta.AutoCompleteDelegate;

            return null;
        }

        /// <summary>
        /// Uses reflection to find all of the methods in this class annotated with the <see cref="Command"/> attribute
        /// and registers them for execution.
        /// </summary>
        private void Register()
        {
            IEnumerable<CommandMeta> query =
                from method in typeof(CommandHandler).GetMethods()
                from attribute in method.GetCustomAttributes().OfType<Command>()
                select new CommandMeta(
                    attribute,
                    method,
                    method.GetParameters().ToList(),
                    MakeAutoCompleteDelegate(attribute.autoCompleteTarget)
                );

            Logger.Log($"Registering {query.Count()} commands...");

            foreach (CommandMeta command in query)
            {
                /// TODO: Register with a custom prefix, maybe controlled by config?
                /// Have to reload/reregister commands when prefix is changed I guess.
                m_actions.Add("/" + command.data.keyword, command);

                string helpText;

                if (command.arguments.Count > 0)
                    helpText = $"{command.hint} - {command.data.description}";
                else
                    helpText = command.data.description;

                Terminal.ConsoleOptionsFetcher fetcher = null;

                if (command.AutoComplete != null)
                    fetcher = new Terminal.ConsoleOptionsFetcher(command.AutoComplete);

                new Terminal.ConsoleCommand("/" + command.data.keyword, helpText, delegate (Terminal.ConsoleEventArgs args)
                {
                    ParseAndRun(args.FullLine);
                }, optionsFetcher: fetcher);
                Logger.Log($"Registered command {command.data.keyword}");
            }

            Logger.Log("Finished registering commands!");
        }

        /// <summary>
        /// Attempts to run a command string that looks similar to <c>/cmd arg1 arg2 "arg with spaces"</c>.
        /// Will split arguments by spaces and quotation marks, attempt to convert each argument to the command's
        /// parameters, then run the command.
        /// </summary>
        /// <param name="text">Command string to evaluate.</param>
        public void ParseAndRun(string text)
        {
            // Remove garbage.
            text = text.Simplified();

            // Split the text using our pattern. Splits by spaces but preserves quote groups.
            List<string> args = Regex.Matches(text, CommandPattern)
                                            .OfType<Match>()
                                            .Select(m => m.Groups[0].Value)
                                            .ToList();

            // Store command ID and remove it from our arguments list.
            string commandName = args[0];
            args.RemoveAt(0);

            // Look up the command value, fail if it doesn't exist.
            if (!m_actions.TryGetValue(commandName, out CommandMeta command))
            {
                Logger.Error($"Unknown command '<color=white>{commandName}</color>'", true);
                return;
            }

            if (args.Count < command.requiredArguments)
            {
                Logger.Error($"Missing required number of arguments for '<color=white>{commandName}</color>'", true);
                return;
            }

            List<object> convertedArgs = new List<object>();

            // Loop through each argument type of the command object
            // and attempt to convert the corresponding text value to that type.
            // We'll unpack the converted args list into the function call which will automatically
            // cast from object -> the parameter type.
            for (int i = 0; i < command.arguments.Count; ++i)
            {
                // If there is a user supplied value, try to convert it.
                if (i < args.Count)
                {
                    Type argType = command.arguments[i].ParameterType;
                    string arg = args[i];

                    object converted = Util.StringToObject(arg, argType);

                    // Couldn't convert, oh well!
                    if (converted == null)
                    {
                        Logger.Error($"Error while converting arguments for command '<color=white>{commandName}</color>'", true);
                        return;
                    }

                    convertedArgs.Add(converted);
                }
                // Otherwise, if we're still iterating, there's parameters they left unfilled.
                // This will only execute if they are optional parameters, due to our required arg count check earlier.
                else
                {
                    // Since Invoke requires all parameters to be filled, we have to manually insert the function's default value.
                    // Very silly.
                    convertedArgs.Add(command.arguments[i].DefaultValue);
                }
            }

            // Invoke the method, which will expand all the arguments automagically.
            command.method.Invoke(this, convertedArgs.ToArray());
        }
    }
}
