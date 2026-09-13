using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Draws the source main-program route: row one, a right return through the
    /// inter-row gap, then row two. Slot occlusion is handled by foreground cards.
    /// </summary>
    [ExecuteAlways]
    public sealed class BOKSSequencePathGraphic : MaskableGraphic
    {
        public Color pathColor = new Color(0.83f, 0.72f, 0.55f, 0.78f);
        [Min(1f)] public float thickness = 3f;
        public float startX = 69.375f;
        public float endX = 386.625f;
        public float rowOneY = 35f;
        public float middleY = 71f;
        public float rowTwoY = 107f;
        public float rightRadius = 19.8f;
        public float leftRadius = 10f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 previous = P(startX, rowOneY);
            Stroke(vh, previous, P(endX, rowOneY));
            previous = P(endX, rowOneY);
            Stroke(vh, previous, P(endX, middleY - rightRadius));
            previous = P(endX, middleY - rightRadius);

            const int curveSteps = 10;
            for (int i = 1; i <= curveSteps; i++)
            {
                float t = i / (float)curveSteps;
                Vector2 next = Quadratic(
                    P(endX, middleY - rightRadius),
                    P(endX, middleY),
                    P(endX - rightRadius, middleY), t);
                Stroke(vh, previous, next);
                previous = next;
            }

            Vector2 leftCurveStart = P(startX + leftRadius, middleY);
            Stroke(vh, previous, leftCurveStart);
            previous = leftCurveStart;
            for (int i = 1; i <= curveSteps; i++)
            {
                float t = i / (float)curveSteps;
                Vector2 next = Quadratic(
                    P(startX + leftRadius, middleY),
                    P(startX, middleY),
                    P(startX, middleY + leftRadius), t);
                Stroke(vh, previous, next);
                previous = next;
            }

            Stroke(vh, previous, P(startX, rowTwoY));
            Stroke(vh, P(startX, rowTwoY), P(endX, rowTwoY));
        }

        static Vector2 P(float x, float topDownY) => new Vector2(x, -topDownY);

        static Vector2 Quadratic(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        void Stroke(VertexHelper vh, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .0001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (thickness * .5f);
            int i = vh.currentVertCount;
            Color32 c = pathColor;
            vh.AddVert(a - normal, c, Vector2.zero);
            vh.AddVert(a + normal, c, Vector2.up);
            vh.AddVert(b + normal, c, Vector2.one);
            vh.AddVert(b - normal, c, Vector2.right);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
