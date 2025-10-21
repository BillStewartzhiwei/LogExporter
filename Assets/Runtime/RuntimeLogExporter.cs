using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MyTools
{
    [DisallowMultipleComponent]
    public static class RuntimeLogExporter
    {
        private static bool isInitialized = false;
        private static StreamWriter logWriter;
        private static RuntimeLogConfig currentConfig;
        private static string logPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (isInitialized) return;

            // 从 Resources 加载配置
            currentConfig = Resources.Load<RuntimeLogConfig>("RuntimeLogConfig");
            if (currentConfig == null)
            {
                Debug.LogWarning("[RuntimeLogExporter] No RuntimeLogConfig found in Resources folder.");
                return;
            }

            // 路径设定
            string basePath = Path.Combine(Application.dataPath, "../Logs");
            if (!string.IsNullOrEmpty(currentConfig.exportDirectory))
                basePath = currentConfig.exportDirectory;

            Directory.CreateDirectory(basePath);

            logPath = Path.Combine(basePath, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            logWriter = new StreamWriter(logPath, true, Encoding.UTF8);
            logWriter.WriteLine($"[RuntimeLogExporter] Started at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
            logWriter.Flush();

            Application.logMessageReceived += HandleLog;
            Application.quitting += OnApplicationQuit;

            isInitialized = true;
            Debug.Log("[RuntimeLogExporter] Logging started.");
        }

        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (currentConfig == null || logWriter == null)
                return;

            // 日志过滤
            if (type == LogType.Log && !currentConfig.logInfo) return;
            if (type == LogType.Warning && !currentConfig.logWarning) return;
            if (type == LogType.Error && !currentConfig.logError) return;
            if (type == LogType.Exception && !currentConfig.logException) return;

            string entry = $"[{DateTime.Now:HH:mm:ss}] [{type}] {logString}";
            if (type == LogType.Error || type == LogType.Exception)
                entry += "\n" + stackTrace;

            logWriter.WriteLine(entry);
            logWriter.Flush();
            
        }

        private static void OnApplicationQuit()
        {
            if (logWriter != null)
            {
                logWriter.WriteLine($"\n[RuntimeLogExporter] Ended at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                logWriter.Flush();
                logWriter.Close();
                logWriter.Dispose();
                logWriter = null;
                isInitialized = false;
            }
        }
    }
}
