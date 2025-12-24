using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MyTools
{
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

    // 1) 加载配置（必须放在 Assets/Resources/RuntimeLogConfig.asset）
    currentConfig = Resources.Load<RuntimeLogConfig>("RuntimeLogConfig");
    if (currentConfig == null)
    {
        Debug.LogWarning("[RuntimeLogExporter] No RuntimeLogConfig found in Resources/RuntimeLogConfig");
        return;
    }

    // 2) 选择可写目录（Android 强制使用 persistentDataPath）
    string basePath;

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android：只用沙盒路径，避免外部存储权限问题
    basePath = Path.Combine(Application.persistentDataPath, "Logs");
#else
    // 其他平台：优先用配置；否则退回到项目旁的 Logs
    if (!string.IsNullOrEmpty(currentConfig.exportDirectory))
        basePath = currentConfig.exportDirectory;
    else
        basePath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Logs");
#endif

    try
    {
        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);
    }
    catch (Exception e)
    {
        // 目录创建失败时，退回到 persistentDataPath
        basePath = Path.Combine(Application.persistentDataPath, "Logs");
        try { if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath); }
        catch (Exception e2)
        {
            Debug.LogError($"[RuntimeLogExporter] Create log dir failed: {e}\nFallback failed: {e2}");
            return;
        }
    }

    // 3) 生成文件名
    switch (currentConfig.namingMode)
    {
        case LogNamingMode.ByDate:
            logPath = Path.Combine(basePath, $"Log_{DateTime.Now:yyyyMMdd}.txt");
            break;
        case LogNamingMode.ByCount:
            int count = 1; string tempPath;
            do { tempPath = Path.Combine(basePath, $"Log_{count}.txt"); count++; }
            while (File.Exists(tempPath));
            logPath = tempPath;
            break;
        default:
            logPath = Path.Combine(basePath, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            break;
    }

    // 4) 打开写入流（失败则放弃，避免崩溃）
    try
    {
        // 用 FileStream + StreamWriter 可控一些
        var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        logWriter = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };

        logWriter.WriteLine();
        logWriter.WriteLine($"[RuntimeLogExporter] Started at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
        logWriter.WriteLine($"[RuntimeLogExporter] Path: {logPath}");
        logWriter.Flush();
    }
    catch (Exception e)
    {
        Debug.LogError($"[RuntimeLogExporter] Open log file failed: {e}");
        return;
    }

    // 5) 注册日志 & 退出回调（threaded 可抓多线程日志）
    Application.logMessageReceivedThreaded += HandleLog;
    Application.quitting += OnApplicationQuit;

    isInitialized = true;
    Debug.Log($"[RuntimeLogExporter] Logging started. -> {logPath}");
}

private static readonly object _writeLock = new object();

private static void HandleLog(string logString, string stackTrace, LogType type)
{
    if (currentConfig == null || logWriter == null) return;

    // 过滤开关
    if (type == LogType.Log      && !currentConfig.logInfo)      return;
    if (type == LogType.Warning  && !currentConfig.logWarning)   return;
    if (type == LogType.Error    && !currentConfig.logError)     return;
    if (type == LogType.Exception&& !currentConfig.logException) return;

    // 构造一条记录
    var sb = new StringBuilder(256);
    sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] [").Append(type).Append("] ")
      .Append(logString);

    if (type == LogType.Error || type == LogType.Exception)
        sb.AppendLine().Append(stackTrace);

    // 线程安全写入
    lock (_writeLock)
    {
        try
        {
            logWriter.WriteLine(sb.ToString());
            // AutoFlush = true 时可不手动 Flush，留着也无妨
            logWriter.Flush();
        }
        catch (Exception e)
        {
            // 写失败时打印一次，避免死循环
            Debug.LogError($"[RuntimeLogExporter] Write failed: {e}");
        }
    }
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
