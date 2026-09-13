using UnityEngine;
using UnityEngine.UI;

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

        readonly BOKSCommandType[] paletteOrder = {
            BOKSCommandType.Forward, BOKSCommandType.Left, BOKSCommandType.Right, BOKSCommandType.Function
        };

        public BOKSLevel2Controller Gameplay => gameplay;

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
        }

        public void ApplyLevel(BOKSLevelDefinition level)
        {
            ClearDynamicLayer(objectLayer);
            ClearDynamicLayer(overlayLayer);
            ClearOldObstacles();

            goal.anchoredPosition = new Vector2(6 + level.GoalColumn * 75f + 35.5f - 54f,
                -(6 + level.GoalRow * 75f + 35.5f - 54f));

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
            bool showGlow = level.glowEnabled;
            foreach (GameObject glow in paletteGlowLayers) if (glow != null) glow.SetActive(showGlow);

            if (level.obstacles != null)
                foreach (BOKSGridCell cell in level.obstacles)
                    if (cell != null) AddObstacle(cell.x, cell.y);
            if (level.decorations != null)
                foreach (BOKSDecoration decoration in level.decorations)
                    if (decoration != null) AddDecoration(decoration);

            gameplay.LoadLevel(level, SpritesFor(level.characterId));
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
            for (int i = layer.childCount - 1; i >= 0; i--) Destroy(layer.GetChild(i).gameObject);
        }

        void ClearOldObstacles()
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
                if (grid.GetChild(i).name.StartsWith("Obstacle (")) Destroy(grid.GetChild(i).gameObject);
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
                    Image image = rt.gameObject.AddComponent<Image>();
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
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
