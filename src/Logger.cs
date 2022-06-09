using System.Runtime.CompilerServices;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

namespace Consol
{
    internal static class Logger
    {
        public static Color ErrorColor = new Color(0.749f, 0.380f, 0.416f);
        public static Color WarningColor = new Color(0.922f, 0.796f, 0.545f);
        public static Color GoodColor = new Color(0.639f, 0.745f, 0.549f);

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

        private static void Print(
            string message,
            LogType type = LogType.Log,
            bool logToConsole = false,
            int lineNumber = 0,
            string filePath = "",
            string methodName = ""
        )
        {
            if (logToConsole)
            {
                string tagColor = "87E3FF";
                string msgColor = ColorUtility.ToHtmlStringRGBA(GetLogColor(type));
                Console.instance.Print($"<color=#{tagColor}>[Consol]</color> <color=#{msgColor}>{message}</color>");
            }

            // Strip out color tags for printing to console.
            message = Util.StripTags(message);

            Debug.unityLogger.Log(type, $"[Consol:{Path.GetFileName(filePath)}:{lineNumber}] {message}");
        }

        public static void Log(string message,
            bool logToConsole = false,
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = "") => Print(message, LogType.Log, logToConsole, lineNumber, filePath, methodName);
        public static void Error(string message,
            bool logToConsole = false,
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = "") => Print(message, LogType.Error, logToConsole, lineNumber, filePath, methodName);
        public static void Warning(string message,
            bool logToConsole = false,
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = "") => Print(message, LogType.Warning, logToConsole, lineNumber, filePath, methodName);
    }
}
