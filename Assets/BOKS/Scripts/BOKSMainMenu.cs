using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Small portrait start gate for the existing campaign scene.</summary>
    public sealed class BOKSMainMenu : MonoBehaviour
    {
        const float StartPopSeconds = .72f; // web startGameFromGate delay before opening the app
        const float PopVisualSeconds = .74f; // web .is-popping goal-bubble animation duration
        const float GateFadeSeconds = 1.65f; // web openAppFromGate gateFadeMs
        const float LevelRevealSeconds = 4.2f; // web #app opacity transition after prestart clears
        const float LevelSettleSeconds = 3.2f; // web #app transform/filter transition
        const float IntroLogoFadeInSeconds = 1.35f;
        const float IntroLogoHoldSeconds = .6f;
        const float IntroLogoFadeOutSeconds = .5f;
        const float IntroPauseSeconds = .12f;
        const float FinalCompositionFadeInSeconds = .7f;
        [SerializeField] Sprite logoSprite;
        RectTransform bubble;
        RectTransform shell;
        RectTransform glow;
        RectTransform flower;
        RectTransform bloom;
        RectTransform core;
        RectTransform ring;
        readonly RectTransform[] shards = new RectTransform[4];
        CanvasGroup shellGroup;
        CanvasGroup glowGroup;
        CanvasGroup flowerGroup;
        CanvasGroup bloomGroup;
        CanvasGroup coreGroup;
        CanvasGroup ringGroup;
        readonly CanvasGroup[] shardGroups = new CanvasGroup[4];
        CanvasGroup menuGroup;
        CanvasGroup introLogoGroup;
        CanvasGroup finalCompositionGroup;
        RectTransform menuRoot;
        CanvasGroup revealMask;
        Button startButton;
        Vector2 bubbleHome;
        bool isPopping;

        void Awake()
        {
            Build();
        }

        void Start()
        {
            BOKSAudioManager.Instance.StartMenuMusic();
            StartCoroutine(PlayMenuIntro());
        }

        void Build()
        {
            var canvasGo = new GameObject("Portrait Menu (520 x 1000)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            // This canvas owns menuGroup and must follow BOKSMainMenu into DontDestroyOnLoad.
            // Previously it was a separate scene root, so loading Campaign destroyed it while
            // OpenCampaignBehindGate was still animating the retained CanvasGroup reference.
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(520, 1000);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            menuGroup = canvasGo.GetComponent<CanvasGroup>();
            RectTransform root = canvasGo.GetComponent<RectTransform>();
            menuRoot = root;

            Shape("Notebook Background", root, Vector2.zero, new Vector2(520, 1000), BOKSShapeGraphic.ShapeKind.RoundedRectangle,
                new Color32(247, 247, 241, 255), new Color32(239, 242, 230, 255), 0f);
            RectTransform introLogo = Logo("MenuLogo", root, new Vector2(0f, 230f), new Vector2(336f, 154f));
            introLogoGroup = introLogo.gameObject.AddComponent<CanvasGroup>();
            introLogoGroup.alpha = 0f;

            RectTransform finalComposition = Rect("Final Menu Composition", root, Vector2.zero, new Vector2(520, 1000));
            finalCompositionGroup = finalComposition.gameObject.AddComponent<CanvasGroup>();
            finalCompositionGroup.alpha = 0f;
            finalCompositionGroup.blocksRaycasts = false;

            bubbleHome = new Vector2(0, -42);
            bubble = Rect("Challenge Bubble", finalComposition, bubbleHome, new Vector2(164, 164));
            glow = Rect("Bubble Glow", bubble, Vector2.zero, new Vector2(164, 164));
            glowGroup = glow.gameObject.AddComponent<CanvasGroup>();
            Shape("Glow Fill", glow, Vector2.zero, new Vector2(164, 164), BOKSShapeGraphic.ShapeKind.Ellipse,
                new Color(1f, .88f, .36f, .18f), new Color(1f, .78f, .2f, .03f), 0f);
            bloom = Rect("Goal Light Bloom", bubble, Vector2.zero, new Vector2(144, 144));
            bloomGroup = bloom.gameObject.AddComponent<CanvasGroup>();
            Shape("Bloom Fill", bloom, Vector2.zero, new Vector2(144, 144), BOKSShapeGraphic.ShapeKind.Ellipse,
                new Color(1f, .94f, .60f, .22f), new Color(1f, .80f, .35f, 0f), 0f);
            shell = Rect("Campaign Bubble Shell", bubble, Vector2.zero, new Vector2(124, 124));
            shellGroup = shell.gameObject.AddComponent<CanvasGroup>();
            BOKSShapeGraphic shellGraphic = Shape("Shell Fill", shell, Vector2.zero, new Vector2(124, 124), BOKSShapeGraphic.ShapeKind.Ellipse,
                new Color32(246, 252, 245, 255), new Color32(183, 232, 216, 255), 2f);
            shellGraphic.borderColor = new Color32(255, 255, 255, 210);
            shellGraphic.raycastTarget = true;
            startButton = shellGraphic.gameObject.AddComponent<Button>();
            startButton.targetGraphic = shellGraphic;
            startButton.transition = Selectable.Transition.None;
            startButton.interactable = false;
            startButton.onClick.AddListener(StartCampaign);
            flower = Rect("Goal Bubble Flower", shell, Vector2.zero, new Vector2(64, 64));
            flowerGroup = flower.gameObject.AddComponent<CanvasGroup>();
            AddFlower(flower);
            core = Rect("Goal Light Core", bubble, Vector2.zero, new Vector2(30, 30));
            coreGroup = core.gameObject.AddComponent<CanvasGroup>();
            Shape("Core Fill", core, Vector2.zero, new Vector2(30, 30), BOKSShapeGraphic.ShapeKind.Ellipse,
                new Color(1f, .98f, .68f, .56f), new Color(1f, .73f, .18f, 0f), 0f);
            ring = Rect("Bubble Pop Ring", bubble, Vector2.zero, new Vector2(38, 38));
            ringGroup = ring.gameObject.AddComponent<CanvasGroup>();
            ringGroup.alpha = 0f;
            BOKSShapeGraphic ringGraphic = Shape("Ring Fill", ring, Vector2.zero, new Vector2(38, 38), BOKSShapeGraphic.ShapeKind.Ellipse,
                new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), 2f);
            ringGraphic.borderColor = new Color(1f, 1f, 1f, .92f);
            CreateShards();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.GetComponent<EventSystem>().sendNavigationEvents = true;
            }
        }

        IEnumerator PlayMenuIntro()
        {
            yield return FadeGroup(introLogoGroup, 0f, 1f, IntroLogoFadeInSeconds, scale: Vector2.one);
            yield return WaitUnscaled(IntroLogoHoldSeconds);
            yield return FadeFinalComposition();
            finalCompositionGroup.blocksRaycasts = true;
            startButton.interactable = true;
        }

        IEnumerator FadeFinalComposition()
        {
            RectTransform rect = (RectTransform)finalCompositionGroup.transform;
            float elapsed = 0f;
            while (elapsed < FinalCompositionFadeInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = GateEase(Mathf.Clamp01(elapsed / FinalCompositionFadeInSeconds));
                finalCompositionGroup.alpha = t;
                rect.localScale = Vector3.one * Mathf.Lerp(.985f, 1f, t);
                yield return null;
            }
            finalCompositionGroup.alpha = 1f;
            rect.localScale = Vector3.one;
        }

        IEnumerator FadeGroup(CanvasGroup group, float from, float to, float seconds, Vector2 scale)
        {
            RectTransform rect = (RectTransform)group.transform;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = GateEase(Mathf.Clamp01(elapsed / seconds));
                group.alpha = Mathf.Lerp(from, to, t);
                rect.localScale = Vector3.one * Mathf.Lerp(scale.x, scale.y, t);
                yield return null;
            }
            group.alpha = to;
            rect.localScale = Vector3.one * scale.y;
        }

        static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime) yield return null;
        }

        void StartCampaign()
        {
            if (startButton == null || !startButton.interactable) return;
            startButton.interactable = false;
            isPopping = true;
            BOKSAudioManager.Play(BOKSAudioCue.Welcome);
            BOKSAudioManager.Play(BOKSAudioCue.BubblePop);
            StartCoroutine(OpenCampaign());
        }

        void Update()
        {
            if (isPopping) return;
            float drift = Mathf.Repeat(Time.unscaledTime / 5.2f, 1f);
            Vector3 pose = DriftPose(drift);
            bubble.anchoredPosition = bubbleHome + new Vector2(pose.x, pose.y);
            bubble.localRotation = Quaternion.Euler(0f, 0f, pose.z);

            float shellPhase = Mathf.Repeat(Time.unscaledTime / 4.6f, 1f);
            float shellLift = shellPhase < .30f ? Mathf.Lerp(0f, 1.5f, Ease(shellPhase / .30f)) :
                shellPhase < .62f ? Mathf.Lerp(1.5f, 3f, Ease((shellPhase - .30f) / .32f)) : Mathf.Lerp(3f, 0f, Ease((shellPhase - .62f) / .38f));
            shell.anchoredPosition = new Vector2(0f, shellLift);
            shell.localScale = Vector3.one * (1f + shellLift * .006f);
            flower.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.unscaledTime / 2.8f * Mathf.PI * 2f) * 1.5f);
            float glowPulse = .5f + .5f * Mathf.Sin(Time.unscaledTime / 3.1f * Mathf.PI * 2f - Mathf.PI * .5f);
            glow.localScale = Vector3.one * Mathf.Lerp(.98f, 1.04f, glowPulse);
            glowGroup.alpha = Mathf.Lerp(.76f, .98f, glowPulse);
        }

        IEnumerator OpenCampaign()
        {
            float elapsed = 0f;
            while (elapsed < StartPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                AnimatePop(Mathf.Clamp01(elapsed / PopVisualSeconds));
                yield return null;
            }
            // The web keeps the start gate above the app while it fades away. Persisting this
            // root lets the Campaign initialise behind the same visual instead of hard-cutting.
            DontDestroyOnLoad(gameObject);
            StartCoroutine(OpenCampaignBehindGate());
        }

        IEnumerator OpenCampaignBehindGate()
        {
            CreateLevelRevealMask();
            AsyncOperation load = SceneManager.LoadSceneAsync("BOKS_Campaign");
            float elapsed = 0f;
            while (elapsed < GateFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = GateEase(Mathf.Clamp01(elapsed / GateFadeSeconds));
                menuGroup.alpha = 1f - t;
                menuRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.025f, t);
                yield return null;
            }

            menuGroup.alpha = 0f;
            menuGroup.blocksRaycasts = false;
            while (!load.isDone) yield return null;
            yield return RevealLevelOne();
            if (revealMask != null) Destroy(revealMask.gameObject);
            Destroy(gameObject);
        }

        void CreateLevelRevealMask()
        {
            GameObject maskGo = new GameObject("Level 1 Reveal Mask", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = maskGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            // Above the ScreenSpaceCamera campaign, below the existing ScreenSpaceOverlay menu gate.
            canvas.sortingOrder = -10;
            CanvasScaler scaler = maskGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(520f, 1000f);
            scaler.matchWidthOrHeight = .5f;
            revealMask = maskGo.GetComponent<CanvasGroup>();
            revealMask.alpha = 1f;
            revealMask.blocksRaycasts = true;

            Image neutral = new GameObject("Notebook Neutral", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            neutral.transform.SetParent(maskGo.transform, false);
            RectTransform rect = neutral.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            neutral.color = new Color32(247, 247, 241, 255); // web body.prestart notebook neutral
            neutral.raycastTarget = true;
            DontDestroyOnLoad(maskGo);
        }

        IEnumerator RevealLevelOne()
        {
            BOKSCampaignView campaign = FindAnyObjectByType<BOKSCampaignView>();
            if (campaign == null) yield break;

            RectTransform presentation = campaign.transform as RectTransform;
            Vector3 homeScale = presentation != null ? presentation.localScale : Vector3.one;
            Vector2 homePosition = presentation != null ? presentation.anchoredPosition : Vector2.zero;
            CanvasGroup presentationGroup = campaign.GetComponent<CanvasGroup>();
            if (presentationGroup == null) presentationGroup = campaign.gameObject.AddComponent<CanvasGroup>();
            presentationGroup.blocksRaycasts = false;
            campaign.Gameplay.SetCampaignInputLocked(true);
            if (presentation != null)
            {
                presentation.localScale = homeScale * .985f;
                presentation.anchoredPosition = homePosition + Vector2.down * 10f;
            }

            // Matches removal of body.prestart: input resumes at the start of the soft app reveal.
            yield return null;
            revealMask.blocksRaycasts = false;
            presentationGroup.blocksRaycasts = true;
            campaign.Gameplay.SetCampaignInputLocked(false);

            float elapsed = 0f;
            while (elapsed < LevelRevealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float opacityT = GateEase(Mathf.Clamp01(elapsed / LevelRevealSeconds));
                revealMask.alpha = 1f - opacityT;
                if (presentation != null)
                {
                    float settleT = GateEase(Mathf.Clamp01(elapsed / LevelSettleSeconds));
                    presentation.localScale = Vector3.Lerp(homeScale * .985f, homeScale, settleT);
                    presentation.anchoredPosition = Vector2.Lerp(homePosition + Vector2.down * 10f, homePosition, settleT);
                }
                yield return null;
            }

            revealMask.alpha = 0f;
            if (presentation != null)
            {
                presentation.localScale = homeScale;
                presentation.anchoredPosition = homePosition;
            }
        }

        void AnimatePop(float p)
        {
            float pressed = Mathf.Lerp(1f, .96f, Mathf.Clamp01(p / (.16f / PopVisualSeconds)));
            bubble.localScale = Vector3.one * pressed;
            shell.anchoredPosition = new Vector2(0f, Mathf.Lerp(2f, 8f, p));
            shell.localScale = Vector3.one * Mathf.Lerp(1.02f, 1.28f, p);
            shellGroup.alpha = Mathf.Lerp(1f, 0f, p);
            flower.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 8f, p));
            flower.localScale = Vector3.one * Mathf.Lerp(1f, .7f, p);
            flowerGroup.alpha = Mathf.Lerp(.96f, 0f, p);
            glow.localScale = Vector3.one * Mathf.Lerp(1f, 1.34f, p);
            glowGroup.alpha = Mathf.Lerp(.9f, 0f, p);
            bloom.localScale = Vector3.one * (p < .38f ? Mathf.Lerp(.86f, 1.42f, p / .38f) : Mathf.Lerp(1.42f, 2.4f, (p - .38f) / .62f));
            bloomGroup.alpha = p < .38f ? Mathf.Lerp(.42f, .92f, p / .38f) : Mathf.Lerp(.92f, 0f, (p - .38f) / .62f);
            core.localScale = Vector3.one * (p < .28f ? Mathf.Lerp(.9f, 1.24f, p / .28f) : Mathf.Lerp(1.24f, .52f, (p - .28f) / .72f));
            coreGroup.alpha = p < .28f ? Mathf.Lerp(.68f, 1f, p / .28f) : Mathf.Lerp(1f, 0f, (p - .28f) / .72f);
            ring.localScale = Vector3.one * Mathf.Lerp(.35f, 3.2f, p);
            ringGroup.alpha = p < .18f ? p / .18f * .95f : Mathf.Lerp(.95f, 0f, (p - .18f) / .82f);
            AnimateShards(p);
        }

        void CreateShards()
        {
            float[] angles = { 18f, 72f, 132f, -36f };
            for (int i = 0; i < shards.Length; i++)
            {
                RectTransform shard = Rect("Bubble Pop Shard", bubble, Vector2.zero, new Vector2(18, 4));
                shard.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                shards[i] = shard;
                shardGroups[i] = shard.gameObject.AddComponent<CanvasGroup>();
                shardGroups[i].alpha = 0f;
                Shape("Shard Fill", shard, Vector2.zero, new Vector2(18, 4), BOKSShapeGraphic.ShapeKind.RoundedRectangle,
                    new Color(1f, 1f, 1f, .96f), new Color(.75f, .94f, 1f, .8f), 0f);
            }
        }

        void AnimateShards(float p)
        {
            float[] angles = { 18f, 72f, 132f, -36f };
            float[] distances = { 20f, 18f, 18f, 21f };
            for (int i = 0; i < shards.Length; i++)
            {
                float angle = angles[i] * Mathf.Deg2Rad;
                shards[i].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distances[i] * p;
                shards[i].localScale = Vector3.one * Mathf.Lerp(.7f, i == 0 || i == 3 ? 1.05f : 1f, p);
                shardGroups[i].alpha = p < .16f ? p / .16f : Mathf.Lerp(1f, 0f, (p - .16f) / .84f);
            }
        }

        static Vector3 DriftPose(float p)
        {
            Vector3[] keys = { new Vector3(0f, 0f, -4f), new Vector3(2f, 5f, -1f), new Vector3(-3f, 10f, 3f), new Vector3(3f, 4f, 1f), new Vector3(0f, 0f, -4f) };
            float scaled = p * 4f;
            int index = Mathf.Min(3, Mathf.FloorToInt(scaled));
            return Vector3.Lerp(keys[index], keys[index + 1], Ease(scaled - index));
        }

        static float Ease(float value) => value * value * (3f - 2f * value);

        // CSS cubic-bezier(0.22, 1, 0.36, 1), solved for y at the supplied x progress.
        static float GateEase(float x)
        {
            const float x1 = .22f, y1 = 1f, x2 = .36f, y2 = 1f;
            float cx = 3f * x1;
            float bx = 3f * (x2 - x1) - cx;
            float ax = 1f - cx - bx;
            float cy = 3f * y1;
            float by = 3f * (y2 - y1) - cy;
            float ay = 1f - cy - by;
            float t = x;
            for (int i = 0; i < 6; i++)
            {
                float sampleX = ((ax * t + bx) * t + cx) * t - x;
                float derivative = (3f * ax * t + 2f * bx) * t + cx;
                if (Mathf.Abs(sampleX) < .00001f || Mathf.Abs(derivative) < .00001f) break;
                t = Mathf.Clamp01(t - sampleX / derivative);
            }
            return ((ay * t + by) * t + cy) * t;
        }

        RectTransform Logo(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Image logo = rect.gameObject.AddComponent<Image>();
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            return rect;
        }

        static void AddFlower(RectTransform parent)
        {
            Color petal = new Color32(255, 117, 105, 255);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f + Mathf.PI * .5f;
                Shape("Flower Petal", parent, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 24f, new Vector2(34, 34),
                    BOKSShapeGraphic.ShapeKind.Ellipse, petal, new Color32(237, 82, 74, 255), 0f);
            }
            Shape("Flower Centre", parent, Vector2.zero, new Vector2(30, 30), BOKSShapeGraphic.ShapeKind.Ellipse,
                new Color32(246, 205, 80, 255), new Color32(229, 163, 58, 255), 0f);
        }

        static RectTransform Rect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static BOKSShapeGraphic Shape(string name, RectTransform parent, Vector2 position, Vector2 size, BOKSShapeGraphic.ShapeKind kind, Color top, Color bottom, float border)
        {
            BOKSShapeGraphic shape = Rect(name, parent, position, size).gameObject.AddComponent<BOKSShapeGraphic>();
            shape.shape = kind; shape.topColor = top; shape.bottomColor = bottom; shape.borderWidth = border; shape.raycastTarget = false;
            return shape;
        }

    }
}
