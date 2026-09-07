#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    /// <summary>
    /// v25 데이터와 절차 메시 채널을 에디터에서 빠르게 확인한다.
    /// 물 프레젠테이션 파일을 읽거나 수정하지 않으므로 물 브랜치와 독립적으로 실행할 수 있다.
    /// </summary>
    public static class FishingV2V25Validator
    {
        private static readonly string[] ExpectedPlayableSpecies =
        {
            "anchovy", "salmon", "mahi", "squid", "tuna"
        };

        private static readonly string[] ExpectedValidationSpecies =
        {
            "needle", "minnow", "disc"
        };

        [MenuItem("Fishing V2/Validate v25 catalog and mesh channels", priority = 50)]
        public static void ValidateV25CatalogAndMeshChannels()
        {
            List<FishSpeciesConfig> species = FishingV2Catalog.CreateValidationDefaults();
            HashSet<string> ids = new HashSet<string>();
            int errors = 0;
            int playableCount = 0;
            int validationCount = 0;
            HashSet<AfterBiteMode> afterBiteModes = new HashSet<AfterBiteMode>();

            for (int i = 0; i < species.Count; i++)
            {
                FishSpeciesConfig config = species[i];
                if (config == null)
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: null species config at index " + i);
                    continue;
                }

                if (string.IsNullOrEmpty(config.SpeciesId) || !ids.Add(config.SpeciesId))
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: duplicate or empty species id at index " + i);
                }

                if (config.ValidationOnly) validationCount++;
                else playableCount++;

                if (config.Visual == null || config.Feed == null || config.Motion == null ||
                    config.Contest == null || config.AfterBite == null)
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: incomplete data contract for " + config.SpeciesId);
                    continue;
                }

                afterBiteModes.Add(config.AfterBite.Mode);
                if (config.AfterBite.Duration.Min <= 0f || config.AfterBite.Duration.Max < config.AfterBite.Duration.Min ||
                    config.AfterBite.SpeedK < 0f || config.AfterBite.Cooldown.Min < 0f ||
                    config.AfterBite.Cooldown.Max < config.AfterBite.Cooldown.Min)
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: invalid After-Bite profile for " + config.SpeciesId);
                }

                Mesh mesh = FishMeshBuilderV2.Build(config);
                if (mesh == null || mesh.vertexCount == 0)
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: mesh build failed for " + config.SpeciesId);
                    continue;
                }

                Vector2[] uv0 = mesh.uv;
                Vector2[] uv1 = mesh.uv2;
                if (uv0 == null || uv0.Length != mesh.vertexCount || uv1 == null || uv1.Length != mesh.vertexCount)
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: UV channel count mismatch for " + config.SpeciesId);
                }
                else
                {
                    float maxU = 0f;
                    float maxRipple = 0f;
                    float minSeed = 1f;
                    float maxSeed = 0f;
                    for (int v = 0; v < mesh.vertexCount; v++)
                    {
                        maxU = Mathf.Max(maxU, uv0[v].x);
                        maxRipple = Mathf.Max(maxRipple, uv1[v].x);
                        minSeed = Mathf.Min(minSeed, uv0[v].y);
                        maxSeed = Mathf.Max(maxSeed, uv0[v].y);
                    }

                    if (maxU < 0.99f)
                    {
                        errors++;
                        Debug.LogError("Fishing V2 v25 validation: aU does not reach the tail for " + config.SpeciesId + " (max=" + maxU + ")");
                    }

                    if (maxRipple < 0.05f)
                    {
                        errors++;
                        Debug.LogError("Fishing V2 v25 validation: aRip is empty for " + config.SpeciesId);
                    }

                    if (config.SpeciesId == "squid" && maxSeed - minSeed < 0.20f)
                    {
                        errors++;
                        Debug.LogError("Fishing V2 v25 validation: squid arm seeds are not separated");
                    }
                }

                Object.DestroyImmediate(mesh);
            }

            ValidateExpectedIds(ids, ExpectedPlayableSpecies, false, ref errors);
            ValidateExpectedIds(ids, ExpectedValidationSpecies, true, ref errors);
            if (playableCount != ExpectedPlayableSpecies.Length)
            {
                errors++;
                Debug.LogError("Fishing V2 v25 validation: expected " + ExpectedPlayableSpecies.Length + " playable species, got " + playableCount);
            }

            if (validationCount != ExpectedValidationSpecies.Length)
            {
                errors++;
                Debug.LogError("Fishing V2 v25 validation: expected " + ExpectedValidationSpecies.Length + " validation species, got " + validationCount);
            }

            ValidateAfterBiteModes(afterBiteModes, ref errors);

            ValidateCatchMath(ref errors);

            if (errors == 0)
            {
                Debug.Log("Fishing V2 v25 validation passed: playable=" + playableCount + ", validation=" + validationCount);
            }
            else
            {
                Debug.LogError("Fishing V2 v25 validation failed with " + errors + " error(s).");
            }
        }

        private static void ValidateExpectedIds(HashSet<string> actual, string[] expected, bool validationOnly, ref int errors)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (!actual.Contains(expected[i]))
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: missing " + (validationOnly ? "validation" : "playable") + " species " + expected[i]);
                }
            }
        }

        private static void ValidateAfterBiteModes(HashSet<AfterBiteMode> actual, ref int errors)
        {
            AfterBiteMode[] expected =
            {
                AfterBiteMode.Peel,
                AfterBiteMode.Pass,
                AfterBiteMode.School,
                AfterBiteMode.Arc,
                AfterBiteMode.Jet
            };

            for (int i = 0; i < expected.Length; i++)
            {
                if (!actual.Contains(expected[i]))
                {
                    errors++;
                    Debug.LogError("Fishing V2 v25 validation: missing After-Bite mode " + expected[i]);
                }
            }
        }

        private static void ValidateCatchMath(ref int errors)
        {
            if (!FishingV2CatchMath.TrySweptMouthContact(
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f),
                0f,
                0f,
                Vector2.zero,
                0.05f,
                out Vector2 contact))
            {
                errors++;
                Debug.LogError("Fishing V2 v25 validation: swept-mouth contact probe failed");
            }

            Vector2 inward = FishingV2CatchMath.InwardNormal(Vector2.zero, Vector2.right, Vector2.up);
            if (Vector2.Dot(inward, Vector2.up) <= 0f)
            {
                errors++;
                Debug.LogError("Fishing V2 v25 validation: basket inward normal points outward");
            }

            float firstBounce = FishingV2CatchMath.BasketBounceHeight(0.23f);
            float secondBounce = FishingV2CatchMath.BasketBounceHeight(0.62f);
            if (firstBounce < 0.40f || secondBounce < 0.15f || secondBounce >= firstBounce)
            {
                errors++;
                Debug.LogError("Fishing V2 v25 validation: explicit two-bounce profile is invalid");
            }

            float liftScale = FishingV2CatchMath.LiftScale(3f, 0f, 1.5f);
            if (liftScale < 2.0f || liftScale > 2.25f)
            {
                errors++;
                Debug.LogError("Fishing V2 v25 validation: lift scale is outside the expected orthographic cue");
            }
        }
    }
}
#endif
