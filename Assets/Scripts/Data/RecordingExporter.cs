using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

namespace QuestGear3D.Scan.Data
{
    /// <summary>
    /// Handles exporting scan sessions to ZIP files and managing recordings.
    /// ZIP files are saved to the Quest Downloads folder for easy access.
    /// </summary>
    public class RecordingExporter : MonoBehaviour
    {
        [Header("Export Settings")]
        [Tooltip("Target folder for ZIP exports. Defaults to Download/Export/")]
        public string exportSubfolder = "Export";

        /// <summary>
        /// Returns true while an export is in progress.
        /// </summary>
        public bool IsExporting { get; private set; }

        /// <summary>
        /// Export progress from 0 to 1.
        /// </summary>
        public float ExportProgress { get; private set; }

        /// <summary>
        /// Path to the last exported ZIP file.
        /// </summary>
        public string LastExportPath { get; private set; }

        private string _scansRootPath;
        private string _exportRootPath;

        void Start()
        {
            _scansRootPath = Path.Combine(Application.persistentDataPath, "Scans");

#if UNITY_ANDROID && !UNITY_EDITOR
            // On Quest, export to the Downloads folder for easy access
            _exportRootPath = Path.Combine("/sdcard/Download", exportSubfolder);
#else
            _exportRootPath = Path.Combine(Application.persistentDataPath, exportSubfolder);
#endif

            if (!Directory.Exists(_exportRootPath))
            {
                Directory.CreateDirectory(_exportRootPath);
            }
        }

        /// <summary>
        /// Gets all available scan session directories.
        /// </summary>
        public string[] GetScanSessions()
        {
            if (!Directory.Exists(_scansRootPath))
                return Array.Empty<string>();

            return Directory.GetDirectories(_scansRootPath);
        }

        /// <summary>
        /// Exports a scan session to a ZIP file asynchronously.
        /// </summary>
        /// <param name="sessionPath">Full path to the scan session folder.</param>
        /// <param name="onComplete">Callback with the ZIP file path on success, null on failure.</param>
        public void ExportSessionToZip(string sessionPath, Action<string> onComplete = null)
        {
            if (IsExporting)
            {
                Debug.LogWarning("[RecordingExporter] Export already in progress!");
                return;
            }

            if (!Directory.Exists(sessionPath))
            {
                Debug.LogError($"[RecordingExporter] Session path does not exist: {sessionPath}");
                onComplete?.Invoke(null);
                return;
            }

            string sessionName = new DirectoryInfo(sessionPath).Name;
            string zipPath = Path.Combine(_exportRootPath, $"{sessionName}.zip");

            IsExporting = true;
            ExportProgress = 0f;

            // Run in background thread to avoid blocking
            Task.Run(() =>
            {
                try
                {
                    // Delete existing zip if present
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    // Count total files for progress
                    string[] allFiles = Directory.GetFiles(sessionPath, "*", SearchOption.AllDirectories);
                    int totalFiles = allFiles.Length;
                    int processedFiles = 0;

                    using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        foreach (string filePath in allFiles)
                        {
                            string relativePath = filePath.Substring(sessionPath.Length + 1);
                            zipArchive.CreateEntryFromFile(filePath, relativePath, System.IO.Compression.CompressionLevel.Fastest);

                            processedFiles++;
                            ExportProgress = (float)processedFiles / totalFiles;
                        }
                    }

                    LastExportPath = zipPath;
                    Debug.Log($"[RecordingExporter] Export complete: {zipPath} ({totalFiles} files)");

                    // Invoke callback on main thread
                    UnityMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(zipPath));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RecordingExporter] Export failed: {e.Message}");
                    UnityMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(null));
                }
                finally
                {
                    IsExporting = false;
                    ExportProgress = 1f;
                }
            });
        }

        /// <summary>
        /// Deletes a scan session and all its data.
        /// </summary>
        /// <param name="sessionPath">Full path to the scan session folder.</param>
        /// <returns>True if deletion succeeded.</returns>
        public bool DeleteSession(string sessionPath)
        {
            try
            {
                if (Directory.Exists(sessionPath))
                {
                    Directory.Delete(sessionPath, true);
                    Debug.Log($"[RecordingExporter] Deleted session: {sessionPath}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[RecordingExporter] Session not found: {sessionPath}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RecordingExporter] Delete failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the size of a scan session in bytes.
        /// </summary>
        public long GetSessionSize(string sessionPath)
        {
            if (!Directory.Exists(sessionPath)) return 0;

            long totalSize = 0;
            foreach (var file in Directory.GetFiles(sessionPath, "*", SearchOption.AllDirectories))
            {
                totalSize += new FileInfo(file).Length;
            }
            return totalSize;
        }

        /// <summary>
        /// Formats a byte size into a human-readable string (KB, MB, GB).
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// Simple main thread dispatcher for callbacks from background threads.
    /// </summary>
    public static class UnityMainThreadDispatcher
    {
        private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue
            = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        private static bool _initialized = false;

        public static void Enqueue(Action action)
        {
            _queue.Enqueue(action);

            if (!_initialized)
            {
                var go = new GameObject("MainThreadDispatcher");
                go.AddComponent<MainThreadDispatcherBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(go);
                _initialized = true;
            }
        }

        private class MainThreadDispatcherBehaviour : MonoBehaviour
        {
            void Update()
            {
                while (_queue.TryDequeue(out Action action))
                {
                    action?.Invoke();
                }
            }
        }
    }
}
