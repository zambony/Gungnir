using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;

namespace Consol
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class Command : Attribute
    {
        public string keyword;
        public string description;

        public Command(string keyword, string description)
        {
            this.keyword = keyword;
            this.description = description;
        }
    }

    internal class CommandHandler
    {
        internal class CommandMeta
        {
            public Command data;
            public MethodBase method;
            public List<ParameterInfo> arguments;
            public string hint;
        }

        private Dictionary<string, CommandMeta> m_actions;
        private const string CommandPattern = @"(?:(?<="").+(?=""))|(?:[^""\s]+)";

        public CommandHandler() : base()
        {
            m_actions = new Dictionary<string, CommandMeta>();
            Register();
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
                select new CommandMeta()
                {
                    data = attribute,
                    method = method,
                    arguments = method.GetParameters().ToList()
                };

            Logger.Log($"Registering {query.Count()} commands...");

            foreach (CommandMeta command in query)
            {
                m_actions.Add(command.data.keyword, command);
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
                Logger.Error($"Unknown command '{commandName}'");
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
                    Logger.Error($"Error while converting arguments for command '{commandName}'");
                    return;
                }
            }

            // Invoke the method, which will expand all the arguments automagically.
            command.method.Invoke(this, convertedArgs.ToArray());
        }
    }
}
