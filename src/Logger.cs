using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Gungnir
{
    internal static class Logger
    {
        public static Color ErrorColor   = new Color(0.9686274509803922f, 0.49411764705882355f, 0.5372549019607843f);
        public static Color WarningColor = new Color(0.922f, 0.796f, 0.545f);
        public static Color GoodColor    = new Color(0.6862745098039216f, 0.8f, 0.5882352941176471f);

        private static Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    return ErrorColor;
                case LogType.Warning:
                    return WarningColor;
                default:
                    return Color.white;
            }
        }

        private static Color GetLogSubColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    return Color.white;
                case LogType.Warning:
                    return Color.white;
                default:
                    return GoodColor;
            }
        }

        private static void Print(
            string message,
            LogType type = LogType.Log,
            bool logToConsole = false,
            int lineNumber = 0,
            string filePath = "",
            string methodName = ""
        )
        {
            if (logToConsole && Console.instance != null)
            {
                string tagColor = "87E3FF";
                string msgColor = ColorUtility.ToHtmlStringRGBA(GetLogColor(type));
                Console.instance.Print($"<color=#{tagColor}>[Gungnir]</color> <color=#{msgColor}>{message}</color>");
            }

            // Strip out color tags for printing to console.
            message = Util.StripTags(message);

            Debug.unityLogger.Log(type, $"[Gungnir:{Path.GetFileName(filePath)}:{lineNumber}] {message}");
        }

        public static LogStream Log(string message = "",
            bool logToConsole = true,
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = "")
        {
            if (!string.IsNullOrEmpty(message))
                Print(message, LogType.Log, logToConsole, lineNumber, filePath, methodName);

            return new LogStream(LogType.Log, lineNumber, filePath, methodName);
        }

        public static LogStream Error(string message = "",
            bool logToConsole = true,
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = "")
        {
            if (!string.IsNullOrEmpty(message))
                Print(message, LogType.Error, logToConsole, lineNumber, filePath, methodName);

            return new LogStream(LogType.Error, lineNumber, filePath, methodName);
        }

        public static LogStream Warning(string message = "",
            bool logToConsole = true,
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = "")
        {
            if (!string.IsNullOrEmpty(message))
                Print(message, LogType.Warning, logToConsole, lineNumber, filePath, methodName);

            return new LogStream(LogType.Warning, lineNumber, filePath, methodName);
        }

        internal class LogStream
        {
            private StringBuilder    builder    = new StringBuilder();
            private readonly int     lineNumber = 0;
            private readonly string  filePath   = "";
            private readonly string  methodName = "";
            private readonly LogType type       = LogType.Log;

            private bool m_space = true;
            private bool m_quote = false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream space() { m_space = true; builder.Append(' '); return this; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream maybeSpace() { if (m_space) builder.Append(' '); return this; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream noSpace() { m_space = false; return this; }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream quote() { m_quote = true; builder.Append('"'); return this; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream maybeQuote(char c = '"') { if (m_quote) builder.Append(c); return this; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream noQuote() { m_quote = false; return this; }

            public bool autoInsertSpaces() { return m_space; }
            public void setAutoInsertSpaces(bool enabled) { m_space = enabled; }

            public LogStream(LogType type, int lineNumber, string filePath, string methodName)
            {
                this.type = type;
                this.lineNumber = lineNumber;
                this.filePath = filePath;
                this.methodName = methodName;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream write(object input, Color? color = null)
            {
                builder.Append(color != null ? input.ToString().WithColor((Color)color) : input);

                return maybeSpace();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LogStream param(object input)
            {
                builder.Append(input.ToString().WithColor(GetLogSubColor(type)));

                return maybeSpace();
            }

            public void commit()
            {
                if (builder.Length > 0)
                    Print(builder.ToString().TrimEnd(), type, true, lineNumber, filePath, methodName);
            }
        }
    }
}
