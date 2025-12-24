using System;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MyTools
{
    public static class RuntimeLogExporter
    {
        private static bool isInitialized = false;
        private static StreamWriter logWriter;
        private static RuntimeLogConfig currentConfig;
        private static string logPath;
        private static readonly object _writeLock = new object();

#if UNITY_EDITOR
        // 编辑器下在域重载前后处理，避免资源泄漏
        [InitializeOnLoadMethod]
        private static void EditorInit()
        {
            // 确保 Play 时也能被初始化
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    Init();
                else if (state == PlayModeStateChange.ExitingPlayMode)
                    CloseWriter();
            };

            AssemblyReloadEvents.beforeAssemblyReload += CloseWriter;
            AssemblyReloadEvents.afterAssemblyReload += () => { if (Application.isPlaying) Init(); };
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (isInitialized) return;

            try
            {
                currentConfig = Resources.Load<RuntimeLogConfig>("RuntimeLogConfig");
            }
            catch (Exception)
            {
                currentConfig = null;
            }

            if (currentConfig == null)
            {
                Debug.LogWarning("[RuntimeLogExporter] No RuntimeLogConfig found in Resources/RuntimeLogConfig");
                return;
            }

            // 选择基路径
            string basePath;
#if UNITY_ANDROID && !UNITY_EDITOR
            basePath = Path.Combine(Application.persistentDataPath, "Logs");
#else
            if (!string.IsNullOrEmpty(currentConfig.exportDirectory))
                basePath = currentConfig.exportDirectory;
            else
                basePath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Logs");
#endif
            // 防止路径包含非法字符
            try { basePath = SanitizePath(basePath); } catch { basePath = Path.Combine(Application.persistentDataPath, "Logs"); }

            try
            {
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RuntimeLogExporter] Create log dir failed: {e}. Fallback to persistentDataPath");
                basePath = Path.Combine(Application.persistentDataPath, "Logs");
                try { if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath); }
                catch (Exception e2)
                {
                    Debug.LogError($"[RuntimeLogExporter] Create log dir failed: {e}\nFallback failed: {e2}");
                    return;
                }
            }

            // 生成文件名
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

            // 打开写入流
            try
            {
                var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                logWriter = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };

                if (currentConfig.includeDeviceInfo)
                {
                    logWriter.WriteLine($"[RuntimeLogExporter] Started at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                    logWriter.WriteLine($"[RuntimeLogExporter] Path: {logPath}");
                    logWriter.WriteLine($"[RuntimeLogExporter] Product: {Application.productName} Version: {Application.version} Platform: {Application.platform} Device: {SystemInfo.deviceModel} OS: {SystemInfo.operatingSystem}");
                }
                else
                {
                    logWriter.WriteLine();
                    logWriter.WriteLine($"[RuntimeLogExporter] Started at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                    logWriter.WriteLine($"[RuntimeLogExporter] Path: {logPath}");
                }

                logWriter.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"[RuntimeLogExporter] Open log file failed: {e}");
                CloseWriter();
                return;
            }

            // 注册日志回调：编辑器使用非线程版本以避免与 Console 冲突
#if UNITY_EDITOR
            Application.logMessageReceived += HandleLog;
#else
            Application.logMessageReceivedThreaded += HandleLog;
#endif
            Application.quitting += OnApplicationQuit;

            isInitialized = true;
            Debug.Log($"[RuntimeLogExporter] Logging started. -> {logPath}");
        }

        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (currentConfig == null || logWriter == null) return;

            if (type == LogType.Log      && !currentConfig.logInfo)      return;
            if (type == LogType.Warning  && !currentConfig.logWarning)   return;
            if (type == LogType.Error    && !currentConfig.logError)     return;
            if (type == LogType.Exception&& !currentConfig.logException) return;

            var sb = new StringBuilder(256);
            sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] [").Append(type).Append("] ")
              .Append(logString);

            if (type == LogType.Error || type == LogType.Exception)
                sb.AppendLine().Append(stackTrace);

            string line = sb.ToString();

            // 大小滚动检查（简单实现：在写入前检查当前文件大小）
            try
            {
                if (currentConfig.enableSizeRotation)
                    EnsureSizeRotation();
            }
            catch (Exception)
            {
                // 不要因为滚动失败而中断日志写入
            }

            lock (_writeLock)
            {
                try
                {
                    logWriter.WriteLine(line);
                    logWriter.Flush();
                }
                catch (Exception e)
                {
                    // 如果写文件失败，不要再调用 Debug.Log 来避免递归。只尝试一次通过 Unity 的日志报告（不会触发 HandleLog）。
                    Debug.LogError($"[RuntimeLogExporter] Write failed: {e}");
                }
            }

            // 注意：不要在这里调用 Debug.Log* 否则会引起递归回调。
            // currentConfig.alsoPrintToConsole 的作用：在编辑器下可以考虑将文件内容复制到 Console，但默认 Unity 已经把日志输出到 Console，
            // 所以通常无需显式再次打印。
        }

        private static void EnsureSizeRotation()
        {
            if (string.IsNullOrEmpty(logPath) || logWriter == null) return;
            if (currentConfig.maxFileSizeKB <= 0) return;

            try
            {
                var fi = new FileInfo(logPath);
                if (fi.Exists && fi.Length >= (long)currentConfig.maxFileSizeKB * 1024)
                {
                    // 关闭当前 writer，然后重命名旧文件并创建新的文件
                    CloseWriterInternal();

                    // 新名字：原名 + _n
                    string dir = Path.GetDirectoryName(logPath);
                    string name = Path.GetFileNameWithoutExtension(logPath);
                    string ext = Path.GetExtension(logPath);
                    int idx = 1;
                    string newName;
                    do
                    {
                        newName = Path.Combine(dir, $"{name}_{idx}{ext}");
                        idx++;
                    } while (File.Exists(newName));
                    try { File.Move(logPath, newName); } catch { /* 忽略重命名失败 */ }

                    // 重新打开原路径的文件（作为新文件）
                    var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    logWriter = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
                    logWriter.WriteLine($"[RuntimeLogExporter] Rotated at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                    logWriter.Flush();
                }
            }
            catch (Exception)
            {
                // 旋转失败不抛出
            }
        }

        private static void OnApplicationQuit()
        {
            CloseWriter();
        }

        private static void CloseWriter()
        {
            // 注销事件
            try
            {
#if UNITY_EDITOR
                Application.logMessageReceived -= HandleLog;
#else
                Application.logMessageReceivedThreaded -= HandleLog;
#endif
                Application.quitting -= OnApplicationQuit;
            }
            catch { }

            CloseWriterInternal();
            isInitialized = false;
        }

        private static void CloseWriterInternal()
        {
            lock (_writeLock)
            {
                try
                {
                    if (logWriter != null)
                    {
                        try
                        {
                            logWriter.WriteLine($"\n[RuntimeLogExporter] Ended at {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                            logWriter.Flush();
                        }
                        catch { /* 忽略写入结束信息的失败 */ }

                        try { logWriter.Close(); } catch { }
                        try { logWriter.Dispose(); } catch { }
                        logWriter = null;
                    }
                }
                catch { /* 最后的兜底 */ }
            }
        }

        private static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            foreach (var c in Path.GetInvalidPathChars())
                path = path.Replace(c.ToString(), "_");
            return path;
        }
    }
}