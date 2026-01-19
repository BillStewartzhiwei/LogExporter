using UnityEngine;
using System.IO;

namespace MyTools
{
    public enum LogNamingMode
    {
        ByDate,
        ByCount
    }

    [CreateAssetMenu(fileName = "RuntimeLogConfig", menuName = "RuntimeLogExporter/Config", order = 1)]
    public class RuntimeLogConfig : ScriptableObject
    {
        [Header("日志导出路径（留空则为默认 Logs 文件夹）")]
        public string exportDirectory = "";

        [Header("是否同时输出到控制台（仅编辑器；通常无需开启以避免递归）")]
        public bool alsoPrintToConsole = true;

        [Header("日志文件输出模式")]
        public LogNamingMode namingMode = LogNamingMode.ByDate;

        [Header("是否按大小滚动日志（如果启用会在超过阈值时创建新文件）")]
        public bool enableSizeRotation = false;
        [Tooltip("当 enableSizeRotation 为 true 时生效，单位 KB")]
        public int maxFileSizeKB = 1024;

        [Header("日志级别过滤")]
        public bool logInfo = true;
        public bool logWarning = true;
        public bool logError = true;
        public bool logException = true;

        [Header("是否在日志文件头包含设备/应用信息")]
        public bool includeDeviceInfo = true;

        // 返回最终用于存放日志的目录，如果未设置则使用 Application.persistentDataPath/Logs
        public string GetExportDirectory()
        {
            if (string.IsNullOrWhiteSpace(exportDirectory))
            {
                return Path.Combine(Application.persistentDataPath, "Logs");
            }
            return exportDirectory;
        }

        // 返回以字节为单位的最大文件大小（基于 maxFileSizeKB）
        public long GetMaxFileSizeBytes()
        {
            // 确保至少为 1 KB
            int kb = Mathf.Max(1, maxFileSizeKB);
            return (long)kb * 1024L;
        }

        // 根据 Unity 的 LogType 决定该日志是否应当被记录到文件
        public bool ShouldLog(LogType logType)
        {
            switch (logType)
            {
                case LogType.Log:
                    return logInfo;
                case LogType.Warning:
                    return logWarning;
                case LogType.Error:
                case LogType.Assert:
                    return logError;
                case LogType.Exception:
                    return logException;
                default:
                    return true;
            }
        }

        // 在编辑器或资源被修改时，做一些基本的约束和清理
        private void OnValidate()
        {
            // 至少 1 KB，避免意外设为 0
            if (maxFileSizeKB < 1) maxFileSizeKB = 1;
            // 上限设为 10 GB（以 KB 为单位）以防输入异常巨大值
            int maxAllowedKB = 10 * 1024 * 1024; // 10 * 1024 * 1024 KB = 10 GB
            if (maxFileSizeKB > maxAllowedKB) maxFileSizeKB = maxAllowedKB;

            if (exportDirectory != null)
            {
                exportDirectory = exportDirectory.Trim();
            }
        }
    }
}