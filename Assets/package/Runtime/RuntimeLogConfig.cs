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

        [Header("是否同时输出到控制台（仅编辑器）")]
        public bool alsoPrintToConsole = true;
        
        [Header("日志文件输出模式")]
        public LogNamingMode namingMode = LogNamingMode.ByDate;
        
        [Header("日志级别过滤")]
        public bool logInfo = true;
        public bool logWarning = true;
        public bool logError = true;
        public bool logException = true;
    }
}
