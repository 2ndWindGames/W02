using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// fishgen.py의 메시 생성 규칙을 런타임에서도 사용할 수 있게 옮긴 빌더다.
    /// 실제 프로젝트에서는 이 결과를 에디터에서 Mesh asset으로 굽고, 첫 수직 슬라이스에서는
    /// 에디터 설치 없이도 확인할 수 있도록 같은 코드를 런타임 생성에 사용한다.
    /// </summary>
    public static class FishMeshBuilderV2
    {
        private const int Rings = 40;
        private const int Segments = 24;
        private const float BodyXScale = 0.80f;

        private struct VertexData
        {
            public Vector3 Position;
            public float U;
            public float Seed;
            public float Ripple;

            public VertexData(Vector3 position, float u, float seed, float ripple)
            {
                Position = position;
                U = u;
                Seed = seed;
                Ripple = ripple;
            }
        }

        public static Mesh Build(FishSpeciesConfig species)
        {
            return Build(species, FishingV2PresentationSettings.For(FishingV2PresentationVariant.CasualFishing));
        }

        public static Mesh Build(FishSpeciesConfig species, FishingV2PresentationSettings presentation)
        {
            if (species == null || species.Visual == null)
            {
                return BuildFallback("FishV2_Fallback");
            }

            FishVisualSpec visual = species.Visual;
            List<Vector3> positions = new List<Vector3>(2200);
            List<int> triangles = new List<int>(5200);
            List<Color> colors = new List<Color>(2200);
            List<Vector2> uv0 = new List<Vector2>(2200);
            List<Vector2> uv1 = new List<Vector2>(2200);

            float length = Mathf.Max(0.05f, visual.Length);
            float maxWidth = Mathf.Max(0.001f, visual.MaxWidth);
            float maxHeight = Mathf.Max(0.001f, visual.MaxHeight);

            for (int i = 0; i <= Rings; i++)
            {
                float u = i / (float)Rings;
                float width = Interpolate(visual.WidthProfile, u) * maxWidth;
                float height = Interpolate(visual.HeightProfile, u) * maxHeight;
                float x = BodyX(u, length);

                for (int j = 0; j < Segments; j++)
                {
                    float angle = j / (float)Segments * Mathf.PI * 2f;
                    float top = Mathf.Max(0f, Mathf.Sin(angle));
                    float shade = Mathf.Pow(top, 0.42f);
                    Color color = Color.Lerp(visual.EdgeColor, visual.BaseColor, shade);

                    StripeSpec stripes = visual.Stripes;
                    if (stripes != null && stripes.Count > 0 && u >= stripes.U0 && u <= stripes.U1)
                    {
                        float stripePosition = (u - stripes.U0) / Mathf.Max(0.0001f, stripes.U1 - stripes.U0) * stripes.Count;
                        float stripeBand = Mathf.Abs(Mathf.Repeat(stripePosition, 1f) - 0.5f) * 2f;
                        if (stripeBand > 1f - stripes.Width)
                        {
                            float amount = stripes.Amount * shade;
                            color = ScaleColor(color, 1f - amount);
                        }
                    }

                    if (visual.Spots && StableHash01(i / 2, j / 2) > 0.62f && top > 0.35f)
                    {
                        color = ScaleColor(color, 0.55f);
                    }

                    Push(positions, colors, uv0, uv1,
                        new VertexData(new Vector3(x, Mathf.Cos(angle) * width, Mathf.Sin(angle) * height), u, 0f, 0f),
                        color);
                }
            }

            for (int i = 0; i < Rings; i++)
            {
                for (int j = 0; j < Segments; j++)
                {
                    int a = i * Segments + j;
                    int b = i * Segments + (j + 1) % Segments;
                    int c = (i + 1) * Segments + (j + 1) % Segments;
                    int d = (i + 1) * Segments + j;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            AddCap(positions, colors, uv0, uv1, triangles, 0, false, length, visual.BaseColor);
            AddCap(positions, colors, uv0, uv1, triangles, Rings, true, length, visual.BaseColor);

            if (visual.Arms != null && visual.Arms.Count > 0)
            {
                AddArms(species, presentation, positions, colors, uv0, uv1, triangles);
            }
            else
            {
                AddTail(species, presentation, positions, colors, uv0, uv1, triangles);
            }

            AddEyes(species, positions, colors, uv0, uv1, triangles);
            AddPectoralFins(species, positions, colors, uv0, uv1, triangles);
            AddFinlets(species, positions, colors, uv0, uv1, triangles);
            AddDorsal(species, positions, colors, uv0, uv1, triangles);

            Mesh mesh = new Mesh();
            mesh.name = "FishV2_" + species.SpeciesId;
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Returns the local +X contact point used by the v25 swept-mouth test. Most species
        /// derive it from the generated geometry; squid explicitly overrides it to the arm
        /// bundle because the mantle swims ahead of the actual catch point.
        /// </summary>
        public static float GetMouthOffset(FishSpeciesConfig species, Mesh mesh)
        {
            if (species != null && species.Visual != null && species.Visual.HasMouthOffsetOverride)
            {
                return species.Visual.MouthOffset;
            }

            if (mesh == null || mesh.vertexCount == 0)
            {
                return species != null && species.Visual != null ? species.Visual.Length * 0.45f : 0f;
            }

            Vector3[] vertices = mesh.vertices;
            float maxX = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                maxX = Mathf.Max(maxX, vertices[i].x);
            }

            return float.IsNegativeInfinity(maxX) ? 0f : maxX;
        }

        public static Mesh BuildFallback(string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.35f, -0.14f, 0f),
                new Vector3(-0.35f, 0.14f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.colors = new[] { Color.white, Color.white, Color.white };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f) };
            mesh.uv2 = new[] { Vector2.zero, Vector2.zero, Vector2.zero };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles,
            int ring,
            bool tail,
            float length,
            Color color)
        {
            int center = positions.Count;
            float u = tail ? 1f : 0f;
            float x = BodyX(u, length) + (tail ? 0f : -0.02f * length);
            Push(positions, colors, uv0, uv1, new VertexData(new Vector3(x, 0f, 0f), u, 0f, 0f), color);

            for (int j = 0; j < Segments; j++)
            {
                int a = ring * Segments + j;
                int b = ring * Segments + (j + 1) % Segments;
                if (tail)
                {
                    triangles.Add(center);
                    triangles.Add(a);
                    triangles.Add(b);
                }
                else
                {
                    triangles.Add(center);
                    triangles.Add(b);
                    triangles.Add(a);
                }
            }
        }

        private static void AddArms(
            FishSpeciesConfig species,
            FishingV2PresentationSettings presentation,
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles)
        {
            FishVisualSpec visual = species.Visual;
            ArmSpec arms = visual.Arms;
            float length = visual.Length;
            float tailX = BodyX(1f, length) - length * 0.015f;
            float tailWidth = Interpolate(visual.WidthProfile, 1f) * visual.MaxWidth;
            int segmentCount = Mathf.Max(1, arms.Segments);

            for (int i = 0; i < arms.Count; i++)
            {
                float side = arms.Count == 1 ? 0f : i / (float)(arms.Count - 1) * 2f - 1f;
                float angle = side * arms.Spread * presentation.SquidArmSpreadScale;
                float longFactor = i == 0 || i == arms.Count - 1
                    ? 1.85f * presentation.SquidOuterArmScale
                    : 1f;
                float armLength = arms.Length * length * longFactor * presentation.SquidArmLengthScale *
                    (0.74f + 0.26f * (1f - Mathf.Abs(side) * 0.8f));
                float baseY = side * tailWidth * 0.70f;
                float halfWidth = arms.Width * length * (longFactor > 1f ? 0.72f : 1f);
                float uEnd = 1f + arms.Length * longFactor * 1.15f * (0.82f + 0.30f * Mathf.Abs(side));
                float seed = StableHash01(i * 7 + 3, 11);
                int segments = longFactor > 1f ? segmentCount + 4 : segmentCount;
                int[,] rows = new int[segments + 1, 2];

                for (int k = 0; k <= segments; k++)
                {
                    float fraction = k / (float)segments;
                    float half = halfWidth * (1f - 0.72f * fraction * fraction);
                    if (longFactor > 1f)
                    {
                        half *= 1f + 2.1f * Mathf.Exp(-Mathf.Pow((fraction - 0.88f) / 0.11f, 2f));
                    }

                    float u = Mathf.Lerp(1f, uEnd, fraction);
                    Vector3 lower = new Vector3(
                        tailX - Mathf.Cos(angle) * armLength * fraction,
                        baseY + Mathf.Sin(angle) * armLength * fraction - half,
                        -0.004f * fraction);
                    Vector3 upper = new Vector3(
                        tailX - Mathf.Cos(angle) * armLength * fraction,
                        baseY + Mathf.Sin(angle) * armLength * fraction + half,
                        -0.004f * fraction);
                    rows[k, 0] = positions.Count;
                    Push(positions, colors, uv0, uv1, new VertexData(lower, u, seed, 0f), visual.FinColor);
                    rows[k, 1] = positions.Count;
                    Push(positions, colors, uv0, uv1, new VertexData(upper, u, seed, 0f), visual.FinColor);
                }

                for (int k = 0; k < segments; k++)
                {
                    AddQuadIndices(triangles, rows[k, 0], rows[k, 1], rows[k + 1, 1], rows[k + 1, 0]);
                }
            }
        }

        private static void AddTail(
            FishSpeciesConfig species,
            FishingV2PresentationSettings presentation,
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles)
        {
            FishVisualSpec visual = species.Visual;
            TailSpec tail = visual.Tail;
            float length = visual.Length;
            float tailX = BodyX(1f, length);
            float baseX = tailX + length * 0.02f;
            float tailLength = tail.Length * presentation.TailLengthScale;
            float endX = baseX - tailLength * length;
            float baseWidth = Interpolate(visual.WidthProfile, 1f) * visual.MaxWidth * 0.95f;
            float endWidth = tail.Spread * presentation.TailSpreadScale * length;
            float uTail = 1f + tailLength * 1.15f;
            float uNotch = 1f + tailLength * 0.5f;

            AddPolygon(
                positions, colors, uv0, uv1, triangles,
                new[]
                {
                    new VertexData(new Vector3(baseX, baseWidth, 0f), 1f, 0f, 0f),
                    new VertexData(new Vector3(endX, endWidth, tail.Cant * length), uTail, 0f, 0f),
                    new VertexData(new Vector3(endX + tail.Notch * presentation.TailNotchScale * tailLength * length, 0f, 0f), uNotch, 0f, 0f),
                    new VertexData(new Vector3(endX, -endWidth, tail.Cant * length), uTail, 0f, 0f),
                    new VertexData(new Vector3(baseX, -baseWidth, 0f), 1f, 0f, 0f)
                },
                visual.FinColor);
        }

        private static void AddEyes(
            FishSpeciesConfig species,
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles)
        {
            FishVisualSpec visual = species.Visual;
            EyeSpec eye = visual.Eye;
            if (eye == null)
            {
                return;
            }

            float width = Interpolate(visual.WidthProfile, eye.U) * visual.MaxWidth;
            float height = Interpolate(visual.HeightProfile, eye.U) * visual.MaxHeight;
            float baseRadius = Mathf.Min(eye.Radius * visual.Length, width * 0.62f);
            float x = BodyX(eye.U, visual.Length);
            Color iris = ScaleColor(visual.EdgeColor, 1.06f);
            iris.r = Mathf.Min(1f, iris.r + 0.10f);
            iris.g = Mathf.Min(1f, iris.g + 0.10f);
            iris.b = Mathf.Min(1f, iris.b + 0.10f);

            for (int sign = 1; sign >= -1; sign -= 2)
            {
                float angle = Mathf.PI * 0.5f - sign * eye.Spread;
                float y = Mathf.Cos(angle) * width;
                float z = height + 0.012f * visual.Length;
                AddDisc(positions, colors, uv0, uv1, triangles, x, y, z, eye.U, baseRadius * eye.RingScale, iris);
                AddDisc(positions, colors, uv0, uv1, triangles, x, y, z + 0.001f * visual.Length, eye.U, baseRadius, new Color(.09f, .10f, .12f));
                AddDisc(positions, colors, uv0, uv1, triangles,
                    x + baseRadius * 0.42f * 0.35f,
                    y - sign * baseRadius * 0.42f,
                    z + 0.002f * visual.Length,
                    eye.U,
                    baseRadius * 0.34f,
                    new Color(.93f, .95f, .96f));
            }
        }

        private static void AddDisc(
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles,
            float x,
            float y,
            float z,
            float u,
            float radius,
            Color color)
        {
            int center = positions.Count;
            Push(positions, colors, uv0, uv1, new VertexData(new Vector3(x, y, z), u, 0f, 0f), color);
            const int discSegments = 12;
            for (int i = 0; i < discSegments; i++)
            {
                float angle = i / (float)discSegments * Mathf.PI * 2f;
                Push(positions, colors, uv0, uv1,
                    new VertexData(new Vector3(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius, z), u, 0f, 0f),
                    color);
            }

            for (int i = 0; i < discSegments; i++)
            {
                triangles.Add(center);
                triangles.Add(center + 1 + i);
                triangles.Add(center + 1 + (i + 1) % discSegments);
            }
        }

        private static void AddPectoralFins(
            FishSpeciesConfig species,
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles)
        {
            FishVisualSpec visual = species.Visual;
            FinSpec fin = visual.Pectoral;
            if (fin == null)
            {
                return;
            }

            int segments = Mathf.Max(1, fin.Segments);
            for (int sign = 1; sign >= -1; sign -= 2)
            {
                int[,] rows = new int[segments + 1, 2];
                for (int i = 0; i <= segments; i++)
                {
                    float fraction = i / (float)segments;
                    float u = Mathf.Lerp(fin.U0, fin.U1, fraction);
                    float profile = Mathf.Sin(Mathf.PI * fraction);
                    float rootWidth = Interpolate(visual.WidthProfile, u) * visual.MaxWidth * 0.80f;
                    float edgeWidth = Interpolate(visual.WidthProfile, u) * visual.MaxWidth + fin.Length * visual.Length * profile;
                    float rootX = BodyX(u, visual.Length);
                    float edgeX = rootX - fin.Length * visual.Length * fin.Sweep * profile;

                    rows[i, 0] = positions.Count;
                    Push(positions, colors, uv0, uv1,
                        new VertexData(new Vector3(rootX, sign * rootWidth, 0f), u, 0f, 0f), visual.FinColor);
                    rows[i, 1] = positions.Count;
                    Push(positions, colors, uv0, uv1,
                        new VertexData(new Vector3(edgeX, sign * edgeWidth, -0.015f), u, 0f, profile), visual.FinColor);
                }

                for (int i = 0; i < segments; i++)
                {
                    AddQuadIndices(triangles, rows[i, 0], rows[i, 1], rows[i + 1, 1], rows[i + 1, 0]);
                }
            }
        }

        private static void AddFinlets(
            FishSpeciesConfig species,
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles)
        {
            FishVisualSpec visual = species.Visual;
            FinletSpec finlets = visual.Finlets;
            if (finlets == null || finlets.Count < 2)
            {
                return;
            }

            for (int i = 0; i < finlets.Count; i++)
            {
                float factor = i / (float)(finlets.Count - 1);
                float u = Mathf.Lerp(finlets.U0, finlets.U1, factor);
                float width = Interpolate(visual.WidthProfile, u) * visual.MaxWidth;
                float length = finlets.Length * visual.Length * (1f - 0.35f * factor);
                Color color = visual.FinletColor;

                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    AddPolygon(
                        positions, colors, uv0, uv1, triangles,
                        new[]
                        {
                            new VertexData(new Vector3(BodyX(u, visual.Length) + length * 0.35f, sign * width * 0.86f, 0.002f), u, 0f, 0f),
                            new VertexData(new Vector3(BodyX(u, visual.Length) - length * 0.30f, sign * (width + length * 0.30f), 0f), u, 0f, 0f),
                            new VertexData(new Vector3(BodyX(u, visual.Length) - length * 1.15f, sign * (width + length * 0.22f), 0f), u, 0f, 0f),
                            new VertexData(new Vector3(BodyX(u, visual.Length) - length * 1.55f, sign * width * 0.80f, 0.002f), u, 0f, 0f)
                        },
                        color);
                }
            }
        }

        private static void AddDorsal(
            FishSpeciesConfig species,
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles)
        {
            FishVisualSpec visual = species.Visual;
            DorsalSpec dorsal = visual.Dorsal;
            if (dorsal == null)
            {
                return;
            }

            const int dorsalSegments = 8;
            for (int i = 0; i < dorsalSegments; i++)
            {
                float f0 = i / (float)dorsalSegments;
                float f1 = (i + 1) / (float)dorsalSegments;
                float u0 = Mathf.Lerp(dorsal.U0, dorsal.U1, f0);
                float u1 = Mathf.Lerp(dorsal.U0, dorsal.U1, f1);
                float bump0 = Mathf.Pow(Mathf.Sin(Mathf.PI * f0), 0.7f);
                float bump1 = Mathf.Pow(Mathf.Sin(Mathf.PI * f1), 0.7f);
                float z0 = Interpolate(visual.HeightProfile, u0) * visual.MaxHeight * 0.92f + dorsal.Height * visual.Length * bump0;
                float z1 = Interpolate(visual.HeightProfile, u1) * visual.MaxHeight * 0.92f + dorsal.Height * visual.Length * bump1;
                float half0 = Interpolate(visual.WidthProfile, u0) * visual.MaxWidth * 0.30f * bump0 + 0.004f;
                float half1 = Interpolate(visual.WidthProfile, u1) * visual.MaxWidth * 0.30f * bump1 + 0.004f;
                float lower0 = Interpolate(visual.HeightProfile, u0) * visual.MaxHeight * 0.86f;
                float lower1 = Interpolate(visual.HeightProfile, u1) * visual.MaxHeight * 0.86f;

                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    AddPolygon(
                        positions, colors, uv0, uv1, triangles,
                        new[]
                        {
                            new VertexData(new Vector3(BodyX(u0, visual.Length), 0f, z0), u0, 0f, 0f),
                            new VertexData(new Vector3(BodyX(u1, visual.Length), 0f, z1), u1, 0f, 0f),
                            new VertexData(new Vector3(BodyX(u1, visual.Length), sign * half1, lower1), u1, 0f, 0f),
                            new VertexData(new Vector3(BodyX(u0, visual.Length), sign * half0, lower0), u0, 0f, 0f)
                        },
                        visual.BaseColor);
                }
            }
        }

        private static void AddPolygon(
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<int> triangles,
            VertexData[] vertices,
            Color color)
        {
            int start = positions.Count;
            for (int i = 0; i < vertices.Length; i++)
            {
                Push(positions, colors, uv0, uv1, vertices[i], color);
            }

            for (int i = 1; i < vertices.Length - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i);
                triangles.Add(start + i + 1);
            }
        }

        private static void AddQuadIndices(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private static void Push(
            List<Vector3> positions,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            VertexData vertex,
            Color color)
        {
            positions.Add(vertex.Position);
            colors.Add(color);
            uv0.Add(new Vector2(vertex.U, vertex.Seed));
            uv1.Add(new Vector2(vertex.Ripple, 0f));
        }

        private static float BodyX(float u, float length)
        {
            // 머리가 +X가 되도록 수면 좌표에서 직접 배치한다.
            return (0.45f - BodyXScale * u) * length;
        }

        private static float Interpolate(Vector2[] points, float u)
        {
            if (points == null || points.Length == 0)
            {
                float clamped = Mathf.Clamp01(u);
                return Mathf.Sin(Mathf.PI * clamped);
            }

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];
                if (u <= p1.x)
                {
                    float t = Mathf.Approximately(p0.x, p1.x) ? 0f : (u - p0.x) / (p1.x - p0.x);
                    return Mathf.Lerp(p0.y, p1.y, Mathf.Clamp01(t));
                }
            }

            return points[points.Length - 1].y;
        }

        private static Color ScaleColor(Color color, float scale)
        {
            color.r *= scale;
            color.g *= scale;
            color.b *= scale;
            return color;
        }

        private static float StableHash01(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263) ^ 0x5bf03635u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967296f;
            }
        }

    }
}
