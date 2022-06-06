using System.Runtime.CompilerServices;
using UnityEngine;
using System.IO;

namespace Consol
{
    internal static class Logger
    {
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
                Console.instance.Print(message);

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
