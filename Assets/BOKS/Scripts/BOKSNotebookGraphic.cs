using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Static approximation of the source notebook paper background.</summary>
    [ExecuteAlways]
    public sealed class BOKSNotebookGraphic : MaskableGraphic
    {
        public Color top = new Color32(250, 249, 239, 255);
        public Color bottom = new Color32(235, 241, 230, 255);
        public Color line = new Color32(205, 211, 202, 87);
        public float spacing = 24f;
        public float lineWidth = 1f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            AddQuad(vh, r, bottom, bottom, top, top);
            for (float x = r.xMin; x <= r.xMax; x += spacing)
                AddQuad(vh, new Rect(x, r.yMin, lineWidth, r.height), line, line, line, line);
            for (float y = r.yMin; y <= r.yMax; y += spacing)
                AddQuad(vh, new Rect(r.xMin, y, r.width, lineWidth), line, line, line, line);
        }

        static void AddQuad(VertexHelper vh, Rect r, Color32 bl, Color32 br, Color32 tr, Color32 tl)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), bl, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), br, Vector2.right);
            vh.AddVert(new Vector3(r.xMax, r.yMax), tr, Vector2.one);
            vh.AddVert(new Vector3(r.xMin, r.yMax), tl, Vector2.up);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
