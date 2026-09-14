using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Source-faithful touch rebuke (MicroAnimationSpec.md section 5a).
    ///
    /// Web source: js/core/game.js
    ///   - any pointerdown / touchstart on #sprite (the hero cell) calls triggerBoksTouchRebuke()
    ///   - ignored while running || animating, cooldown 950 ms (reactions.characterTapCooldownMs)
    ///   - sets [data-touch-rebuke] for 560 ms (reactions.characterTapMs)
    /// styles/character.css @keyframes boksTouchRebuke, 0.56 s cubic-bezier(.22,1,.36,1):
    ///   0%   translate(0,0)        rotate(0)    scale(1)
    ///   18%  translate(-4%,0)      rotate(-8deg) scale(0.98,1.02)
    ///   36%  translate(5%,-1%)     rotate(7deg)  scale(1.01,0.99)
    ///   56%  translate(-3%,0)      rotate(-5deg) scale(0.99,1.01)
    ///   78%  translate(2%,0)       rotate(3deg)  scale(1)
    ///   100% identity
    /// The same flag also holds the eyes squinted; see <see cref="BOKSCharacterBlink"/>.
    ///
    /// The shake runs on "BOKS Visual" (the web .boks-hero element), so the grid-cell root never
    /// moves, and the translate percentages are relative to the 71 px hero rect like the source's
    /// own border box. Percentages and rotations are applied exactly as the source transform list:
    /// translate(rotate(scale(...))).
    /// </summary>
    public sealed class BOKSCharacterRebuke : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>Source tap reaction duration (runtime-rules.json reactions.characterTapMs).</summary>
        public const float RebukeSeconds = 0.56f;

        /// <summary>Source tap cooldown (runtime-rules.json reactions.characterTapCooldownMs).</summary>
        public const float CooldownSeconds = 0.95f;

        const float EaseX1 = .22f;
        const float EaseY1 = 1f;
        const float EaseX2 = .36f;
        const float EaseY2 = 1f;

        struct Key
        {
            public float At;
            public float X;                 // fraction of the hero width
            public float Y;                 // fraction of the hero height (CSS y grows downwards)
            public float RotationDegrees;   // CSS clockwise degrees
            public float ScaleX;
            public float ScaleY;
        }

        static readonly Key[] Keys =
        {
            new Key { At = 0f, X = 0f, Y = 0f, RotationDegrees = 0f, ScaleX = 1f, ScaleY = 1f },
            new Key { At = .18f, X = -.04f, Y = 0f, RotationDegrees = -8f, ScaleX = .98f, ScaleY = 1.02f },
            new Key { At = .36f, X = .05f, Y = -.01f, RotationDegrees = 7f, ScaleX = 1.01f, ScaleY = .99f },
            new Key { At = .56f, X = -.03f, Y = 0f, RotationDegrees = -5f, ScaleX = .99f, ScaleY = 1.01f },
            new Key { At = .78f, X = .02f, Y = 0f, RotationDegrees = 3f, ScaleX = 1f, ScaleY = 1f },
            new Key { At = 1f, X = 0f, Y = 0f, RotationDegrees = 0f, ScaleX = 1f, ScaleY = 1f },
        };

        [SerializeField] BOKSLevel2Controller controller;
        [SerializeField] RectTransform visual;
        [SerializeField] BOKSCharacterBlink blink;

        Coroutine playing;
        float cooldownUntil = -1f;
        Vector2 posePosition;
        Vector3 poseScale = Vector3.one;
        Quaternion poseRotation = Quaternion.identity;

        /// <summary>True while the 560 ms shake/squash is playing.</summary>
        public bool Rebuking => playing != null;

        public void Bind(BOKSLevel2Controller owner, RectTransform visualChild, BOKSCharacterBlink eyeBlink)
        {
            controller = owner;
            visual = visualChild;
            blink = eyeBlink;
        }

        void Awake() => EnsureTapTarget();

        public void OnPointerDown(PointerEventData eventData) => Trigger();

        /// <summary>
        /// Taps the hero: shake/squash unless a run owns the input or the 950 ms cooldown is still
        /// running. Audio remains with the existing BOKSAudioPointerCue on this Art raycast target.
        /// </summary>
        public bool Trigger()
        {
            if (controller != null && controller.InputLocked) return false;
            if (Time.unscaledTime < cooldownUntil) return false;

            cooldownUntil = Time.unscaledTime + CooldownSeconds;
            blink?.SquintFor(RebukeSeconds);

            if (visual == null) return true;
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(Play());
            return true;
        }

        /// <summary>The web binds the tap on the hero cell; uGUI needs a raycast target there.</summary>
        void EnsureTapTarget()
        {
            if (GetComponent<Graphic>() != null) return;
            Image hit = gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
        }

        IEnumerator Play()
        {
            posePosition = visual.anchoredPosition;
            poseScale = visual.localScale;
            poseRotation = visual.localRotation;
            float startedAt = Time.unscaledTime;

            while (Time.unscaledTime - startedAt < RebukeSeconds)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / RebukeSeconds);
                Apply(progress, posePosition, poseScale, poseRotation);
                yield return null;
            }

            RestorePose();
            playing = null;
        }

        /// <summary>Stops a running shake and restores the visual pose (level reset / reload path).</summary>
        public void Cancel()
        {
            if (playing != null)
            {
                StopCoroutine(playing);
                playing = null;
            }
            RestorePose();
        }

        void RestorePose()
        {
            if (visual == null) return;
            visual.anchoredPosition = posePosition;
            visual.localScale = poseScale;
            visual.localRotation = poseRotation;
        }

        void Apply(float progress, Vector2 basePosition, Vector3 baseScale, Quaternion baseRotation)
        {
            int index = 0;
            while (index < Keys.Length - 2 && progress > Keys[index + 1].At) index++;

            Key from = Keys[index];
            Key to = Keys[index + 1];
            float span = Mathf.Max(.0001f, to.At - from.At);
            float eased = BOKSLevel2Controller.CubicBezierYForX(
                Mathf.Clamp01((progress - from.At) / span), EaseX1, EaseY1, EaseX2, EaseY2);

            float x = Mathf.Lerp(from.X, to.X, eased);
            float y = Mathf.Lerp(from.Y, to.Y, eased);
            float rotation = Mathf.Lerp(from.RotationDegrees, to.RotationDegrees, eased);
            float scaleX = Mathf.Lerp(from.ScaleX, to.ScaleX, eased);
            float scaleY = Mathf.Lerp(from.ScaleY, to.ScaleY, eased);

            Rect rect = visual.rect;
            visual.anchoredPosition = basePosition + new Vector2(x * rect.width, -y * rect.height);
            visual.localRotation = baseRotation * Quaternion.Euler(0f, 0f, -rotation);
            visual.localScale = Vector3.Scale(baseScale, new Vector3(scaleX, scaleY, 1f));
        }
    }
}
