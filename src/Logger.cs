using System.Runtime.CompilerServices;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

namespace Gungnir
{
    internal static class Logger
    {
        public static Color ErrorColor = new Color(0.9686274509803922f, 0.49411764705882355f, 0.5372549019607843f);
        public static Color WarningColor = new Color(0.922f, 0.796f, 0.545f);
        public static Color GoodColor = new Color(0.6862745098039216f, 0.8f, 0.5882352941176471f);

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
                Console.instance.Print($"<color=#{tagColor}>[Gungnir]</color> <color=#{msgColor}>{message}</color>");
            }

            // Strip out color tags for printing to console.
            message = Util.StripTags(message);

            Debug.unityLogger.Log(type, $"[Gungnir:{Path.GetFileName(filePath)}:{lineNumber}] {message}");
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
