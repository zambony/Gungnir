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
    /// Attribute to be applied to a static method for use by the command handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class Command : Attribute
    {
        public readonly string keyword;
        public readonly string description;

        public Command(string keyword, string description)
        {
            this.keyword = keyword;
            this.description = description;
        }
    }

    /// <summary>
    /// This class is responsible for parsing and running commands. All commands are
    /// defined here as public static methods and annotated with the <see cref="Command"/> attribute.
    /// </summary>
    internal class CommandHandler
    {
        /// <summary>
        /// Stores metadata information about a command, including a reference to the method in question.
        /// </summary>
        internal class CommandMeta
        {
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
            public int requiredArguments;

            public CommandMeta(Command data, MethodBase method, List<ParameterInfo> arguments)
            {
                this.data = data;
                this.method = method;
                this.arguments = arguments;

                if (arguments.Count > 0)
                {
                    StringBuilder builder = new StringBuilder();

                    foreach (ParameterInfo info in arguments)
                    {
                        bool optional = info.HasDefaultValue;

                        requiredArguments += optional ? 0 : 1;

                        if (!optional)
                            builder.Append($"<{Util.GetSimpleTypeName(info.ParameterType)} {info.Name}> ");
                        else
                            builder.Append($"[{Util.GetSimpleTypeName(info.ParameterType)} {info.Name}={info.DefaultValue}] ");
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

        private Dictionary<string, CommandMeta> m_actions;
        private const string CommandPattern = @"(?:(?<="").+(?=""))|(?:[^""\s]+)";

        public CommandHandler() : base()
        {
            m_actions = new Dictionary<string, CommandMeta>();
            Register();
        }

        [Command("test", "Tests if the mod works.")]
        public static void Test(string text)
        {
            Logger.Log("Your command has been run: " + text, true);
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
                select new CommandMeta(attribute, method, method.GetParameters().ToList());

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

                new Terminal.ConsoleCommand("/" + command.data.keyword, helpText, delegate (Terminal.ConsoleEventArgs args)
                {
                    ParseAndRun(args.FullLine);
                });
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
                Logger.Error($"Unknown command '{commandName}'", true);
                return;
            }

            if (args.Count < command.requiredArguments)
            {
                Logger.Error($"Missing required number of arguments for '{commandName}'", true);
                return;
            }

            List<object> convertedArgs = new List<object>();

            // Loop through each argument type of the command object
            // and attempt to convert the corresponding text value to that type.
            // We'll unpack the converted args list into the function call which will automatically
            // cast from object -> the parameter type.
            for (int i = 0; i < command.arguments.Count; i++)
            {
                Type argType = command.arguments[i].ParameterType;
                string arg = args[i];

                object converted = Util.StringToObject(arg, argType);

                // Couldn't convert, oh well!
                if (converted == null)
                {
                    Logger.Error($"Error while converting arguments for command '{commandName}'", true);
                    return;
                }

                convertedArgs.Add(converted);
            }

            // Invoke the method, which will expand all the arguments automagically.
            command.method.Invoke(this, convertedArgs.ToArray());
        }
    }
}
