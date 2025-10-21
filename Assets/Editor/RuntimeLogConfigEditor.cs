#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using MyTools;
namespace MyTools.Editor
{
    public static class RuntimeLogConfigEditor
    {
        [MenuItem("Tools/Runtime Log Exporter/Create Config Asset")]
        public static void CreateConfigAsset()
        {
            string dir = "Assets/Plugins/RuntimeLogExporter/Resources";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string assetPath = Path.Combine(dir, "RuntimeLogConfig.asset");
            if (File.Exists(assetPath))
            {
                Debug.LogWarning("RuntimeLogConfig.asset already exists.");
                return;
            }

            var config = ScriptableObject.CreateInstance<RuntimeLogConfig>();
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Created RuntimeLogConfig.asset at: " + assetPath);
        }
    }
}
#endif