using System;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class FileOutputHelper
    {
        public const int TEXT_THRESHOLD = 81920; // 80KB

        private static string _outputDir;

        public static string OutputDir
        {
            get
            {
                if (_outputDir == null)
                {
                    _outputDir = Path.Combine(Application.dataPath, "..", "Temp", "MCP");
                    Directory.CreateDirectory(_outputDir);
                }
                return _outputDir;
            }
        }

        public static string WriteText(string content, string prefix = "output")
        {
            Cleanup();
            var path = Path.Combine(OutputDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, content);
            return path;
        }

        public static string WritePng(byte[] pngData, string prefix = "screenshot")
        {
            Cleanup();
            var path = Path.Combine(OutputDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(path, pngData);
            return path;
        }

        public static void Cleanup()
        {
            if (!Directory.Exists(OutputDir)) return;
            var cutoff = DateTime.Now.AddHours(-1);
            foreach (var file in Directory.GetFiles(OutputDir))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    try { File.Delete(file); } catch { /* ignore */ }
                }
            }
        }
    }
}
