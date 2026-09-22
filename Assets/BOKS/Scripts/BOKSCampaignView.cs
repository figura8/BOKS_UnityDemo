using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BOKS.Demo
{
    /// <summary>Applies source level data to the one persistent campaign board.</summary>
    public sealed class BOKSCampaignView : MonoBehaviour
    {
        [SerializeField] BOKSLevel2Controller gameplay;
        [SerializeField] RectTransform grid;
        [SerializeField] RectTransform goal;
        [SerializeField] RectTransform objectLayer;
        [SerializeField] RectTransform overlayLayer;
        [SerializeField] RectTransform[] slotRoots;
        [SerializeField] Button[] paletteButtons;
        [SerializeField] GameObject[] paletteGlowLayers;
        [SerializeField] Sprite[] characterSprites;
        [SerializeField] Sprite treeSprite;
        [SerializeField] Sprite daisySprite;
        [SerializeField] Sprite beeSprite;
        string campaignPlayerCharacterId;
        readonly List<BOKSDecorationReaction> decorationReactions = new List<BOKSDecorationReaction>();
        BOKSLevel1Onboarding onboarding;

        readonly BOKSCommandType[] paletteOrder = {
            BOKSCommandType.Forward, BOKSCommandType.Left, BOKSCommandType.Right, BOKSCommandType.Function
        };

        public BOKSLevel2Controller Gameplay => gameplay;

        /// <summary>Campaign-owned character selection. Level data remains intact but no longer replaces this set.</summary>
        public void SetCampaignPlayerCharacter(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId)) campaignPlayerCharacterId = characterId;
        }

        public sealed class VisualTransitionPreview
        {
            public RectTransform Stage { get; }
            public RectTransform Hero { get; }
            public RectTransform HeroVisual { get; }
            public RectTransform HeroArt { get; }

            public VisualTransitionPreview(RectTransform stage, RectTransform hero, RectTransform heroVisual, RectTransform heroArt)
            {
                Stage = stage;
                Hero = hero;
                HeroVisual = heroVisual;
                HeroArt = heroArt;
            }
        }

        /// <summary>
        /// Creates an inactive copy of this presentation and applies a level definition to it for
        /// a campaign-level scroll transition. Its gameplay/controller scripts are disabled before it
        /// becomes visible, so it is strictly a visual staging board.
        /// </summary>
        public VisualTransitionPreview CreateVisualTransitionPreview(BOKSLevelDefinition level)
        {
            GameObject source = gameObject;
            RectTransform liveStage = transform as RectTransform;
            GameObject stageObject = new GameObject("Next Level Visual Preview Stage", typeof(RectTransform));
            RectTransform stage = stageObject.GetComponent<RectTransform>();
            stage.SetParent(transform.parent, false);
            stage.anchorMin = liveStage.anchorMin;
            stage.anchorMax = liveStage.anchorMax;
            stage.pivot = liveStage.pivot;
            stage.sizeDelta = liveStage.sizeDelta;
            stage.anchoredPosition = liveStage.anchoredPosition;
            // An inactive parent prevents the clone's Awake from ever running before its gameplay
            // scripts are removed. This is what prevents the Level 1 spawn visual from flashing.
            stageObject.SetActive(false);
            GameObject previewObject = Instantiate(source, stage, false);
            RectTransform previewRect = previewObject.transform as RectTransform;
            previewRect.anchoredPosition = Vector2.zero;

            previewObject.name = "Next Level Visual Preview";
            BOKSCampaignView preview = previewObject.GetComponent<BOKSCampaignView>();
            BOKSCampaignController previewCampaign = previewObject.GetComponent<BOKSCampaignController>();
            BOKSLevel2Controller previewGameplay = previewObject.GetComponent<BOKSLevel2Controller>();
            BOKSLevel1Onboarding previewOnboarding = previewObject.GetComponent<BOKSLevel1Onboarding>();
            if (previewCampaign != null) previewCampaign.enabled = false;
            if (previewGameplay != null) previewGameplay.enabled = false;
            if (previewOnboarding != null) previewOnboarding.enabled = false;
            if (preview != null) preview.enabled = false;

            // Instantiate does not copy this runtime-only private field. Carry the campaign-owned
            // character set into the visual-only preview before it resolves the next direction's
            // sprite, so transition blending can never fall back to the level-authored colour.
            if (preview != null) preview.SetCampaignPlayerCharacter(campaignPlayerCharacterId);

            // ApplyLevel is safe to call while the copy is inactive: it only constructs and lays
            // out UI from the existing data-driven definition. No cloned controller is enabled.
            preview.ApplyLevel(level);
            RectTransform previewHero = previewGameplay != null ? previewGameplay.HeroRoot : null;
            RectTransform previewHeroVisual = previewGameplay != null ? previewGameplay.HeroVisual : null;
            RectTransform previewHeroArt = previewGameplay != null && previewGameplay.HeroArt != null ? previewGameplay.HeroArt.rectTransform : null;
            if (previewGameplay != null) previewGameplay.SetTransitionHeroVisible(false);
            CanvasGroup previewGroup = previewObject.GetComponent<CanvasGroup>();
            if (previewGroup == null) previewGroup = previewObject.AddComponent<CanvasGroup>();
            previewGroup.interactable = false;
            previewGroup.blocksRaycasts = false;

            // Remove the clone's runtime owners while it is still inactive. This guarantees that
            // activating the preview cannot run a second Campaign/Level controller or overwrite
            // the staged next-level data through an Awake callback.
            if (previewCampaign != null) DestroyImmediate(previewCampaign);
            if (previewGameplay != null) DestroyImmediate(previewGameplay);
            if (previewOnboarding != null) DestroyImmediate(previewOnboarding);
            if (preview != null) DestroyImmediate(preview);
            stageObject.SetActive(true);
            return new VisualTransitionPreview(stage, previewHero, previewHeroVisual, previewHeroArt);
        }

        // Configure is an editor scene-builder API. At runtime Unity restores the serialized
        // references directly, so the onboarding component must be installed/bound here as well.
        void Awake() => EnsureRuntimeBindings();

        void EnsureRuntimeBindings()
        {
            if (gameplay == null || paletteButtons == null || paletteButtons.Length == 0 || slotRoots == null) return;
            BOKSProgramAreaPresentation.Ensure(transform as RectTransform);
            gameplay.HeroEnteredCell -= TriggerDecorationReactionsAt;
            gameplay.HeroEnteredCell += TriggerDecorationReactionsAt;
            onboarding = GetComponent<BOKSLevel1Onboarding>();
            if (onboarding == null) onboarding = gameObject.AddComponent<BOKSLevel1Onboarding>();
            onboarding.Bind(gameplay, (RectTransform)transform, paletteButtons[0], slotRoots, gameplay.PlayButton);

            // CampaignController may have loaded Level 1 in its Awake before this component's
            // Awake created the onboarding. Start that already-loaded first-level state here;
            // normal level loads still start it from ApplyLevel below.
            if (gameplay.LevelNumber == 1) onboarding.Begin(true);
        }

        public void Configure(BOKSLevel2Controller controller, RectTransform gridRoot, RectTransform goalRoot,
            RectTransform objects, RectTransform overlays, RectTransform[] slots, Button[] buttons, GameObject[] glows,
            Sprite[] allCharacterSprites, Sprite tree, Sprite daisy, Sprite bee)
        {
            gameplay = controller;
            grid = gridRoot;
            goal = goalRoot;
            objectLayer = objects;
            overlayLayer = overlays;
            slotRoots = slots;
            paletteButtons = buttons;
            paletteGlowLayers = glows;
            characterSprites = allCharacterSprites;
            treeSprite = tree;
            daisySprite = daisy;
            beeSprite = bee;
            EnsureRuntimeBindings();
        }

        public void ApplyLevel(BOKSLevelDefinition level)
        {
            ClearDynamicLayer(objectLayer);
            ClearDynamicLayer(overlayLayer);
            decorationReactions.Clear();
            ClearOldObstacles();

            goal.gameObject.SetActive(level.goal != null);
            if (level.goal != null)
            {
                goal.anchoredPosition = new Vector2(6 + level.GoalColumn * 75f + 35.5f - 54f,
                    -(6 + level.GoalRow * 75f + 35.5f - 54f));
                ApplyGoalIdle();
            }

            for (int i = 0; i < slotRoots.Length; i++)
            {
                bool enabled = i < 8 ? level.IsMainSlotEnabled(i) : level.IsFunctionSlotEnabled(i - 8);
                // Fade only the card visuals ("Slot Card Visuals"), never the slot root.
                // The root also owns the "Connector Occlusion Mask", which must stay opaque so the
                // sequence connector remains behind every slot card. Fading the root would also leave
                // a stale card alpha on a slot that transitions from locked to enabled, washing out a
                // placed command.
                Transform visuals = slotRoots[i].Find("Slot Card Visuals");
                if (visuals != null)
                {
                    CanvasGroup group = visuals.GetComponent<CanvasGroup>();
                    if (group == null) group = visuals.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = enabled ? 1f : .34f;
                }
                BOKSProgramDropSlot drop = slotRoots[i].GetComponent<BOKSProgramDropSlot>();
                if (drop != null) drop.SetEnabled(enabled);
            }

            for (int i = 0; i < paletteButtons.Length && i < paletteOrder.Length; i++)
                paletteButtons[i].gameObject.SetActive(level.IsCommandEnabled(paletteOrder[i]));
            // The web applies its available-block glow only to enabled palette blocks. The scene
            // keeps each block's three glow circles as command-area siblings, so gate each group.
            for (int i = 0; i < paletteGlowLayers.Length; i++)
            {
                int paletteIndex = i / 3;
                bool showGlow = level.glowEnabled && paletteIndex < paletteButtons.Length && paletteButtons[paletteIndex].gameObject.activeSelf;
                if (paletteGlowLayers[i] != null) paletteGlowLayers[i].SetActive(showGlow);
            }

            if (level.obstacles != null)
                foreach (BOKSGridCell cell in level.obstacles)
                    if (cell != null) AddObstacle(cell.x, cell.y);
            if (level.decorations != null)
                foreach (BOKSDecoration decoration in level.decorations)
                    if (decoration != null) AddDecoration(decoration);

            string activeCharacterId = !string.IsNullOrEmpty(campaignPlayerCharacterId)
                ? campaignPlayerCharacterId
                : level.characterId;
            gameplay.LoadLevel(level, SpritesFor(activeCharacterId));
            onboarding?.Begin(level.levelNumber == 1);
        }

        public void SetEditorCommandEnabled(BOKSCommandType command, bool enabled)
        {
            int index = System.Array.IndexOf(paletteOrder, command);
            if (index >= 0 && index < paletteButtons.Length)
                paletteButtons[index].gameObject.SetActive(enabled);
            gameplay.SetEditorCommandEnabled(command, enabled);
            for (int i = 0; i < paletteGlowLayers.Length; i++)
            {
                int paletteIndex = i / 3;
                if (paletteIndex == index && paletteGlowLayers[i] != null)
                    paletteGlowLayers[i].SetActive(enabled && gameplay.LogicalProgramCount == 0);
            }
        }

        public void SetEditorSlotEnabled(int slotIndex, bool enabled)
        {
            if (slotIndex < 0 || slotIndex >= slotRoots.Length) return;
            Transform visuals = slotRoots[slotIndex].Find("Slot Card Visuals");
            if (visuals != null)
            {
                CanvasGroup group = visuals.GetComponent<CanvasGroup>();
                if (group == null) group = visuals.gameObject.AddComponent<CanvasGroup>();
                group.alpha = enabled ? 1f : .34f;
            }
            BOKSProgramDropSlot drop = slotRoots[slotIndex].GetComponent<BOKSProgramDropSlot>();
            if (drop != null) drop.SetEnabled(enabled);
            gameplay.SetEditorSlotEnabled(slotIndex, enabled);
        }

        /// <summary>
        /// Installs/refreshes the source goal-bubble idle (drift, pulse, wobble, glow) on the goal
        /// root. The rest pose must be re-cached after the per-level reposition.
        /// </summary>
        void ApplyGoalIdle()
        {
            if (goal == null) return;
            BOKSGoalBubbleIdle idle = goal.GetComponent<BOKSGoalBubbleIdle>();
            if (idle == null) idle = goal.gameObject.AddComponent<BOKSGoalBubbleIdle>();
            idle.CaptureRestPose();
        }

        Sprite[] SpritesFor(string characterId)
        {
            int character = characterId == "boks_yellow" ? 1 : characterId == "boks_blu" ? 2 : characterId == "boks_green" ? 3 : 0;
            var result = new Sprite[4];
            for (int i = 0; i < 4; i++) result[i] = characterSprites[character * 4 + i];
            return result;
        }

        void ClearDynamicLayer(RectTransform layer)
        {
            if (layer == null) return;
            for (int i = layer.childCount - 1; i >= 0; i--) RemoveLevelObject(layer.GetChild(i).gameObject);
        }

        void ClearOldObstacles()
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
                if (grid.GetChild(i).name.StartsWith("Obstacle (")) RemoveLevelObject(grid.GetChild(i).gameObject);
        }

        static void RemoveLevelObject(GameObject target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        void AddObstacle(int x, int y)
        {
            RectTransform rt = NewRect($"Obstacle ({x},{y})", grid, 7 + x * 75f, 7 + y * 75f, 71, 71);
            BOKSShapeGraphic shape = rt.gameObject.AddComponent<BOKSShapeGraphic>();
            shape.topColor = new Color32(205, 184, 148, 255);
            shape.bottomColor = new Color32(181, 156, 116, 255);
            shape.borderColor = new Color32(133, 104, 67, 190);
            shape.borderWidth = 1f;
            shape.cornerRadius = 8f;
            shape.raycastTarget = false;
            rt.SetSiblingIndex(Mathf.Max(0, objectLayer.GetSiblingIndex()));
        }

        void AddDecoration(BOKSDecoration d)
        {
            RectTransform parent = d.layer == "overlay" ? overlayLayer : objectLayer;
            int count = Mathf.Max(1, d.count);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : i * Mathf.PI * 2f / count;
                float spread = count == 1 ? 0f : 12f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spread;
                if (d.asset == "bridge") AddBridge(parent, d, offset, i);
                else
                {
                    Sprite sprite = d.asset == "tree_small" ? treeSprite : d.asset == "daisy_flower" ? daisySprite : beeSprite;
                    float baseSize = d.asset == "bee_hover" ? 16f : 71f;
                    RectTransform rt = NewCentredRect(d.asset + " " + i, parent,
                        d.anchorX * 460f + offset.x, -d.anchorY * 460f + offset.y,
                        baseSize * Mathf.Max(.1f, d.scale), baseSize * Mathf.Max(.1f, d.scale));
                    if (d.asset == "bee_hover") rt.gameObject.AddComponent<BOKSBeeHover>().Build(rt.rect.width, i, count, d.anchorX, d.anchorY);
                    else
                    {
                        Image image = rt.gameObject.AddComponent<Image>();
                        image.sprite = sprite;
                        image.preserveAspect = true;
                        AddDecorationReaction(rt, d);
                    }
                }
            }
        }

        void AddBridge(RectTransform parent, BOKSDecoration d, Vector2 offset, int index)
        {
            RectTransform rt = NewCentredRect("bridge " + index, parent, d.anchorX * 460f + offset.x,
                -d.anchorY * 460f + offset.y, 84f * d.scale, 28f * d.scale);
            BOKSShapeGraphic shape = rt.gameObject.AddComponent<BOKSShapeGraphic>();
            shape.topColor = new Color32(188, 153, 107, 255);
            shape.bottomColor = new Color32(139, 105, 69, 255);
            shape.cornerRadius = 5f;
            shape.raycastTarget = false;
            AddDecorationReaction(rt, d);
        }

        void AddDecorationReaction(RectTransform target, BOKSDecoration decoration)
        {
            BOKSDecorationReaction reaction = target.gameObject.AddComponent<BOKSDecorationReaction>();
            reaction.Configure(decoration.x, decoration.y);
            decorationReactions.Add(reaction);
        }

        void TriggerDecorationReactionsAt(int column, int row)
        {
            foreach (BOKSDecorationReaction reaction in decorationReactions)
                if (reaction != null && reaction.Column == column && reaction.Row == row) reaction.Trigger();
        }

        void OnDestroy()
        {
            if (gameplay != null) gameplay.HeroEnteredCell -= TriggerDecorationReactionsAt;
        }

        static RectTransform NewRect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static RectTransform NewCentredRect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            RectTransform rt = NewRect(name, parent, 0, 0, w, h);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }
    }
}
