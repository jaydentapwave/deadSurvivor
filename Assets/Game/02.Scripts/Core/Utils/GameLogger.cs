using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Game.Tests.EditMode")]

public static class GameLogger
{
    public static void Log(string message) => Write(Const.IsLogEnabled, LogType.Log, message);

    public static void LogWarning(string message) => Write(Const.IsLogEnabled, LogType.Warning, message);

    public static void LogError(string message) => Write(Const.IsLogEnabled, LogType.Error, message);

    internal static void Write(bool isEnabled, LogType logType, string message)
    {
        if (!isEnabled)
        {
            return;
        }

        Debug.unityLogger.Log(logType, message);
    }
}
