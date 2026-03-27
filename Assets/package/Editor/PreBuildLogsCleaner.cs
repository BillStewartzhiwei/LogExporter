#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MyTools.Editor
{
    /// <summary>
    /// Clears project Logs content before each build starts.
    /// </summary>
    public class PreBuildLogsCleaner : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logsPath = Path.Combine(projectRoot, "Logs");

            if (!Directory.Exists(logsPath))
            {
                return;
            }

            try
            {
                var dirInfo = new DirectoryInfo(logsPath);
                int deletedFiles = 0;
                int deletedDirs = 0;

                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    try
                    {
                        file.IsReadOnly = false;
                        file.Delete();
                        deletedFiles++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[PreBuildLogsCleaner] Skip file: {file.FullName}, reason: {ex.Message}");
                    }
                }

                foreach (DirectoryInfo dir in dirInfo.GetDirectories())
                {
                    try
                    {
                        dir.Delete(true);
                        deletedDirs++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[PreBuildLogsCleaner] Skip directory: {dir.FullName}, reason: {ex.Message}");
                    }
                }

                Debug.Log($"[PreBuildLogsCleaner] Logs cleanup finished before build: {logsPath} (files: {deletedFiles}, dirs: {deletedDirs})");
            }
            catch (Exception ex)
            {
                // Never block build for cleanup issues.
                Debug.LogWarning($"[PreBuildLogsCleaner] Failed to clear Logs directory: {ex.Message}");
            }
        }
    }
}
#endif
