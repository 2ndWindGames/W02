#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Fishing.V2.EditorTools
{
    public static class FishingV2SetupWizard
    {
        private const string Root = "Assets/FishingV2";
        private const string DataRoot = Root + "/Data";
        private const string SceneRoot = Root + "/Scenes";

        [MenuItem("Fishing V2/Create default data", priority = 10)]
        public static void CreateDefaultData()
        {
            EnsureFolder(Root);
            EnsureFolder(DataRoot);
            EnsureFolder(SceneRoot);
            EnsureUrpPipeline();

            FishingV2TuningAsset tuning = LoadOrCreate<FishingV2TuningAsset>(DataRoot + "/FishingV2Tuning.asset");
            if (tuning != null)
            {
                EditorUtility.SetDirty(tuning);
            }

            var defaults = FishingV2Catalog.CreateDefaults();
            FishSpeciesAsset[] speciesAssets = new FishSpeciesAsset[defaults.Count];
            for (int i = 0; i < defaults.Count; i++)
            {
                FishSpeciesConfig config = defaults[i];
                string path = DataRoot + "/Fish_" + config.SpeciesId + ".asset";
                FishSpeciesAsset asset = AssetDatabase.LoadAssetAtPath<FishSpeciesAsset>(path);
                if (asset == null)
                {
                    if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                    asset = ScriptableObject.CreateInstance<FishSpeciesAsset>();
                    asset.Data = config;
                    AssetDatabase.CreateAsset(asset, path);
                }
                else
                {
                    asset.Data = config;
                    EditorUtility.SetDirty(asset);
                }

                speciesAssets[i] = asset;
            }

            string spotPath = DataRoot + "/Spot_Beach.asset";
            if (File.Exists(spotPath)) AssetDatabase.DeleteAsset(spotPath);
            FishingSpotAsset spot = ScriptableObject.CreateInstance<FishingSpotAsset>();
            AssetDatabase.CreateAsset(spot, spotPath);
            spot.SpotId = "beach";
            spot.DisplayName = "해변";
            spot.SessionSeconds = 90f;
            spot.Species = speciesAssets;
            EditorUtility.SetDirty(spot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = spot;
            Debug.Log("Fishing V2: 기본 튜닝·5종 어종·해변 낚시터 데이터를 생성했습니다.");
        }

        [MenuItem("Fishing V2/Create validation all-species data", priority = 15)]
        public static void CreateValidationData()
        {
            EnsureFolder(Root);
            EnsureFolder(DataRoot);
            EnsureFolder(SceneRoot);
            EnsureUrpPipeline();

            List<FishSpeciesConfig> defaults = FishingV2Catalog.CreateValidationDefaults();
            FishSpeciesAsset[] speciesAssets = new FishSpeciesAsset[defaults.Count];
            for (int i = 0; i < defaults.Count; i++)
            {
                FishSpeciesConfig config = defaults[i];
                string path = DataRoot + "/Fish_" + config.SpeciesId + ".asset";
                FishSpeciesAsset asset = AssetDatabase.LoadAssetAtPath<FishSpeciesAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<FishSpeciesAsset>();
                    asset.Data = config;
                    AssetDatabase.CreateAsset(asset, path);
                }
                else
                {
                    asset.Data = config;
                    EditorUtility.SetDirty(asset);
                }

                speciesAssets[i] = asset;
            }

            string spotPath = DataRoot + "/Spot_Validation_AllSpecies.asset";
            FishingSpotAsset spot = AssetDatabase.LoadAssetAtPath<FishingSpotAsset>(spotPath);
            if (spot == null)
            {
                spot = ScriptableObject.CreateInstance<FishingSpotAsset>();
                AssetDatabase.CreateAsset(spot, spotPath);
            }

            spot.SpotId = "validation_all_species";
            spot.DisplayName = "검증 — 전체 어종";
            spot.SessionSeconds = 90f;
            spot.Species = speciesAssets;
            EditorUtility.SetDirty(spot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = spot;
            Debug.Log("Fishing V2: 실제 5종 + 검증용 3종 전체 어종 데이터를 생성/갱신했습니다.");
        }

        private static void EnsureUrpPipeline()
        {
            const string pipelinePath = DataRoot + "/FishingV2_URP.asset";
            const string rendererPath = DataRoot + "/FishingV2_URP_Renderer.asset";
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "FishingV2_URP_Renderer";
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null || pipeline.rendererDataList.Length == 0 || pipeline.rendererDataList[0] == null)
            {
                if (pipeline != null)
                {
                    AssetDatabase.DeleteAsset(pipelinePath);
                }

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "FishingV2_URP";
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        [MenuItem("Fishing V2/Create procedural mesh assets", priority = 20)]
        public static void CreateMeshAssets()
        {
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Art");

            FishSpeciesAsset[] assets = AssetDatabase.FindAssets("t:FishSpeciesAsset", new[] { DataRoot })
                .Length == 0 ? new FishSpeciesAsset[0] :
                System.Array.ConvertAll(AssetDatabase.FindAssets("t:FishSpeciesAsset", new[] { DataRoot }), guid =>
                    AssetDatabase.LoadAssetAtPath<FishSpeciesAsset>(AssetDatabase.GUIDToAssetPath(guid)));

            for (int i = 0; i < assets.Length; i++)
            {
                FishSpeciesAsset asset = assets[i];
                if (asset == null || asset.Data == null) continue;
                Mesh mesh = FishMeshBuilderV2.Build(asset.Data);
                string meshName = "Fish_" + asset.Data.SpeciesId;
                mesh.name = meshName;
                string path = Root + "/Art/" + meshName + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    existing.name = meshName;
                    Object.DestroyImmediate(mesh);
                    EditorUtility.SetDirty(existing);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Fishing V2: 절차 메시 에셋을 생성/갱신했습니다.");
        }

        [MenuItem("Fishing V2/Create prototype scene", priority = 30)]
        public static void CreatePrototypeScene()
        {
            CreateDefaultData();
            FishingV2TuningAsset tuning = AssetDatabase.LoadAssetAtPath<FishingV2TuningAsset>(DataRoot + "/FishingV2Tuning.asset");
            FishingSpotAsset spot = AssetDatabase.LoadAssetAtPath<FishingSpotAsset>(DataRoot + "/Spot_Beach.asset");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrototypeCamera();
            CreatePrototypeLight();
            GameObject sessionObject = new GameObject("FishingV2Session");
            FishingV2Session session = sessionObject.AddComponent<FishingV2Session>();
            session.Tuning = tuning;
            session.Spot = spot;
            session.BeginOnStart = true;

            string scenePath = SceneRoot + "/FishingV2Prototype.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            AssetDatabase.Refresh();
            Selection.activeGameObject = sessionObject;
            Debug.Log("Fishing V2: 프로토타입 씬을 생성했습니다: " + scenePath);
        }

        private static void CreatePrototypeCamera()
        {
            GameObject cameraObject = new GameObject("FishingV2Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.transform.position = new Vector3(0f, 0f, 10f);
            camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.07f, 0.08f, 1f);
        }

        private static void CreatePrototypeLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f, 1f);
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

    }
}
#endif
