using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Visual-only handoff between consecutive campaign levels. It keeps the live presentation in place,
    /// stages the next level below it, then scrolls both as a single camera-like move.
    /// </summary>
    public sealed class BOKSLevelScrollTransition : MonoBehaviour
    {
        const float CompletionPauseSeconds = .10f;
        const float ScrollSeconds = 1.5f;
        const float FinalArrivalSettleSeconds = .20f;
        const float GapViewportFraction = .12f;
        const float HeroArcHeight = 24f;
        const float HeroIncomingLeadPixels = 16f;
        const float DirectionTurnStart = .30f;
        const float DirectionSpriteBlendStart = .43f;
        const float DirectionTurnFinish = .80f;
        const float DirectionTurnDegrees = 18f;
        const float AdjacentDirectionBlendSeconds = .10f;
        const float OppositeDirectionBlendSeconds = .12f;
        const bool ShowLevelNumber = true;

        Coroutine activeTransition;

        public void Play(BOKSCampaignView liveView, BOKSLevelDefinition nextLevel, Action handoff)
        {
            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(PlayRoutine(liveView, nextLevel, handoff));
        }

        IEnumerator PlayRoutine(BOKSCampaignView liveView, BOKSLevelDefinition nextLevel, Action handoff)
        {
            if (liveView == null || nextLevel == null)
            {
                handoff?.Invoke();
                yield break;
            }

            RectTransform liveStage = liveView.transform as RectTransform;
            BOKSLevel2Controller liveGameplay = liveView.Gameplay;
            BOKSCampaignView.VisualTransitionPreview preview = liveView.CreateVisualTransitionPreview(nextLevel);
            RectTransform previewStage = preview != null ? preview.Stage : null;
            if (liveStage == null || previewStage == null || preview.HeroArt == null || liveGameplay == null || liveGameplay.HeroArt == null)
            {
                if (previewStage != null) Destroy(previewStage.gameObject);
                handoff?.Invoke();
                yield break;
            }

            Vector2 home = liveStage.anchoredPosition;
            float viewportHeight = liveStage.rect.height;
            float gapHeight = viewportHeight * GapViewportFraction;
            float travel = viewportHeight + gapHeight;
            previewStage.anchoredPosition = home + Vector2.down * travel;

            RectTransform levelLabel = ShowLevelNumber ? CreateGapLabel(liveStage.parent as RectTransform, home, viewportHeight, gapHeight, nextLevel.levelNumber) : null;
            // Briefly preserve the completed gameplay pose before visual ownership changes.
            yield return WaitUnscaled(CompletionPauseSeconds);
            Image originalHeroImage = liveGameplay.HeroArt;
            RectTransform transitionHero = CreatePixelMatchedProxy(liveStage.parent as RectTransform, originalHeroImage, out Image proxyImage);
            CanvasGroup originalVisualGroup = liveGameplay.HeroVisual.GetComponent<CanvasGroup>();
            if (originalVisualGroup == null) originalVisualGroup = liveGameplay.HeroVisual.gameObject.AddComponent<CanvasGroup>();
            float originalVisualAlpha = originalVisualGroup.alpha;
            // The proxy is configured now but remains non-rendering for the current frame. This
            // guarantees the current render contains only the original visual.
            yield return new WaitForEndOfFrame();
            // No yield separates these writes: the next rendered frame contains only the proxy visual.
            proxyImage.enabled = true;
            originalVisualGroup.alpha = 0f;
            Vector2 transitionHeroStart = transitionHero.anchoredPosition;
            Vector2 transferControl = transitionHeroStart + FacingVector(liveGameplay.Facing) * HeroIncomingLeadPixels;
            DirectionSpriteTurn directionTurn = CreateDirectionSpriteTurn(
                transitionHero, proxyImage, preview.HeroArt.GetComponent<Image>(), liveGameplay.Facing, nextLevel.Direction);

            for (float elapsed = 0f; elapsed < ScrollSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float timelineProgress = Mathf.Clamp01(elapsed / ScrollSeconds);
                float t = EaseInOutCubic(timelineProgress);
                Vector2 offset = Vector2.up * (travel * t);
                liveStage.anchoredPosition = home + offset;
                previewStage.anchoredPosition = home + Vector2.down * travel + offset;
                if (levelLabel != null) levelLabel.anchoredPosition = home + Vector2.down * (viewportHeight + gapHeight * .5f) + offset;
                Vector2 target = ParentPoint(liveStage.parent as RectTransform, preview.HeroArt);
                transitionHero.anchoredPosition = QuadraticBezier(transitionHeroStart, transferControl, target, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * HeroArcHeight);
                directionTurn.Update(timelineProgress);
                yield return null;
            }

            liveStage.anchoredPosition = home + Vector2.up * travel;
            previewStage.anchoredPosition = home;
            transitionHero.anchoredPosition = ParentPoint(liveStage.parent as RectTransform, preview.HeroArt);
            directionTurn.Complete();
            if (levelLabel != null) Destroy(levelLabel.gameObject);
            yield return WaitUnscaled(FinalArrivalSettleSeconds);

            // Hide the visual stand-in first, then rebuild the live board while it remains above
            // the viewport. The live board is returned to home only after existing ApplyLevel has
            // produced the real next-level state, avoiding a visible rebuild/snap.
            previewStage.gameObject.SetActive(false);
            handoff?.Invoke();
            liveStage.anchoredPosition = home;
            originalVisualGroup.alpha = originalVisualAlpha;
            Destroy(transitionHero.gameObject);
            Destroy(previewStage.gameObject);
            activeTransition = null;
        }

        static Vector2 ParentPoint(RectTransform parent, RectTransform target)
        {
            return parent.InverseTransformPoint(target.position);
        }

        static Vector2 FacingVector(BOKSDirection direction)
        {
            switch (direction)
            {
                case BOKSDirection.Up: return Vector2.up;
                case BOKSDirection.Left: return Vector2.left;
                case BOKSDirection.Down: return Vector2.down;
                default: return Vector2.right;
            }
        }

        static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            Vector2 first = Vector2.Lerp(start, control, t);
            Vector2 second = Vector2.Lerp(control, end, t);
            return Vector2.Lerp(first, second, t);
        }

        static DirectionSpriteTurn CreateDirectionSpriteTurn(RectTransform proxy, Image outgoing, Image incoming, BOKSDirection currentDirection, BOKSDirection nextDirection)
        {
            if (outgoing == null || incoming == null || currentDirection == nextDirection || outgoing.sprite == incoming.sprite)
                return DirectionSpriteTurn.None;

            float angularDifference = Mathf.DeltaAngle(DirectionAngle(currentDirection), DirectionAngle(nextDirection));
            float blendSeconds = Mathf.Abs(angularDifference) == 180f ? OppositeDirectionBlendSeconds : AdjacentDirectionBlendSeconds;
            Image incomingOverlay = CreateDirectionSpriteOverlay(proxy, incoming);
            return new DirectionSpriteTurn(proxy, outgoing, incomingOverlay, incoming.sprite, incoming.color,
                proxy.localRotation, Mathf.Sign(angularDifference) * DirectionTurnDegrees,
                DirectionSpriteBlendStart, blendSeconds / ScrollSeconds, DirectionTurnFinish);
        }

        static float DirectionAngle(BOKSDirection direction)
        {
            switch (direction)
            {
                case BOKSDirection.Up: return 90f;
                case BOKSDirection.Left: return 180f;
                case BOKSDirection.Down: return 270f;
                default: return 0f;
            }
        }

        static Image CreateDirectionSpriteOverlay(RectTransform proxy, Image source)
        {
            GameObject overlayObject = new GameObject("IncomingDirectionArt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(proxy, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlayRect.localRotation = Quaternion.identity;
            overlayRect.localScale = Vector3.one;

            Image overlay = overlayObject.GetComponent<Image>();
            overlay.sprite = source.sprite;
            overlay.color = WithAlpha(source.color, 0f);
            overlay.material = source.material;
            overlay.preserveAspect = source.preserveAspect;
            overlay.type = source.type;
            overlay.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            overlay.raycastTarget = false;
            return overlay;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        sealed class DirectionSpriteTurn
        {
            public static readonly DirectionSpriteTurn None = new DirectionSpriteTurn();

            readonly RectTransform proxy;
            readonly Image outgoing;
            readonly Image incomingOverlay;
            readonly Sprite incomingSprite;
            readonly Color incomingColor;
            readonly Color outgoingColor;
            readonly Quaternion baseRotation;
            readonly float turnDegrees;
            readonly float blendStart;
            readonly float blendDuration;
            readonly float finish;
            bool spriteCommitted;

            DirectionSpriteTurn() { }

            public DirectionSpriteTurn(RectTransform proxy, Image outgoing, Image incomingOverlay, Sprite incomingSprite, Color incomingColor,
                Quaternion baseRotation, float turnDegrees, float blendStart, float blendDuration, float finish)
            {
                this.proxy = proxy;
                this.outgoing = outgoing;
                this.incomingOverlay = incomingOverlay;
                this.incomingSprite = incomingSprite;
                this.incomingColor = incomingColor;
                outgoingColor = outgoing.color;
                this.baseRotation = baseRotation;
                this.turnDegrees = turnDegrees;
                this.blendStart = blendStart;
                this.blendDuration = blendDuration;
                this.finish = finish;
            }

            public void Update(float progress)
            {
                if (proxy == null) return;
                float rotationProgress = progress <= DirectionTurnStart
                    ? 0f
                    : progress < DirectionSpriteBlendStart
                        ? EaseInOutCubic(Mathf.InverseLerp(DirectionTurnStart, DirectionSpriteBlendStart, progress))
                        : 1f - EaseInOutCubic(Mathf.InverseLerp(DirectionSpriteBlendStart, finish, progress));
                proxy.localRotation = baseRotation * Quaternion.Euler(0f, 0f, turnDegrees * rotationProgress);

                if (incomingOverlay == null || outgoing == null) return;
                float blend = Mathf.Clamp01((progress - blendStart) / blendDuration);
                if (!spriteCommitted)
                {
                    outgoing.color = WithAlpha(outgoingColor, outgoingColor.a * (1f - blend));
                    incomingOverlay.color = WithAlpha(incomingColor, incomingColor.a * blend);
                    if (blend < 1f) return;
                    outgoing.sprite = incomingSprite;
                    outgoing.color = incomingColor;
                    incomingOverlay.gameObject.SetActive(false);
                    spriteCommitted = true;
                }
            }

            public void Complete()
            {
                if (proxy == null) return;
                proxy.localRotation = baseRotation;
                if (outgoing != null && incomingSprite != null)
                {
                    outgoing.sprite = incomingSprite;
                    outgoing.color = incomingColor;
                }
                if (incomingOverlay != null) UnityEngine.Object.Destroy(incomingOverlay.gameObject);
            }
        }

        static RectTransform CreatePixelMatchedProxy(RectTransform layer, Image source, out Image image)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform sourceRect = source.rectTransform;
            Vector3[] worldCorners = new Vector3[4];
            sourceRect.GetWorldCorners(worldCorners);
            Vector2 bottomLeft = layer.InverseTransformPoint(worldCorners[0]);
            Vector2 topLeft = layer.InverseTransformPoint(worldCorners[1]);
            Vector2 topRight = layer.InverseTransformPoint(worldCorners[2]);
            Vector2 bottomRight = layer.InverseTransformPoint(worldCorners[3]);

            GameObject proxyObject = new GameObject("TransitionBoksVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform proxy = proxyObject.GetComponent<RectTransform>();
            proxy.SetParent(layer, false);
            proxy.anchorMin = proxy.anchorMax = proxy.pivot = new Vector2(.5f, .5f);
            proxy.localScale = Vector3.one;
            proxy.localRotation = Quaternion.Inverse(layer.rotation) * sourceRect.rotation;
            proxy.sizeDelta = new Vector2(Vector2.Distance(bottomLeft, bottomRight), Vector2.Distance(bottomLeft, topLeft));
            proxy.anchoredPosition = (bottomLeft + topRight) * .5f;
            image = proxyObject.GetComponent<Image>();
            image.sprite = source.sprite;
            image.color = source.color;
            image.material = source.material;
            image.preserveAspect = source.preserveAspect;
            image.type = source.type;
            image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            image.raycastTarget = false;
            image.enabled = false;
            proxy.SetAsLastSibling();
            return proxy;
        }

        static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime) yield return null;
        }

        static float EaseInOutCubic(float t) => t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * .5f;

        static RectTransform CreateGapLabel(RectTransform parent, Vector2 home, float viewportHeight, float gapHeight, int levelNumber)
        {
            GameObject labelObject = new GameObject($"Level Transition Label {levelNumber:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(120f, 52f);
            rect.anchoredPosition = home + Vector2.down * (viewportHeight + gapHeight * .5f);
            Text text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 25;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(46f / 255f, 97f / 255f, 128f / 255f, .55f);
            text.text = levelNumber.ToString("00");
            text.raycastTarget = false;
            return rect;
        }
    }
}
