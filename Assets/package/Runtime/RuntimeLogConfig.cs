using UnityEngine;

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
    }
}