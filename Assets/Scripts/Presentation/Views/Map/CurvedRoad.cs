using UnityEngine;
using UnityEngine.UI;

namespace SpirePresentation
{
    // Cubic Bezier strip with distance-based UVs; texture may be a dotted trail, brush or road.
    public sealed class CurvedRoad : MaskableGraphic
    {
        public Vector2 Start, End;
        public float Width = 6, TileLength = 24, Curvature = .32f;
        public Texture RoadTexture;
        public override Texture mainTexture => RoadTexture != null ? RoadTexture : s_WhiteTexture;

        public static Vector2 Point(Vector2 a, Vector2 b, float t, float curvature)
        {
            Vector2 delta = b - a;
            var c1 = a + new Vector2(delta.x * curvature, delta.y * .5f);
            var c2 = b - new Vector2(delta.x * curvature, delta.y * .5f);
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int segments = 32;
            float distance = 0;
            Vector2 previous = Start;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 p = Point(Start, End, t, Curvature);
                var tangent = Point(Start, End, Mathf.Min(1, t + .01f), Curvature) - Point(Start, End, Mathf.Max(0, t - .01f), Curvature);
                Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * Width * .5f;
                distance += Vector2.Distance(previous, p);
                previous = p;
                float uv = distance / Mathf.Max(1, TileLength);
                vh.AddVert(p - normal, color, new Vector2(uv, 0));
                vh.AddVert(p + normal, color, new Vector2(uv, 1));
                if (i > 0)
                {
                    int v = i * 2;
                    vh.AddTriangle(v - 2, v - 1, v);
                    vh.AddTriangle(v, v - 1, v + 1);
                }
            }
        }
    }
}
