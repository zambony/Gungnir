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
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string methodName = ""
        )
        {
            Debug.unityLogger.Log(type, $"[Consol:{Path.GetFileName(filePath)}:{lineNumber}] {message}");
        }

        public static void Log(string message) => Print(message, LogType.Log);
        public static void Error(string message) => Print(message, LogType.Error);
        public static void Warning(string message) => Print(message, LogType.Warning);
    }
}
