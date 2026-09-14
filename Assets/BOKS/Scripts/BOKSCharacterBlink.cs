using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Source-faithful BOKS blink and reaction squint (MicroAnimationSpec.md sections 3-4).
    ///
    /// Web source: styles/character.css
    ///   boksEyeBlink / boksPupilBlink : 5.8 s infinite ease-in-out (reactions.blinkLoopMs)
    ///     0%, 45%, 47%, 100% -> eye white scaleY(1), pupil scaleY(1) opacity(1)
    ///     46%               -> eye white scaleY(0.08), pupil scaleY(0.08) opacity(0.7)
    ///   reaction flags ([data-touch-rebuke], [data-obstacle-struggle], [data-goal-bubble-impact])
    ///     cancel the animation and hold the eyes closed:
    ///     eye white scaleY(0.08) translateY(4%), pupil scaleY(0.08) opacity(0).
    /// The eye/pupil elements are tagged by character-renderer.decorateBlinkableEye (first ellipse
    /// plus the pupil circle) and are transformed about their own centre (transform-box: fill-box).
    ///
    /// The Unity hero is one baked 512x512 PNG per direction, so the eye is redrawn as a small
    /// procedural overlay inside "BOKS Art": an occluder in the flat body colour, the eye white
    /// (with the source 1 px #F3CCA6 stroke where the SVG has one) and the pupil. All geometry is
    /// taken from assets/characters/&lt;id&gt;/base-&lt;direction&gt;-&lt;id&gt;.svg on the 512x512 artboard.
    /// </summary>
    public sealed class BOKSCharacterBlink : MonoBehaviour
    {
        /// <summary>Source blink loop (runtime-rules.json reactions.blinkLoopMs).</summary>
        public const float BlinkLoopSeconds = 5.8f;

        // @keyframes boksEyeBlink / boksPupilBlink
        const float ClosedScale = 0.08f;
        const float PhaseOpen = 0.45f;
        const float PhaseClosed = 0.46f;
        const float PhaseReopen = 0.47f;
        const float PupilClosedAlpha = 0.7f;
        const float EaseInOutX1 = .42f;
        const float EaseInOutX2 = .58f;
        // Reaction squint: scaleY(0.08) translateY(4%) -> the translate is scaled too.
        const float SquintTranslateRatio = 0.04f;

        // Source artboard size of every character SVG (viewBox 0 0 512 512).
        const float SvgArtboard = 512f;
        const float PupilRadiusX = 16.25f;
        const float PupilRadiusY = 17.22f;
        // Absorbs the 1 px eye stroke plus its antialias edge when hiding the baked eye.
        const float OccluderMargin = 1.5f;

        static readonly Color EyeWhiteColor = Color.white;
        static readonly Color EyeStrokeColor = new Color32(0xF3, 0xCC, 0xA6, 0xFF);
        static readonly Color PupilColor = Color.black;
        static readonly Color BodyRed = new Color32(0xFF, 0x2A, 0x0C, 0xFF);
        static readonly Color BodyYellow = new Color32(0xF2, 0xDD, 0x63, 0xFF);
        static readonly Color BodyBlu = new Color32(0x0D, 0x76, 0xFF, 0xFF);
        static readonly Color BodyGreen = new Color32(0x00, 0x98, 0x4A, 0xFF);

        sealed class EyeGeometry
        {
            public Vector2 WhiteCentre;
            public Vector2 WhiteRadius;
            public Vector2 PupilCentre;
            public Vector2 PupilRadius;
            public Color BodyFill;
            public float WhiteStrokeWidth;
        }

        /// <summary>First ellipse + pupil of each Unity hero sprite, in source SVG coordinates.</summary>
        static readonly Dictionary<string, EyeGeometry> Eyes = new Dictionary<string, EyeGeometry>
        {
            { "character-red-right", Eye(440f, 214f, 59f, 54f, 478f, 214f, BodyRed, 1f) },
            { "character-red-left", Eye(68f, 214f, 59f, 54f, 34f, 214f, BodyRed, 1f) },
            { "character-red-up", Eye(256f, 56f, 59f, 54f, 256f, 24f, BodyRed, 1f) },
            { "character-red-down", Eye(256f, 372f, 59f, 54f, 256f, 406f, BodyRed, 1f) },
            { "character-yellow-right", Eye(441.75f, 214f, 59f, 54f, 458f, 214f, BodyYellow, 1f) },
            { "character-yellow-left", Eye(68f, 214f, 59f, 54f, 34f, 214f, BodyYellow, 1f) },
            { "character-yellow-up", Eye(256f, 57.22f, 59f, 54f, 256f, 34f, BodyYellow, 1f) },
            { "character-yellow-down", Eye(256f, 372f, 59f, 54f, 256f, 398f, BodyYellow, 1f) },
            { "character-blu-right", Eye(444.25f, 214f, 59f, 54f, 480.25f, 214f, BodyBlu, 1f) },
            { "character-blu-left", Eye(68.25f, 214f, 59f, 54f, 30.25f, 214f, BodyBlu, 1f) },
            { "character-blu-up", Eye(272.25f, 54f, 59f, 54f, 272.25f, 26f, BodyBlu, 1f) },
            { "character-blu-down", Eye(256f, 372f, 59f, 54f, 256f, 406f, BodyBlu, 1f) },
            // Green uses a circle pupil (r 16.25) and an eye white without the amber stroke.
            { "character-green-right", Eye(433f, 210.12f, 59f, 55.88f, 458.48f, 210.12f, BodyGreen, 0f, 16.25f, 16.25f) },
            { "character-green-left", Eye(77f, 199.29f, 59f, 55.88f, 43f, 199.29f, BodyGreen, 0f, 16.25f, 16.25f) },
            { "character-green-up", Eye(256f, 65.12f, 59f, 55.88f, 256f, 35.12f, BodyGreen, 0f, 16.25f, 16.25f) },
            { "character-green-down", Eye(256f, 323.29f, 59f, 55.88f, 256f, 358.29f, BodyGreen, 0f, 16.25f, 16.25f) },
        };

        Image artImage;
        BOKSShapeGraphic occluder;
        BOKSShapeGraphic eyeWhite;
        BOKSShapeGraphic pupil;
        EyeGeometry geometry;
        Vector2 eyeWhiteCentre;
        float blinkOrigin = -1f;
        float squintUntil = -1f;
        float appliedPupilAlpha = 1f;

        /// <summary>True while a reaction holds the eyes squinted instead of blinking.</summary>
        public bool Squinting => geometry != null && Time.unscaledTime < squintUntil;

        /// <summary>True when the current directional sprite has a known eye overlay.</summary>
        public bool OverlayVisible => geometry != null;

        /// <summary>Binds the overlay to the directional hero art and lays out the current sprite.</summary>
        public void Bind(Image art)
        {
            artImage = art;
            EnsureChildren();
            ApplySprite();
        }

        /// <summary>
        /// Re-lays out the overlay for the sprite currently displayed. The web re-renders the hero
        /// markup on every sprite swap, which restarts the CSS blink animation.
        /// </summary>
        public void ApplySprite()
        {
            string spriteName = artImage != null && artImage.sprite != null ? artImage.sprite.name : string.Empty;
            EyeGeometry next;
            SetGeometry(Eyes.TryGetValue(spriteName, out next) ? next : null);
        }

        /// <summary>Holds the squint pose (reaction override) for the given unscaled duration.</summary>
        public void SquintFor(float seconds)
        {
            if (geometry == null || seconds <= 0f) return;
            squintUntil = Mathf.Max(squintUntil, Time.unscaledTime + seconds);
        }

        public void ClearSquint() => squintUntil = -1f;

        void EnsureChildren()
        {
            if (occluder != null) return;
            occluder = Child("Eye Occluder", BOKSShapeGraphic.ShapeKind.RoundedRectangle, 0f);
            eyeWhite = Child("Eye White", BOKSShapeGraphic.ShapeKind.Ellipse, SvgArtboard);
            pupil = Child("Eye Pupil", BOKSShapeGraphic.ShapeKind.Ellipse, SvgArtboard);
        }

        void SetGeometry(EyeGeometry next)
        {
            geometry = next;
            blinkOrigin = -1f;
            squintUntil = -1f;
            appliedPupilAlpha = 1f;
            if (occluder == null) return;

            bool visible = next != null;
            occluder.gameObject.SetActive(visible);
            eyeWhite.gameObject.SetActive(visible);
            pupil.gameObject.SetActive(visible);
            if (!visible) return;

            Vector2 occluderRadius = next.WhiteRadius + Vector2.one * OccluderMargin;
            Place(occluder, next.WhiteCentre, occluderRadius * 2f);
            Fill(occluder, next.BodyFill, 0f, Color.clear);

            eyeWhiteCentre = Place(eyeWhite, next.WhiteCentre, next.WhiteRadius * 2f);
            Fill(eyeWhite, EyeWhiteColor, next.WhiteStrokeWidth * ArtScale, EyeStrokeColor);

            Place(pupil, next.PupilCentre, next.PupilRadius * 2f);
            Fill(pupil, PupilColor, 0f, Color.clear);

            ApplyScale(1f);
            ((RectTransform)eyeWhite.transform).anchoredPosition = eyeWhiteCentre;
            ApplyPupilAlpha(1f);
        }

        void Update()
        {
            if (geometry == null) return;

            float scaleY;
            float pupilAlpha;
            float offsetY = 0f;

            if (Time.unscaledTime < squintUntil)
            {
                // Reaction pose: animation: none, eyes held closed.
                scaleY = ClosedScale;
                pupilAlpha = 0f;
                offsetY = SquintTranslateRatio * ClosedScale * eyeWhite.rectTransform.rect.height;
            }
            else
            {
                if (blinkOrigin < 0f) blinkOrigin = Time.unscaledTime;
                float phase = Mathf.Repeat(Time.unscaledTime - blinkOrigin, BlinkLoopSeconds) / BlinkLoopSeconds;
                scaleY = 1f;
                pupilAlpha = 1f;
                if (phase > PhaseOpen && phase < PhaseReopen)
                {
                    float segment = phase < PhaseClosed
                        ? Mathf.InverseLerp(PhaseOpen, PhaseClosed, phase)
                        : Mathf.InverseLerp(PhaseReopen, PhaseClosed, phase);
                    float eased = BOKSLevel2Controller.CubicBezierYForX(segment, EaseInOutX1, 0f, EaseInOutX2, 1f);
                    scaleY = Mathf.Lerp(1f, ClosedScale, eased);
                    pupilAlpha = Mathf.Lerp(1f, PupilClosedAlpha, eased);
                }
            }

            ApplyScale(scaleY);
            ((RectTransform)eyeWhite.transform).anchoredPosition = eyeWhiteCentre + Vector2.down * offsetY;
            ApplyPupilAlpha(pupilAlpha);
        }

        void ApplyScale(float scaleY)
        {
            Vector3 scale = new Vector3(1f, scaleY, 1f);
            eyeWhite.transform.localScale = scale;
            pupil.transform.localScale = scale;
        }

        void ApplyPupilAlpha(float alpha)
        {
            if (Mathf.Approximately(appliedPupilAlpha, alpha)) return;
            appliedPupilAlpha = alpha;
            Color top = pupil.topColor; top.a = alpha; pupil.topColor = top;
            Color bottom = pupil.bottomColor; bottom.a = alpha; pupil.bottomColor = bottom;
            pupil.SetVerticesDirty();
        }

        /// <summary>Source SVG pixels -> art rect pixels (512 px artboard onto the 76.68 px art rect).</summary>
        float ArtScale => ((RectTransform)transform).rect.width / SvgArtboard;

        /// <summary>Maps a source SVG point onto the art rect (top-left origin, flipped Y).</summary>
        Vector2 Place(BOKSShapeGraphic graphic, Vector2 svgCentre, Vector2 svgSize)
        {
            float scale = ArtScale;
            RectTransform rect = (RectTransform)graphic.transform;
            rect.anchoredPosition = new Vector2(
                (svgCentre.x - SvgArtboard * .5f) * scale,
                (SvgArtboard * .5f - svgCentre.y) * scale);
            rect.sizeDelta = svgSize * scale;
            return rect.anchoredPosition;
        }

        BOKSShapeGraphic Child(string name, BOKSShapeGraphic.ShapeKind shape, float cornerRadius)
        {
            Transform existing = transform.Find(name);
            BOKSShapeGraphic graphic = existing != null ? existing.GetComponent<BOKSShapeGraphic>() : null;
            if (graphic == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform, false);
                graphic = go.AddComponent<BOKSShapeGraphic>();
            }
            RectTransform rect = (RectTransform)graphic.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            graphic.shape = shape;
            graphic.cornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            return graphic;
        }

        static void Fill(BOKSShapeGraphic graphic, Color color, float borderWidth, Color borderColor)
        {
            graphic.topColor = color;
            graphic.bottomColor = color;
            graphic.borderWidth = borderWidth;
            graphic.borderColor = borderColor;
            graphic.SetVerticesDirty();
        }

        static EyeGeometry Eye(float whiteX, float whiteY, float whiteRadiusX, float whiteRadiusY,
            float pupilX, float pupilY, Color body, float strokeWidth,
            float pupilRadiusX = PupilRadiusX, float pupilRadiusY = PupilRadiusY)
        {
            return new EyeGeometry
            {
                WhiteCentre = new Vector2(whiteX, whiteY),
                WhiteRadius = new Vector2(whiteRadiusX, whiteRadiusY),
                PupilCentre = new Vector2(pupilX, pupilY),
                PupilRadius = new Vector2(pupilRadiusX, pupilRadiusY),
                BodyFill = body,
                WhiteStrokeWidth = strokeWidth
            };
        }
    }
}
