using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;

namespace Wagenheimer.IAPHelper
{
    public enum IAPLogLevel
    {
        Info,
        Warning,
        Error
    }

    public readonly struct IAPLogEntry
    {
        public readonly DateTime Time;
        public readonly IAPLogLevel Level;
        public readonly string Message;
        public readonly string StackTrace;

        public IAPLogEntry(DateTime time, IAPLogLevel level, string message, string stackTrace)
        {
            Time = time;
            Level = level;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    /// <summary>
    /// Persistent, in-memory log of everything IAP related, kept from app start (not only while the
    /// debug overlay is open). It automatically captures every Unity log line tagged "[IAP..." (IAPHelper,
    /// IAPRestoreButton, IAPProductButton...) and is written to directly by <see cref="BaseIAPForm"/>.
    /// Read it with <see cref="ToText"/> to copy, save or share it.
    /// </summary>
    public static class IAPLog
    {
        private const int Capacity = 800;
        private const string CapturedTagPrefix = "[IAP";
        private const int MaxStackLines = 6;
        private const string FileName = "iap-log.txt";

        private static readonly object Gate = new object();
        private static readonly List<IAPLogEntry> Entries = new List<IAPLogEntry>(Capacity);

        // Set while IAPLog itself writes to the Unity console, so the capture hook does not add the line twice.
        [ThreadStatic] private static bool _isWriting;

        /// <summary>Raised (possibly from a non-main thread) when an entry is added.</summary>
        public static event Action<IAPLogEntry> OnEntryAdded;

        /// <summary>
        /// Optional hook that lets the game share the log natively (e.g. a NativeShare plugin):
        /// <c>IAPLog.ShareHandler = text => MyShare(text);</c>. The debug overlay shows a Share button when set.
        /// </summary>
        public static Action<string> ShareHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            lock (Gate)
                Entries.Clear();

            OnEntryAdded = null;
            ShareHandler = null;
            Application.logMessageReceivedThreaded -= OnUnityLog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartCapture()
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            Application.logMessageReceivedThreaded += OnUnityLog;
            Info($"[IAPLog] Capture started. {Application.productName} {Application.version} | Unity {Application.unityVersion} | {Application.platform} | {SystemInfo.deviceModel}");
        }

        public static void Info(string message) => Write(IAPLogLevel.Info, message);
        public static void Warning(string message) => Write(IAPLogLevel.Warning, message);
        public static void Error(string message) => Write(IAPLogLevel.Error, message);

        public static int Count
        {
            get
            {
                lock (Gate)
                    return Entries.Count;
            }
        }

        /// <summary>Copy of the entries, oldest first.</summary>
        public static List<IAPLogEntry> Snapshot()
        {
            lock (Gate)
                return new List<IAPLogEntry>(Entries);
        }

        public static void Clear()
        {
            lock (Gate)
                Entries.Clear();
        }

        /// <summary>The whole log as plain text, ready to paste into a chat, a bug report or an AI prompt.</summary>
        public static string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== IAP Helper Log ===");
            sb.AppendLine($"App: {Application.productName} {Application.version} | Unity {Application.unityVersion}");
            sb.AppendLine($"Platform: {Application.platform} | Device: {SystemInfo.deviceModel} | OS: {SystemInfo.operatingSystem}");
            sb.AppendLine($"Language: {Application.systemLanguage} | Development build: {Debug.isDebugBuild}");
            sb.AppendLine($"Entries: {Count}");
            sb.AppendLine("======================");

            foreach (var entry in Snapshot())
            {
                sb.Append('[').Append(entry.Time.ToString("HH:mm:ss.fff")).Append("] ")
                  .Append(LevelTag(entry.Level)).Append(' ')
                  .AppendLine(entry.Message);

                if (!string.IsNullOrEmpty(entry.StackTrace))
                    sb.AppendLine(TrimStack(entry.StackTrace));
            }

            return sb.ToString();
        }

        /// <summary>Writes the log to persistentDataPath and returns the full path (or null on failure).</summary>
        public static string SaveToFile()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, FileName);
                File.WriteAllText(path, ToText());
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPLog] Could not save the log: {ex.Message}");
                return null;
            }
        }

        private static void Write(IAPLogLevel level, string message)
        {
            Add(level, message, null);

            _isWriting = true;
            try
            {
                switch (level)
                {
                    case IAPLogLevel.Error: Debug.LogError(message); break;
                    case IAPLogLevel.Warning: Debug.LogWarning(message); break;
                    default: Debug.Log(message); break;
                }
            }
            finally
            {
                _isWriting = false;
            }
        }

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (_isWriting || condition == null || !condition.StartsWith(CapturedTagPrefix, StringComparison.Ordinal))
                return;

            var level = type switch
            {
                LogType.Error or LogType.Exception or LogType.Assert => IAPLogLevel.Error,
                LogType.Warning => IAPLogLevel.Warning,
                _ => IAPLogLevel.Info
            };

            Add(level, condition, level == IAPLogLevel.Error ? stackTrace : null);
        }

        private static void Add(IAPLogLevel level, string message, string stackTrace)
        {
            var entry = new IAPLogEntry(DateTime.Now, level, message, stackTrace);

            lock (Gate)
            {
                if (Entries.Count >= Capacity)
                    Entries.RemoveAt(0);
                Entries.Add(entry);
            }

            OnEntryAdded?.Invoke(entry);
        }

        private static string LevelTag(IAPLogLevel level) => level switch
        {
            IAPLogLevel.Error => "[E]",
            IAPLogLevel.Warning => "[W]",
            _ => "[I]"
        };

        private static string TrimStack(string stack)
        {
            var lines = stack.Split('\n');
            var count = Math.Min(lines.Length, MaxStackLines);
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var line = lines[i].TrimEnd();
                if (line.Length > 0)
                    sb.Append("      ").AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
