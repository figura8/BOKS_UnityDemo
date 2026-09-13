using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Tiny teaching-friendly UI primitive used by the static Level 2 mock-up.
    /// It draws a rounded rectangle or ellipse without requiring a generated sprite atlas.
    /// </summary>
    [ExecuteAlways]
    public sealed class BOKSShapeGraphic : MaskableGraphic
    {
        public enum ShapeKind { RoundedRectangle, Ellipse, TriangleRight }

        public ShapeKind shape = ShapeKind.RoundedRectangle;
        public Color topColor = Color.white;
        public Color bottomColor = Color.white;
        public Color borderColor = Color.clear;
        [Min(0f)] public float borderWidth;
        [Min(0f)] public float cornerRadius = 8f;
        [Range(2, 12)] public int cornerSegments = 5;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;

            if (shape == ShapeKind.TriangleRight)
            {
                AddTriangle(vh, r);
                return;
            }

            int segments = shape == ShapeKind.Ellipse ? 48 : Mathf.Max(2, cornerSegments) * 4;
            Vector2[] outer = shape == ShapeKind.Ellipse
                ? EllipsePoints(r, segments)
                : RoundedPoints(r, cornerRadius, Mathf.Max(2, cornerSegments));

            float inset = Mathf.Clamp(borderWidth, 0f, Mathf.Min(r.width, r.height) * .49f);
            Rect innerRect = new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
            Vector2[] inner = shape == ShapeKind.Ellipse
                ? EllipsePoints(innerRect, segments)
                : RoundedPoints(innerRect, Mathf.Max(0f, cornerRadius - inset), Mathf.Max(2, cornerSegments));

            if (inset > .01f && borderColor.a > 0f)
                AddRing(vh, outer, inner, borderColor, r);

            AddFan(vh, inner, r);
        }

        void AddTriangle(VertexHelper vh, Rect r)
        {
            Color32 c = topColor;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, Vector2.up);
            vh.AddVert(new Vector3(r.xMax, r.center.y), c, Vector2.one);
            vh.AddTriangle(0, 1, 2);
        }

        void AddFan(VertexHelper vh, Vector2[] points, Rect r)
        {
            int centre = vh.currentVertCount;
            vh.AddVert(r.center, LerpColor(r.center.y, r), new Vector2(.5f, .5f));
            for (int i = 0; i < points.Length; i++)
                vh.AddVert(points[i], LerpColor(points[i].y, r), Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                vh.AddTriangle(centre, centre + 1 + i, centre + 1 + ((i + 1) % points.Length));
        }

        void AddRing(VertexHelper vh, Vector2[] outer, Vector2[] inner, Color c, Rect r)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < outer.Length; i++)
            {
                vh.AddVert(outer[i], c, Vector2.zero);
                vh.AddVert(inner[i], c, Vector2.zero);
            }
            for (int i = 0; i < outer.Length; i++)
            {
                int n = (i + 1) % outer.Length;
                vh.AddTriangle(start + i * 2, start + n * 2, start + n * 2 + 1);
                vh.AddTriangle(start + i * 2, start + n * 2 + 1, start + i * 2 + 1);
            }
        }

        Color32 LerpColor(float y, Rect r)
        {
            float t = Mathf.InverseLerp(r.yMin, r.yMax, y);
            return Color.Lerp(bottomColor, topColor, t);
        }

        static Vector2[] EllipsePoints(Rect r, int count)
        {
            Vector2[] p = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * 2f * i / count;
                p[i] = r.center + new Vector2(Mathf.Cos(a) * r.width * .5f, Mathf.Sin(a) * r.height * .5f);
            }
            return p;
        }

        static Vector2[] RoundedPoints(Rect r, float radius, int perCorner)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * .5f);
            Vector2[] p = new Vector2[perCorner * 4];
            Vector2[] centres =
            {
                new Vector2(r.xMax - radius, r.yMax - radius),
                new Vector2(r.xMin + radius, r.yMax - radius),
                new Vector2(r.xMin + radius, r.yMin + radius),
                new Vector2(r.xMax - radius, r.yMin + radius)
            };
            for (int corner = 0; corner < 4; corner++)
            for (int i = 0; i < perCorner; i++)
            {
                float a = Mathf.Deg2Rad * (corner * 90f + i * (90f / (perCorner - 1)));
                p[corner * perCorner + i] = centres[corner] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return p;
        }
    }
}
