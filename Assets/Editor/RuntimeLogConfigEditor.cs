using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyTools
{
    [CustomEditor(typeof(RuntimeLogConfig))]
    public class RuntimeLogConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RuntimeLogConfig config = (RuntimeLogConfig)target;
            if (GUILayout.Button("Open Logs Folder"))
            {
                string path = string.IsNullOrEmpty(config.exportDirectory)
                    ? Path.Combine(Application.dataPath, "../Logs")
                    : config.exportDirectory;
                path = Path.GetFullPath(path);
                EditorUtility.RevealInFinder(path);
            }
        }
    }
}