#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    /// <summary>
    /// Creates the small, replaceable prototype substrate source used by WaterSurfaceV2.
    /// It is intentionally muted and tileable: the shader supplies scale variation and the
    /// second rotated sample so this source does not read as a repeated texture stamp.
    /// </summary>
    internal static class FishingV2BottomSubstrateGenerator
    {
        private const int TextureSize = 256;
        private const string AssetPath = "Assets/FishingV2/Art/BottomSubstrateOrganic.png";
        private static readonly Vector4[] StoneSeeds =
        {
            new Vector4(0.12f, 0.18f, 0.060f, 0.72f),
            new Vector4(0.31f, 0.76f, 0.044f, 0.28f),
            new Vector4(0.49f, 0.34f, 0.072f, 0.54f),
            new Vector4(0.68f, 0.17f, 0.050f, 0.40f),
            new Vector4(0.83f, 0.63f, 0.064f, 0.66f),
            new Vector4(0.93f, 0.34f, 0.038f, 0.22f),
            new Vector4(0.18f, 0.93f, 0.046f, 0.48f),
            new Vector4(0.72f, 0.91f, 0.042f, 0.34f)
        };

        [MenuItem("Fishing V2/Generate Organic Bottom Substrate")]
        private static void GenerateFromMenu()
        {
            Generate();
        }

        public static void Generate()
        {
            string absolutePath = Path.Combine(Application.dataPath, "FishingV2/Art/BottomSubstrateOrganic.png");
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };

            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize;
                    float v = (y + 0.5f) / TextureSize;
                    pixels[y * TextureSize + x] = BuildPixel(u, v);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 1;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            Debug.Log("Fishing V2 organic bottom substrate generated: " + AssetPath);
        }

        private static Color BuildPixel(float u, float v)
        {
            float macro = TileValueNoise(u, v, 4, 13);
            float broad = TileValueNoise(u + 0.17f, v - 0.11f, 7, 29);
            float breakup = TileValueNoise(u - 0.09f, v + 0.23f, 16, 47);
            float fine = TileValueNoise(u + 0.31f, v + 0.07f, 48, 71);

            float materialMix = Mathf.Clamp01(macro * 0.58f + broad * 0.30f + breakup * 0.12f);
            materialMix = Mathf.Clamp01((materialMix - 0.34f) * 2.40f + 0.34f);
            Color darkMud = new Color(0.008f, 0.016f, 0.012f, 1f);
            Color warmMud = new Color(0.580f, 0.420f, 0.200f, 1f);
            Color coolSilt = new Color(0.010f, 0.060f, 0.120f, 1f);
            Color color = Color.Lerp(darkMud, warmMud, materialMix);

            float coolPocket = Mathf.SmoothStep(0.62f, 0.92f, TileValueNoise(u + 0.41f, v - 0.29f, 6, 83));
            color = Color.Lerp(color, coolSilt, coolPocket * 0.22f);

            // A few soft, irregular stone inclusions keep the texture material-like without
            // turning it into a pebble field. Periodic distance makes the whole source tile.
            float stoneMask = 0f;
            float stoneCoreMask = 0f;
            for (int i = 0; i < StoneSeeds.Length; i++)
            {
                Vector4 seed = StoneSeeds[i];
                float centerX = seed.x;
                float centerY = seed.y;
                float radius = seed.z * 1.35f;
                float dx = PeriodicDistance(u, centerX);
                float dy = PeriodicDistance(v, centerY);
                float irregular = TileValueNoise(u + centerX, v + centerY, 18, 191 + i);
                float effectiveRadius = radius * Mathf.Lerp(0.78f, 1.16f, irregular) * Mathf.Lerp(0.90f, 1.10f, seed.w);
                float distance = Mathf.Sqrt(dx * dx * 1.35f + dy * dy);
                float stone = 1f - Mathf.SmoothStep(effectiveRadius * 0.54f, effectiveRadius, distance);
                float stoneCore = 1f - Mathf.SmoothStep(effectiveRadius * 0.20f, effectiveRadius * 0.66f, distance);
                stoneMask = Mathf.Max(stoneMask, stone);
                stoneCoreMask = Mathf.Max(stoneCoreMask, stoneCore);
            }

            Color stoneShadow = new Color(0.030f, 0.042f, 0.030f, 1f);
            Color softStone = new Color(0.720f, 0.610f, 0.350f, 1f);
            color = Color.Lerp(color, stoneShadow, stoneMask * 0.34f);
            color = Color.Lerp(color, softStone, stoneCoreMask * 0.76f);

            float darkPock = Mathf.SmoothStep(0.80f, 0.97f, TileValueNoise(u - 0.27f, v + 0.19f, 32, 227));
            color = Color.Lerp(color, new Color(0.004f, 0.010f, 0.008f, 1f), darkPock * 0.45f);
            float fineValue = (fine - 0.5f) * 0.055f;
            color += new Color(fineValue, fineValue, fineValue, 0f);
            // Encode the authored linear palette for the sRGB texture importer so the shader
            // receives the intended material values after sampling.
            color.r = Mathf.LinearToGammaSpace(Mathf.Clamp01(color.r));
            color.g = Mathf.LinearToGammaSpace(Mathf.Clamp01(color.g));
            color.b = Mathf.LinearToGammaSpace(Mathf.Clamp01(color.b));
            color.a = 1f;
            return color;
        }

        private static float TileValueNoise(float u, float v, int grid, int seed)
        {
            float x = u * grid;
            float y = v * grid;
            int x0 = Mod(Mathf.FloorToInt(x), grid);
            int y0 = Mod(Mathf.FloorToInt(y), grid);
            int x1 = Mod(x0 + 1, grid);
            int y1 = Mod(y0 + 1, grid);
            float tx = Smooth01(x - Mathf.Floor(x));
            float ty = Smooth01(y - Mathf.Floor(y));
            float a = Hash01(x0 + y0 * grid, seed);
            float b = Hash01(x1 + y0 * grid, seed);
            float c = Hash01(x0 + y1 * grid, seed);
            float d = Hash01(x1 + y1 * grid, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash01(int value, int seed)
        {
            float valueAsFloat = value * 0.75487766f + seed * 0.56984029f;
            return Mathf.Repeat(Mathf.Sin(valueAsFloat * 12.9898f) * 43758.5453f, 1f);
        }

        private static float PeriodicDistance(float a, float b)
        {
            float delta = Mathf.Abs(a - b);
            return Mathf.Min(delta, 1f - delta);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
#endif
