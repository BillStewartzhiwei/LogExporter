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

            // 加载配置
            currentConfig = Resources.Load<RuntimeLogConfig>("RuntimeLogConfig");
            if (currentConfig == null)
            {
                Debug.LogWarning("[RuntimeLogExporter] No RuntimeLogConfig found in Resources folder.");
                return;
            }

            // 获取项目根目录
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string basePath = string.IsNullOrEmpty(currentConfig.exportDirectory)
                ? Path.Combine(projectRoot, "Logs")
                : currentConfig.exportDirectory;

            // 创建目录（如果已存在不会报错）
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            // 根据命名模式生成日志文件名
            switch (currentConfig.namingMode)
            {
                case LogNamingMode.ByDate:
                    logPath = Path.Combine(basePath, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    break;

                case LogNamingMode.ByCount:
                    int count = 1;
                    string tempPath;
                    do
                    {
                        tempPath = Path.Combine(basePath, $"Log_{count}.txt");
                        count++;
                    } while (File.Exists(tempPath));
                    logPath = tempPath;
                    break;
            }

            // 打开日志写入流
            logWriter = new StreamWriter(logPath, true, Encoding.UTF8);
            logWriter.WriteLine($"[RuntimeLogExporter] Started at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
            logWriter.Flush();

            // 注册 Unity 日志回调
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
