using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// Static, presentation-only environment placeholders for the underwater observation
    /// slice. These meshes intentionally avoid gameplay colliders and can later be replaced
    /// by authored rock/plant assets without changing the session or fish simulation.
    /// </summary>
    public static class FishingV2EnvironmentV2
    {
        private struct RockSpec
        {
            public Vector2 Position;
            public Vector2 Size;
            public float Rotation;
            public float Seed;
            public float Alpha;

            public RockSpec(Vector2 position, Vector2 size, float rotation, float seed, float alpha)
            {
                Position = position;
                Size = size;
                Rotation = rotation;
                Seed = seed;
                Alpha = alpha;
            }
        }

        public static Transform Build(Transform parent, Rect pond, Material material, float strength)
        {
            GameObject root = new GameObject("FishingV2Environment");
            root.transform.SetParent(parent, false);

            float visualStrength = Mathf.Clamp(strength, 0f, 2f);
            Color rockColor = new Color(0.070f, 0.195f, 0.145f, 1f);
            Color rockHighlight = new Color(0.130f, 0.285f, 0.195f, 1f);
            Color plantColor = new Color(0.045f, 0.235f, 0.140f, 1f);

            // Keep the center open for the bobber and fish observation area. The placement
            // follows the pond rectangle rather than screen pixels, so camera changes remain
            // coherent with the world.
            RockSpec[] rocks =
            {
                new RockSpec(new Vector2(pond.xMin + 0.95f, pond.yMax - 0.68f), new Vector2(1.18f, 0.34f), -0.18f, 1.2f, 0.34f),
                new RockSpec(new Vector2(pond.xMax - 0.82f, pond.yMax - 0.92f), new Vector2(0.92f, 0.28f), 0.24f, 4.7f, 0.28f),
                new RockSpec(new Vector2(pond.xMin + 0.78f, pond.yMin + 0.72f), new Vector2(1.45f, 0.42f), 0.12f, 8.1f, 0.42f),
                new RockSpec(new Vector2(pond.xMax - 0.92f, pond.yMin + 0.78f), new Vector2(1.28f, 0.36f), -0.28f, 12.4f, 0.36f),
                new RockSpec(new Vector2(pond.xMax - 2.55f, pond.yMin + 0.38f), new Vector2(0.72f, 0.22f), 0.36f, 18.2f, 0.24f),
                new RockSpec(new Vector2(pond.xMin + 2.10f, pond.yMax - 0.28f), new Vector2(0.68f, 0.20f), -0.42f, 22.5f, 0.22f)
            };

            for (int i = 0; i < rocks.Length; i++)
            {
                RockSpec rock = rocks[i];
                CreateRock(root.transform, material, rock, rockColor, rockHighlight, visualStrength);
            }

            CreateGrassClump(root.transform, material, new Vector2(pond.xMin + 0.34f, pond.yMin + 1.55f), 0.92f, -0.30f, plantColor, visualStrength);
            CreateGrassClump(root.transform, material, new Vector2(pond.xMin + 0.42f, pond.yMax - 1.70f), 0.76f, 0.28f, plantColor, visualStrength);
            CreateGrassClump(root.transform, material, new Vector2(pond.xMax - 0.36f, pond.yMin + 1.40f), 0.86f, 0.34f, plantColor, visualStrength);
            CreateGrassClump(root.transform, material, new Vector2(pond.xMax - 0.44f, pond.yMax - 1.56f), 0.72f, -0.25f, plantColor, visualStrength);
            CreateGrassClump(root.transform, material, new Vector2(pond.center.x + 3.85f, pond.yMin + 0.35f), 0.58f, 0.18f, plantColor, visualStrength * 0.82f);
            CreateGrassClump(root.transform, material, new Vector2(pond.center.x - 3.10f, pond.yMin + 0.24f), 0.68f, 0.16f, plantColor, visualStrength * 0.90f);
            CreateGrassClump(root.transform, material, new Vector2(pond.center.x + 1.90f, pond.yMin + 0.18f), 0.54f, -0.12f, plantColor, visualStrength * 0.78f);

            // 연못 바깥 장식. 연출에서 카메라가 뒤로 빠지면 게임플레이 사각형 너머까지
            // 보이는데, 거기가 비어 있으면 물이 아니라 "맵이 끝난 자리"로 읽힌다.
            // 게임플레이에는 안 보이는 영역이고 콜라이더도 없으므로 순수 장식이다.
            RockSpec[] outerRocks =
            {
                new RockSpec(new Vector2(pond.xMin - 2.10f, pond.yMax + 0.85f), new Vector2(1.60f, 0.46f), 0.22f, 27.3f, 0.26f),
                new RockSpec(new Vector2(pond.xMax + 2.45f, pond.yMax + 0.55f), new Vector2(1.32f, 0.38f), -0.31f, 31.8f, 0.22f),
                new RockSpec(new Vector2(pond.xMin - 3.30f, pond.yMin - 0.70f), new Vector2(1.85f, 0.52f), -0.14f, 36.1f, 0.24f),
                new RockSpec(new Vector2(pond.xMax + 3.05f, pond.yMin - 0.95f), new Vector2(1.55f, 0.44f), 0.27f, 41.6f, 0.20f),
                new RockSpec(new Vector2(pond.center.x - 4.60f, pond.yMax + 1.35f), new Vector2(1.10f, 0.32f), 0.08f, 45.2f, 0.18f),
                new RockSpec(new Vector2(pond.center.x + 3.90f, pond.yMin - 1.55f), new Vector2(1.24f, 0.36f), -0.19f, 49.7f, 0.19f),
                new RockSpec(new Vector2(pond.xMin - 4.80f, pond.center.y + 0.40f), new Vector2(1.42f, 0.40f), 0.33f, 53.4f, 0.16f),
                new RockSpec(new Vector2(pond.xMax + 4.55f, pond.center.y - 0.65f), new Vector2(1.36f, 0.39f), -0.24f, 58.9f, 0.17f)
            };

            for (int i = 0; i < outerRocks.Length; i++)
            {
                // 바깥은 더 멀고 탁하다. 안쪽과 같은 세기로 그리면 연못 경계가 사라진다.
                CreateRock(root.transform, material, outerRocks[i], rockColor, rockHighlight, visualStrength * 0.72f);
            }

            CreateGrassClump(root.transform, material, new Vector2(pond.xMin - 1.55f, pond.yMin - 0.35f), 0.80f, 0.22f, plantColor, visualStrength * 0.62f);
            CreateGrassClump(root.transform, material, new Vector2(pond.xMax + 1.70f, pond.yMax + 0.25f), 0.74f, -0.18f, plantColor, visualStrength * 0.58f);
            CreateGrassClump(root.transform, material, new Vector2(pond.center.x - 5.90f, pond.yMin - 1.10f), 0.66f, 0.30f, plantColor, visualStrength * 0.55f);
            CreateGrassClump(root.transform, material, new Vector2(pond.center.x + 5.40f, pond.yMax + 1.05f), 0.70f, -0.26f, plantColor, visualStrength * 0.52f);

            return root.transform;
        }

        private static void CreateRock(
            Transform parent,
            Material material,
            RockSpec spec,
            Color rockColor,
            Color highlightColor,
            float strength)
        {
            GameObject rockObject = new GameObject("SubmergedRock");
            rockObject.transform.SetParent(parent, false);
            rockObject.transform.localPosition = new Vector3(spec.Position.x, spec.Position.y, -0.045f);
            rockObject.transform.localRotation = Quaternion.Euler(0f, 0f, spec.Rotation * Mathf.Rad2Deg);

            MeshFilter filter = rockObject.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildRockMesh(spec.Size, spec.Seed);
            MeshRenderer renderer = rockObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            SetColor(renderer, "_ShadowColor", rockColor, spec.Alpha * strength);

            // A smaller offset plane gives each placeholder rock a quiet top plane instead
            // of a flat black decal, while remaining low contrast against the water.
            GameObject highlightObject = new GameObject("SubmergedRockTop");
            highlightObject.transform.SetParent(rockObject.transform, false);
            highlightObject.transform.localPosition = new Vector3(-spec.Size.x * 0.10f, spec.Size.y * 0.10f, 0.006f);
            highlightObject.transform.localScale = new Vector3(0.58f, 0.52f, 1f);
            MeshFilter highlightFilter = highlightObject.AddComponent<MeshFilter>();
            highlightFilter.sharedMesh = filter.sharedMesh;
            MeshRenderer highlightRenderer = highlightObject.AddComponent<MeshRenderer>();
            highlightRenderer.sharedMaterial = material;
            SetColor(highlightRenderer, "_ShadowColor", highlightColor, spec.Alpha * strength * 0.55f);
        }

        private static Mesh BuildRockMesh(Vector2 size, float seed)
        {
            const int points = 11;
            List<Vector3> vertices = new List<Vector3>(points + 1);
            List<Vector3> normals = new List<Vector3>(points + 1);
            List<int> triangles = new List<int>(points * 3);
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.forward);

            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2f;
                float variation = 0.84f + 0.12f * Mathf.Sin(seed * 1.7f + i * 2.31f) + 0.05f * Mathf.Sin(seed * 3.1f - i * 0.83f);
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * size.x * variation,
                    Mathf.Sin(angle) * size.y * variation,
                    0f));
                normals.Add(Vector3.forward);
            }

            for (int i = 0; i < points; i++)
            {
                triangles.Add(0);
                triangles.Add(1 + i);
                triangles.Add(1 + (i + 1) % points);
            }

            Mesh mesh = new Mesh { name = "FishingV2_SubmergedRock" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateGrassClump(
            Transform parent,
            Material material,
            Vector2 position,
            float height,
            float lean,
            Color color,
            float strength)
        {
            GameObject grassObject = new GameObject("SubmergedGrass");
            grassObject.transform.SetParent(parent, false);
            grassObject.transform.localPosition = new Vector3(position.x, position.y, -0.028f);

            List<Vector3> vertices = new List<Vector3>(36);
            List<Vector3> normals = new List<Vector3>(36);
            List<int> triangles = new List<int>(54);
            const int blades = 7;

            for (int i = 0; i < blades; i++)
            {
                float t = blades == 1 ? 0f : i / (float)(blades - 1) * 2f - 1f;
                float bladeHeight = height * (0.60f + 0.34f * (1f - Mathf.Abs(t) * 0.52f));
                float baseX = t * height * 0.18f;
                float tipX = baseX + lean * bladeHeight * (0.50f + 0.16f * t);
                float width = height * (0.018f + 0.006f * (1f - Mathf.Abs(t)));
                AddBlade(vertices, normals, triangles, new Vector2(baseX, 0f), new Vector2(tipX, bladeHeight), width);
            }

            Mesh mesh = new Mesh { name = "FishingV2_SubmergedGrass" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();

            MeshFilter filter = grassObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = grassObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            SetColor(renderer, "_ShadowColor", color, 0.44f * strength);
        }

        private static void AddBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector2 basePoint,
            Vector2 tipPoint,
            float width)
        {
            Vector2 direction = (tipPoint - basePoint).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            int start = vertices.Count;
            vertices.Add(new Vector3(basePoint.x - perpendicular.x * width, basePoint.y - perpendicular.y * width, 0f));
            vertices.Add(new Vector3(basePoint.x + perpendicular.x * width, basePoint.y + perpendicular.y * width, 0f));
            vertices.Add(new Vector3(tipPoint.x + perpendicular.x * width * 0.16f, tipPoint.y + perpendicular.y * width * 0.16f, 0f));
            vertices.Add(new Vector3(tipPoint.x - perpendicular.x * width * 0.16f, tipPoint.y - perpendicular.y * width * 0.16f, 0f));
            normals.Add(Vector3.forward);
            normals.Add(Vector3.forward);
            normals.Add(Vector3.forward);
            normals.Add(Vector3.forward);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void SetColor(Renderer renderer, string property, Color color, float alpha)
        {
            if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(property))
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            color.a = Mathf.Clamp01(alpha);
            block.SetColor(property, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
