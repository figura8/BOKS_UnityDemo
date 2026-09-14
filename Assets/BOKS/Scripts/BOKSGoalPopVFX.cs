using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Source-faithful goal-bubble pop. Reproduces the active Canvas implementation from
    /// js/core/game.js (drawGoalPopCanvas + createGoalPopParticles): an expanding, fading stroked
    /// ring plus 14-20 soft light-blue orbs that fly outward with a slight wobble, then fade and
    /// shrink. Orbs are pooled and reused so repeated levels never leak objects.
    /// </summary>
    public sealed class BOKSGoalPopVFX : MonoBehaviour
    {
        const float RingDuration = 0.760f;   // ringMs
        const float RingStartRadius = 16f;
        const float RingEndRadius = 100f;
        const float ClearTime = 2.5f;        // canvasClearMs
        const float OrbLifeMin = 1.5f;
        const float OrbLifeMax = 2.32f;
        const float MotionDamp = 0.82f;      // x + vx * t * 0.82
        const float FadeRate = 0.82f;        // alpha * (1 - t * 0.82)
        const float ShrinkRate = 0.34f;      // radius * (1 - t * 0.34)
        const int PoolSize = 32;

        static readonly Color RingStroke = new Color(234f / 255f, 250f / 255f, 255f / 255f, 0.92f);
        static readonly Color GlowTint = new Color(158f / 255f, 237f / 255f, 255f / 255f, 0.45f);

        Sprite softSprite;
        readonly List<Image> orbPool = new List<Image>();
        BOKSShapeGraphic ring;
        Image glow;
        [SerializeField] CanvasGroup bubbleFade;
        int nextOrb;

        public void BindGoalBubble(CanvasGroup bubble) => bubbleFade = bubble;

        public void Play(Vector2 centre)
        {
            EnsureAssets();
            StopAllCoroutines();
            DeactivateAll();
            if (bubbleFade != null) bubbleFade.alpha = 0f;
            StartCoroutine(Run(centre));
        }

        public void Reset()
        {
            if (bubbleFade != null) bubbleFade.alpha = 1f;
        }

        void OnDestroy()
        {
            if (softSprite != null)
            {
                if (softSprite.texture != null) Destroy(softSprite.texture);
                Destroy(softSprite);
                softSprite = null;
            }
        }

        void EnsureAssets()
        {
            if (softSprite == null) softSprite = BuildSoftSprite();

            if (ring == null)
            {
                ring = NewRect("Goal Pop Ring", RingStartRadius * 2f).gameObject.AddComponent<BOKSShapeGraphic>();
                ring.shape = BOKSShapeGraphic.ShapeKind.Ellipse;
                ring.topColor = ring.bottomColor = Color.clear;
                ring.borderWidth = 3f;
                ring.borderColor = RingStroke;
                ring.raycastTarget = false;
            }

            if (glow == null)
                glow = NewImage("Goal Pop Glow", RingStartRadius * 2f * 1.7f, GlowTint);

            while (orbPool.Count < PoolSize)
                orbPool.Add(NewImage("Goal Pop Orb", 16f, Color.white));
        }

        RectTransform NewRect(string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        Image NewImage(string name, float size, Color tint)
        {
            var rt = NewRect(name, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = softSprite;
            img.color = tint;
            img.raycastTarget = false;
            img.gameObject.SetActive(false);
            return img;
        }

        Image GetOrb()
        {
            Image img = orbPool[nextOrb];
            nextOrb = (nextOrb + 1) % orbPool.Count;
            img.gameObject.SetActive(true);
            img.color = Color.white;
            return img;
        }

        void DeactivateAll()
        {
            foreach (Image img in orbPool) img.gameObject.SetActive(false);
            if (ring != null) ring.gameObject.SetActive(false);
            if (glow != null) glow.gameObject.SetActive(false);
        }

        IEnumerator Run(Vector2 centre)
        {
            ring.gameObject.SetActive(true);
            glow.gameObject.SetActive(true);
            RectTransform ringRt = (RectTransform)ring.transform;
            RectTransform glowRt = (RectTransform)glow.transform;
            ringRt.anchoredPosition = glowRt.anchoredPosition = centre;

            int count = Random.Range(14, 21);
            var orbs = new List<Orb>(count);
            float burstAngle = Random.value * Mathf.PI * 2f;
            float burstBias = 0.45f + Random.value * 0.35f;
            for (int i = 0; i < count; i++)
            {
                float spreadMode = Random.value;
                float baseAngle = spreadMode < 0.55f
                    ? burstAngle + (Random.value - 0.5f) * 1.35f
                    : (Mathf.PI * 2f * i) / count + (Random.value - 0.5f) * 0.62f;
                float biasBoost = 1f + Mathf.Max(0f, Mathf.Cos(baseAngle - burstAngle)) * burstBias;
                float speed = (34f + Random.value * 84f) * biasBoost;
                float spawnRadius = Random.value * 10f;
                float radius = Random.value > 0.62f ? 14f + Random.value * 18f : 8f + Random.value * 14f;
                Image img = GetOrb();
                orbs.Add(new Orb
                {
                    image = img,
                    rect = (RectTransform)img.transform,
                    velocity = new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) * speed,
                    origin = centre + new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) * spawnRadius,
                    radius = radius,
                    alpha = Mathf.Min(1f, 0.78f + Random.value * 0.24f),
                    life = OrbLifeMin + Random.value * (OrbLifeMax - OrbLifeMin),
                    wobble = (Random.value - 0.5f) * 18f,
                    wobblePhase = Random.value * Mathf.PI * 2f
                });
            }

            float elapsed = 0f;
            while (elapsed < ClearTime)
            {
                elapsed += Time.unscaledDeltaTime;

                float ringProgress = Mathf.Clamp01(elapsed / RingDuration);
                float ringRadius = RingStartRadius + ringProgress * (RingEndRadius - RingStartRadius);
                float ringAlpha = 1f - ringProgress;
                ringRt.sizeDelta = new Vector2(ringRadius * 2f, ringRadius * 2f);
                glowRt.sizeDelta = new Vector2(ringRadius * 3.4f, ringRadius * 3.4f);
                Color stroke = RingStroke; stroke.a = RingStroke.a * ringAlpha;
                ring.borderColor = stroke;
                ring.SetVerticesDirty();
                Color gc = GlowTint; gc.a = GlowTint.a * ringAlpha;
                glow.color = gc;

                foreach (Orb orb in orbs)
                {
                    float t = Mathf.Clamp01(elapsed / orb.life);
                    if (t >= 1f) { orb.image.gameObject.SetActive(false); continue; }
                    float wobbleX = Mathf.Sin(t * 7f + orb.wobblePhase) * orb.wobble * (1f - t);
                    float wobbleY = Mathf.Cos(t * 6f + orb.wobblePhase) * orb.wobble * 0.45f * (1f - t);
                    Vector2 pos = orb.origin + orb.velocity * t * MotionDamp + new Vector2(wobbleX, wobbleY);
                    float r = orb.radius * (1f - t * ShrinkRate);
                    float a = orb.alpha * Mathf.Max(0f, 1f - t * FadeRate);
                    orb.rect.anchoredPosition = pos;
                    orb.rect.sizeDelta = new Vector2(r * 2f, r * 2f);
                    Color c = orb.image.color; c.a = a; orb.image.color = c;
                }

                yield return null;
            }

            DeactivateAll();
        }

        struct Orb
        {
            public Image image;
            public RectTransform rect;
            public Vector2 velocity;
            public Vector2 origin;
            public float radius;
            public float alpha;
            public float life;
            public float wobble;
            public float wobblePhase;
        }

        static Sprite BuildSoftSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size - 0.5f;
                float ny = (y + 0.5f) / size - 0.5f;
                float t = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny) * 2f);
                tex.SetPixel(x, y, OrbColor(t));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        // Matches the web 5-stop radial gradient (white core -> light blue -> transparent edge).
        static Color OrbColor(float t)
        {
            Color c0 = new Color(1f, 1f, 1f, 0.98f);
            Color c1 = new Color(0.96f, 0.99f, 1f, 0.95f);
            Color c2 = new Color(0.78f, 0.93f, 1f, 0.34f);
            Color c3 = new Color(0.66f, 0.89f, 0.97f, 0.08f);
            Color c4 = new Color(0.66f, 0.89f, 0.97f, 0f);
            if (t < 0.18f) return Color.Lerp(c0, c1, t / 0.18f);
            if (t < 0.54f) return Color.Lerp(c1, c2, (t - 0.18f) / 0.36f);
            if (t < 0.78f) return Color.Lerp(c2, c3, (t - 0.54f) / 0.24f);
            return Color.Lerp(c3, c4, Mathf.Clamp01((t - 0.78f) / 0.22f));
        }
    }
}
