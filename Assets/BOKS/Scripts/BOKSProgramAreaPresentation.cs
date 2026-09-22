using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Visual-only Function sequence treatment; slot objects and program state remain untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class BOKSProgramAreaPresentation : MonoBehaviour
    {
        static readonly Color FunctionFill = new Color32(222, 241, 255, 255);
        static readonly Color FunctionLine = new Color32(91, 183, 239, 210);

        const float PanelX = 14f;
        const float PanelY = 150f;
        const float PanelWidth = 428f;
        const float PanelHeight = 60f;

        public static void Ensure(RectTransform gameplayRoot)
        {
            if (gameplayRoot == null) return;
            RectTransform programArea = FindDescendant(gameplayRoot, "BOKS Program Area");
            if (programArea == null || programArea.GetComponent<BOKSProgramAreaPresentation>() != null) return;
            programArea.gameObject.AddComponent<BOKSProgramAreaPresentation>();
        }

        void Awake()
        {
            RectTransform content = FindDescendant(transform as RectTransform, "Tracks and Slots");
            if (content == null || content.Find("Function Panel - Presentation") != null) return;

            DisableLegacyFunctionVisuals(content);
            RectTransform panel = AddRect("Function Panel - Presentation", content, PanelX, PanelY, PanelWidth, PanelHeight);
            panel.SetSiblingIndex(0);
            AddPanelImage(panel);
            AddFunctionConnector(panel);
            AddFunctionStartMarker(panel);
            AddBlueDashedSeparator(content);
        }

        static void DisableLegacyFunctionVisuals(RectTransform content)
        {
            foreach (Transform child in content)
            {
                if (child.name == "Function Track" || child.name == "Function Arrow" || child.name == "Dashed Separator")
                    child.gameObject.SetActive(false);
            }
        }

        static void AddPanelImage(RectTransform panel)
        {
            Image image = panel.gameObject.AddComponent<Image>();
            image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            image.type = Image.Type.Sliced;
            image.color = FunctionFill;
            image.raycastTarget = false;

            // Keep the existing presentation child as a transparent soft layer; the Function zone
            // is distinguished by its pale-blue fill, dashed divider, connector and start marker,
            // not by a hard rectangular outline.
            RectTransform outline = AddRect("Function Panel Blue Outline", panel, 0f, 0f, PanelWidth, PanelHeight);
            BOKSShapeGraphic border = outline.gameObject.AddComponent<BOKSShapeGraphic>();
            border.topColor = new Color(FunctionFill.r, FunctionFill.g, FunctionFill.b, 0f);
            border.bottomColor = new Color(FunctionFill.r, FunctionFill.g, FunctionFill.b, 0f);
            border.borderColor = Color.clear;
            border.borderWidth = 0f;
            border.cornerRadius = 12f;
            border.raycastTarget = false;
        }

        static void AddFunctionConnector(RectTransform panel)
        {
            // The original four Function slots remain at x=23, 128.75, 234.5, 340.25 in content.
            // This line is placed behind their shared vertical centre inside the new sub-panel.
            AddImage("Function Blue Connector", panel, 52f, 28.5f, 333f, 3f, FunctionLine);
        }

        static void AddFunctionStartMarker(RectTransform panel)
        {
            RectTransform marker = AddRect("Function Blue Start Marker", panel, 8f, 21f, 9f, 18f);
            BOKSShapeGraphic arrow = marker.gameObject.AddComponent<BOKSShapeGraphic>();
            arrow.shape = BOKSShapeGraphic.ShapeKind.TriangleRight;
            arrow.topColor = FunctionLine;
            arrow.bottomColor = FunctionLine;
            arrow.raycastTarget = false;
        }

        static void AddBlueDashedSeparator(RectTransform content)
        {
            RectTransform separator = AddRect("Function Blue Dashed Separator", content, 16f, 142f, 424f, 3f);
            separator.SetSiblingIndex(1);
            for (float x = 0f; x < 424f; x += 14f)
                AddImage("Blue Dash", separator, x, 0f, 9f, 2.5f, FunctionLine);
        }

        static Image AddImage(string name, RectTransform parent, float x, float y, float width, float height, Color color)
        {
            RectTransform rect = AddRect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform AddRect(string name, RectTransform parent, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static RectTransform FindDescendant(RectTransform root, string name)
        {
            if (root == null) return null;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }
    }
}
