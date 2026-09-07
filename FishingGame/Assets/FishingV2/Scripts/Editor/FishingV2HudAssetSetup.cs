#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    /// <summary>
    /// Applies portfolio HUD artwork without coupling gameplay rules to the image assets.
    /// Missing icons keep the runtime-generated portrait fallback.
    /// </summary>
    public static class FishingV2HudAssetSetup
    {
        private const string ScenePath = "Assets/FishingV2/Scenes/FishingV2Prototype.unity";
        private const string ArtRoot = "Assets/FishingV2/Art/Hud/";

        [MenuItem("Fishing V2/Apply HUD artwork", priority = 35)]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            Dictionary<string, string> icons = new Dictionary<string, string>
            {
                { "anchovy", ArtRoot + "Hud_Anchovy.png" },
                { "salmon", ArtRoot + "Hud_Salmon.png" },
                { "mahi", ArtRoot + "Hud_Mahi.png" },
                { "squid", ArtRoot + "Hud_Squid.png" },
                { "tuna", ArtRoot + "Hud_Tuna.png" }
            };
            Dictionary<string, float> iconScales = new Dictionary<string, float>
            {
                { "anchovy", 0.58f },
                { "salmon", 0.78f },
                { "mahi", 0.86f },
                { "squid", 0.82f },
                { "tuna", 1.00f }
            };

            foreach (KeyValuePair<string, string> entry in icons)
            {
                ConfigureTexture(entry.Value, true);
                FishSpeciesAsset species = AssetDatabase.LoadAssetAtPath<FishSpeciesAsset>(
                    "Assets/FishingV2/Data/Fish_" + entry.Key + ".asset");
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Value);
                if (species == null || species.Data == null || icon == null)
                {
                    Debug.LogError("Fishing V2 HUD artwork mapping failed: " + entry.Key + " -> " + entry.Value);
                    continue;
                }

                species.Data.HudIcon = icon;
                species.Data.HudIconScale = iconScales[entry.Key];
                EditorUtility.SetDirty(species);
            }

            string bagPath = ArtRoot + "CatchBag.png";
            ConfigureTexture(bagPath, false);
            Texture2D bag = AssetDatabase.LoadAssetAtPath<Texture2D>(bagPath);
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath);
            FishingV2Session session = Object.FindFirstObjectByType<FishingV2Session>();
            if (session == null || bag == null)
            {
                Debug.LogError("Fishing V2 catch bag artwork could not be assigned.");
            }
            else
            {
                session.CatchBagTexture = bag;
                EditorUtility.SetDirty(session);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Fishing V2 HUD artwork applied: five species icons and catch bag.");
        }

        private static void ConfigureTexture(string path, bool sprite)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
#endif
