using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in import setup for approved 4K terrain derived from four-times-larger masters.</summary>
[InitializeOnLoad]
internal static class TerrainResolutionPreparation
{
    private const string Marker = "Temp/ToxicBarrierTerrain.prepare";
    static TerrainResolutionPreparation() { EditorApplication.update += Prepare; }
    private static void Prepare()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker)) return;
        File.Delete(Marker);
        try
        {
            string report = "PASS\n";
            for (int region = 2; region <= 3; region++)
            {
                string path = $"Assets/Sprites/Sprites_map_{region}.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (width != 4096 || height != 4096) throw new Exception("Unexpected terrain source dimensions: " + path);
                importer.spritePixelsPerUnit = width; // Original: 1254 pixels / 1254 PPU = one local unit.
                importer.maxTextureSize = 4096;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.maxTextureSize = 4096;
                defaults.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(defaults);
                var desktop = importer.GetPlatformTextureSettings("Standalone");
                desktop.overridden = true;
                desktop.maxTextureSize = 4096;
                desktop.format = TextureImporterFormat.BC7;
                desktop.textureCompression = TextureImporterCompression.CompressedHQ;
                desktop.compressionQuality = 100;
                desktop.crunchedCompression = false;
                importer.SetPlatformTextureSettings(desktop);
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite.texture.width != 4096 || sprite.texture.height != 4096 || sprite.texture.format != TextureFormat.BC7 ||
                    Mathf.Abs(sprite.rect.width / sprite.pixelsPerUnit - 1) > .0001f)
                    throw new Exception($"Terrain import mismatch: {path}; {sprite.texture.width}x{sprite.texture.height}, " +
                        $"PPU {sprite.pixelsPerUnit}, rect {sprite.rect}, format {sprite.texture.format}, " +
                        $"active target {EditorUserBuildSettings.activeBuildTarget}, GPU BC7 {SystemInfo.SupportsTextureFormat(TextureFormat.BC7)}");
                report += $"Region {region}: {sprite.texture.width}x{sprite.texture.height}, PPU {sprite.pixelsPerUnit}, " +
                    $"{sprite.texture.format}, mip levels {sprite.texture.mipmapCount}, one local unit; GUID unchanged.\n";
            }
            File.WriteAllText("Temp/ToxicBarrierTerrain.result", report);
        }
        catch (Exception error) { File.WriteAllText("Temp/ToxicBarrierTerrain.result", "FAIL\n" + error); }
    }
}
