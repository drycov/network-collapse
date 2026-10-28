#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace NetworkCollapse.Editor
{
    public static class FreeAssetImporter
    {
        private const string SciFiUrl = "https://lpc.opengameart.org/sites/default/files/kenney_ui-pack-scifi.zip";
        private const string AudioUrl = "https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip";
        private const string Theme = "Assets/Resources/NetworkTheme";
        private const string Audio = "Assets/Resources/Audio";

        [MenuItem("Network Collapse/Import Free CC0 Assets")]
        public static void Import()
        {
            if (!EditorUtility.DisplayDialog("Import Kenney CC0 assets",
                "Download Kenney UI Pack Sci-Fi and Interface Sounds from OpenGameArt (CC0) and add selected PNG/OGG files to this project?", "Import", "Cancel")) return;
            try
            {
                Directory.CreateDirectory(Theme);
                Directory.CreateDirectory(Audio);
                ImportPack(SciFiUrl, true);
                ImportPack(AudioUrl, false);
                AssetDatabase.Refresh();
                Debug.Log("Kenney CC0 assets imported. Check Assets/Resources/NetworkTheme and Assets/Resources/Audio.");
            }
            catch (Exception error) { Debug.LogError("Asset import failed: " + error); }
        }

        private static void ImportPack(string url, bool visuals)
        {
            using (var client = new WebClient())
            using (var stream = new MemoryStream(client.DownloadData(url)))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                    entry.Length < 4_000_000 &&
                    entry.Name.EndsWith(visuals ? ".png" : ".ogg", StringComparison.OrdinalIgnoreCase)).ToList();
                if (visuals)
                {
                    CopyOne(files, Theme + "/skin_panel.png", new[] { "panel", "window" });
                    CopyOne(files, Theme + "/skin_button.png", new[] { "button" });
                }
                else
                {
                    CopyOne(files, Audio + "/click.ogg", new[] { "click" });
                    CopyOne(files, Audio + "/select.ogg", new[] { "switch", "click" });
                    CopyOne(files, Audio + "/confirm.ogg", new[] { "confirmation", "confirm", "positive" });
                    CopyOne(files, Audio + "/error.ogg", new[] { "error", "negative", "close" });
                    CopyOne(files, Audio + "/alert.ogg", new[] { "notification", "error" });
                    CopyOne(files, Audio + "/mission.ogg", new[] { "confirmation", "open" });
                    CopyOne(files, Audio + "/achievement.ogg", new[] { "confirmation", "positive" });
                }
            }
        }

        private static void CopyOne(System.Collections.Generic.List<ZipArchiveEntry> files, string output, string[] hints)
        {
            var entry = files.FirstOrDefault(e => hints.Any(h => e.Name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0));
            if (entry == null) { Debug.LogWarning("No matching Kenney asset for " + output); return; }
            using (var input = entry.Open())
            using (var target = File.Create(output)) input.CopyTo(target);
            Debug.Log("Imported " + entry.FullName + " => " + output);
        }
    }
}
#endif
