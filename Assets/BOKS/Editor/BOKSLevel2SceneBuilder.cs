using System.Collections.Generic;
using BOKS.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BOKS.Editor
{
    public static class BOKSLevel2SceneBuilder
    {
        static readonly Color Panel = C("#ede7d7");
        static readonly Color PanelEdge = C("#d2c9b4");
        static readonly Color Cell = C("#cfeaa5");
        static readonly Color CellEdge = new Color(114f / 255f, 164f / 255f, 72f / 255f, .5f);

        static string ScenePathFor(int levelNumber) => $"Assets/Scenes/Archive/BOKS_Level{levelNumber:D2}.unity";
        const string MainMenuScenePath = "Assets/Scenes/BOKS_MainMenu.unity";
        const string CampaignScenePath = "Assets/Scenes/BOKS_Campaign.unity";
        const string LevelEditorScenePath = "Assets/Scenes/BOKS_LevelEditor.unity";

        [MenuItem("BOKS/Build Campaign 1-10")]
        public static void BuildCampaign() => Build(1, true);

        [MenuItem("BOKS/Build Level 2")]
        public static void BuildLevel2() => Build(2);
        [MenuItem("BOKS/Build Level 3")]
        public static void BuildLevel3() => Build(3);
        [MenuItem("BOKS/Build Level 4")]
        public static void BuildLevel4() => Build(4);
        [MenuItem("BOKS/Build Level 5")]
        public static void BuildLevel5() => Build(5);

        [MenuItem("BOKS/Build All Scenes")]
        public static void BuildAllScenes()
        {
            Build(1, true);
            for (int n = 2; n <= 5; n++) Build(n);
        }

        public static void Build(int levelNumber) => Build(levelNumber, false);

        static void Build(int levelNumber, bool campaign)
        {
            ConfigureTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            BOKSLevelDefinition level = LoadLevelDefinition(levelNumber);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = campaign ? "BOKS_Campaign" : $"BOKS_Level{levelNumber:D2}";

            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            Camera cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = C("#f7f7f1");
            cam.orthographic = true;
            cameraGo.transform.position = new Vector3(0, 0, -10);

            string canvasName = campaign ? "BOKS Campaign UI" : $"BOKS Legacy Level {levelNumber:D2} UI";
            GameObject canvasGo = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasRenderer), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(520, 1000);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            BOKSNotebookGraphic paper = canvasGo.AddComponent<BOKSNotebookGraphic>();
            paper.raycastTarget = false;

            RectTransform portrait = Rect("BOKS Gameplay Board", canvasRect, 0, 0, 520, 1000);
            portrait.anchorMin = portrait.anchorMax = portrait.pivot = new Vector2(.5f, .5f);
            portrait.anchoredPosition = Vector2.zero;

            List<BOKSCommandType> paletteCommands = campaign
                ? new List<BOKSCommandType> { BOKSCommandType.Forward, BOKSCommandType.Left, BOKSCommandType.Right, BOKSCommandType.Function }
                : EnabledCommands(level);

            BuildHeader(portrait);
            BuildBoard(portrait, level, out RectTransform grid, out RectTransform goal, out RectTransform objectLayer, out RectTransform beeLayer,
                out RectTransform hero, out Image heroArt, out RectTransform heroVisual, out Sprite[] facingSprites);
            BuildPalette(portrait, paletteCommands, out Button[] paletteButtons, out GameObject[] paletteGlows);
            BuildProgramBoard(portrait, level, campaign, out GameObject[] commandVisuals, out BOKSShapeGraphic[] enabledWells, out RectTransform[] dropSlots);
            BuildRunButton(portrait, out Button playButton, out RectTransform runRoot, out RectTransform runShell, out BOKSShapeGraphic[] runDots);

            BOKSLevel2Controller controller = portrait.gameObject.AddComponent<BOKSLevel2Controller>();
            controller.Configure(level, hero, heroArt, heroVisual, facingSprites, paletteButtons, playButton, commandVisuals, enabledWells, runDots, runRoot, runShell, paletteGlows);

            BOKSGoalPopVFX goalPopVfx = new GameObject("Goal Pop VFX", typeof(RectTransform)).AddComponent<BOKSGoalPopVFX>();
            RectTransform goalPopRect = (RectTransform)goalPopVfx.transform;
            goalPopRect.SetParent(grid, false);
            goalPopRect.anchorMin = goalPopRect.anchorMax = goalPopRect.pivot = new Vector2(0f, 1f);
            goalPopRect.anchoredPosition = Vector2.zero;
            goalPopRect.sizeDelta = Vector2.zero;
            controller.BindGoalPopVFX(goalPopVfx);

            CanvasGroup goalFade = goal.gameObject.AddComponent<CanvasGroup>();
            goalFade.alpha = 1f;
            goalPopVfx.BindGoalBubble(goalFade);

            for (int i = 0; i < paletteButtons.Length; i++)
            {
                BOKSCommandDragSource paletteDrag = paletteButtons[i].gameObject.AddComponent<BOKSCommandDragSource>();
                paletteDrag.Configure(controller, true, -1, paletteCommands[i]);
            }
            for (int i = 0; i < commandVisuals.Length; i++)
            {
                commandVisuals[i].GetComponent<Image>().raycastTarget = true;
                BOKSCommandDragSource placedDrag = commandVisuals[i].AddComponent<BOKSCommandDragSource>();
                placedDrag.Configure(controller, false, i);
            }
            for (int i = 0; i < dropSlots.Length; i++)
            {
                bool enabled = i < 8 ? level.IsMainSlotEnabled(i) : level.IsFunctionSlotEnabled(i - 8);
                BOKSProgramDropSlot drop = dropSlots[i].gameObject.AddComponent<BOKSProgramDropSlot>();
                drop.Configure(controller, i, enabled);
            }

            if (campaign)
            {
                BOKSCampaignView view = portrait.gameObject.AddComponent<BOKSCampaignView>();
                view.Configure(controller, grid, goal, objectLayer, beeLayer, dropSlots, paletteButtons, paletteGlows,
                    LoadAllCharacterSprites(),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BOKS/RuntimeAssets/tree.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BOKS/RuntimeAssets/daisy.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BOKS/RuntimeAssets/bee.png"));
                BOKSCampaignController campaignController = portrait.gameObject.AddComponent<BOKSCampaignController>();
                campaignController.Configure(view);
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem events = eventSystem.GetComponent<EventSystem>();
            events.pixelDragThreshold = 6;
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            PlayerSettings.defaultScreenWidth = 520;
            PlayerSettings.defaultScreenHeight = 1000;
            string scenePath = campaign ? CampaignScenePath : ScenePathFor(levelNumber);
            EditorSceneManager.SaveScene(scene, scenePath);
            if (campaign)
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(MainMenuScenePath, true),
                    new EditorBuildSettingsScene(CampaignScenePath, true),
                    new EditorBuildSettingsScene(LevelEditorScenePath, true)
                };
            Selection.activeGameObject = canvasGo;
            Debug.Log((campaign ? "BOKS campaign" : $"BOKS Level {levelNumber}") + " static scene built at " + scenePath);
        }

        static BOKSLevelDefinition LoadLevelDefinition(int levelNumber)
        {
            BOKSLevelDefinition campaignLevel = BOKSLevelLoader.CampaignLevel(levelNumber);
            if (campaignLevel != null) return campaignLevel;
            string path = BOKSLevelLoader.LevelJsonPath(levelNumber);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            BOKSLevelDefinition definition = asset != null ? BOKSLevelLoader.FromJson(asset.text) : null;
            if (definition != null)
                return definition;

            Debug.LogWarning("[BOKS] Could not load '" + path + "'; using the built-in Level 2 fallback.");
            return BOKSLevelLoader.CreateLevel2Fallback();
        }

        static void ConfigureTextures()
        {
            foreach (string color in new[] { "red", "yellow", "blu", "green" })
            foreach (string dir in new[] { "up", "right", "down", "left" })
            {
                string path = $"Assets/BOKS/RuntimeAssets/character-{color}-{dir}.png";
                if (System.IO.File.Exists(path)) ConfigureTexture(path);
            }

            foreach (string name in new[] { "turn-left", "turn-right", "forward", "tree", "daisy", "bee", "boks-logo" })
            {
                string path = $"Assets/BOKS/RuntimeAssets/{name}.png";
                if (System.IO.File.Exists(path)) ConfigureTexture(path);
            }
        }

        static List<BOKSCommandType> EnabledCommands(BOKSLevelDefinition level)
        {
            var commands = new List<BOKSCommandType>();
            BOKSCommandType[] order = { BOKSCommandType.Forward, BOKSCommandType.Left, BOKSCommandType.Right, BOKSCommandType.Function };
            foreach (BOKSCommandType command in order)
                if (level.IsCommandEnabled(command)) commands.Add(command);
            return commands;
        }

        static void BuildHeader(RectTransform root)
        {
            RectTransform group = Rect("BOKS Gameplay Header", root, 207, 5, 106, 47.21875f);
            Shape("Shadow", group, 0, 2, 106, 47.21875f, C("#cdc2ab", .82f), 10);
            Shape("Logo Backing", group, 0, 0, 106, 47.21875f, C("#fff9ea", .58f), 10);
            SpriteImage("BOKS Logo (original)", group, 10, 4, 86, 39.21875f, "Assets/BOKS/RuntimeAssets/boks-logo.png", false);
        }

        static void BuildBoard(RectTransform root, BOKSLevelDefinition level, out RectTransform grid, out RectTransform goal,
            out RectTransform objectLayer, out RectTransform beeLayer, out RectTransform hero, out Image heroArt,
            out RectTransform heroVisual, out Sprite[] facingSprites)
        {
            RectTransform wrapper = Rect("BOKS Board - 6 x 6", root, 30, 61.21875f, 460, 460);
            Shape("Board Soft Shadow", wrapper, 0, 8, 460, 460, C("#5f4e34", .14f), 16);
            Shape("Board Lower Edge", wrapper, 0, 2, 460, 460, C("#cdc2ab"), 16);
            Shape("Board Wrapper", wrapper, 0, 0, 460, 460, Panel, 16, 2, PanelEdge);

            grid = Rect("Grid Stage (460 px)", wrapper, 2, 2, 460, 460);
            Shape("Grid Gutter #acd784", grid, 0, 0, 460, 460, C("#acd784"), 16, 1, C("#dfd5bf"));

            RectTransform cells = Rect("Cells", grid, 0, 0, 460, 460);
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                RectTransform cell = Rect($"Cell ({x},{y})", cells, 7 + x * 75, 7 + y * 75, 71, 71);
                Shape("Cell Surface", cell, 0, 0, 71, 71, Cell, 8, 1, CellEdge);
                if (x == level.GoalColumn && y == level.GoalRow) continue;
                if ((x + y) % 2 == 0) AddGrass(cell);
                if ((3 * x + 2 * y) % 7 == 0) AddTinyFlower(cell);
                if ((5 * x + y) % 9 == 0) AddStones(cell);
            }

            foreach (BOKSGridCell obstacle in level.obstacles)
                if (obstacle != null) BuildObstacle(grid, obstacle.x, obstacle.y);

            objectLayer = Rect("Decorations - Object Layer (z8)", grid, 0, 0, 460, 460);
            SpriteImage("Tree small - anchor .745,.893 scale 1", objectLayer, 307.2f, 348.3f, 71, 71, "Assets/BOKS/RuntimeAssets/tree.png", false);
            SpriteImage("Tree large - anchor .257,.263 scale 1.6", objectLayer, 61.42f, 21.012f, 113.6f, 113.6f, "Assets/BOKS/RuntimeAssets/tree.png", false);
            SpriteImage("Daisy - anchor .198,.880 scale .9", objectLayer, 59.13f, 348.568f, 63.9f, 63.9f, "Assets/BOKS/RuntimeAssets/daisy.png", false);
            SpriteImage("Daisy - anchor .710,.357 scale .9", objectLayer, 294.65f, 107.988f, 63.9f, 63.9f, "Assets/BOKS/RuntimeAssets/daisy.png", false);

            goal = BuildGoal(grid, level);
            hero = BuildCharacter(grid, level, out heroArt, out heroVisual, out facingSprites);

            beeLayer = Rect("Bees - Overlay Layer (z24)", grid, 0, 0, 460, 460);
            AddBee(beeLayer, "Bee 1", .284633f, .392708f, 16, -20);
            AddBee(beeLayer, "Bee 2", .348539f, .567444f, 15, 24);
            AddBee(beeLayer, "Bee 3", .468143f, .277431f, 14, -34);
        }

        static RectTransform BuildGoal(RectTransform grid, BOKSLevelDefinition level)
        {
            // Source offset-centre: 6 + x*75 + 35.5, one pixel above-left of CSS centre.
            float goalCentreX = 6 + level.GoalColumn * 75f + 35.5f;
            float goalCentreY = 6 + level.GoalRow * 75f + 35.5f;
            RectTransform goal = Rect($"Goal Bubble - Cell ({level.GoalColumn},{level.GoalRow}), source offset (-1,-1)", grid, goalCentreX - 54, goalCentreY - 54, 108, 108);
            Shape("Pale Glow Outer", goal, 0, 0, 108, 108, C("#d8f5ff", .035f), 54, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Pale Glow Middle", goal, 14, 14, 80, 80, C("#d8f5ff", .055f), 40, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Warm Glow Core", goal, 30, 30, 48, 48, C("#fff8c1", .08f), 24, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            RectTransform shell = Rect("Bubble Shell", goal, 26, 23, 56, 62);
            Shape("Bubble Fill", shell, 0, 0, 56, 62, C("#e8faff", .16f), 28, 2.2f, C("#e8faff", .88f), BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Inner Rim", shell, 6, 6, 48, 52, Color.clear, 24, 1.2f, C("#a4e2f8", .28f), BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Upper Gloss", shell, 12, 8, 12, 20, C("#ffffff", .58f), 6, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse).localEulerAngles = new Vector3(0, 0, 23);
            Shape("Lower Reflection", shell, 33, 35, 18, 12, C("#a4e2f8", .22f), 6, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse).localEulerAngles = new Vector3(0, 0, -22);
            Vector2[] petals = { new Vector2(28, 21), new Vector2(37, 29), new Vector2(34, 40), new Vector2(22, 40), new Vector2(19, 29) };
            string[] colours = { "#ff7878", "#ff5c5c", "#ff4a4a", "#ff6e6e", "#ff3a3a" };
            for (int i = 0; i < petals.Length; i++)
                Shape("Coral Petal " + (i + 1), shell, petals[i].x - 4.5f, petals[i].y - 4.5f, 9, 9, C(colours[i], .96f), 5, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Flower Centre", shell, 24.2f, 26.2f, 7.6f, 7.6f, C("#f3c341"), 4, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            return goal;
        }

        static RectTransform BuildCharacter(RectTransform grid, BOKSLevelDefinition level, out Image heroArt, out RectTransform heroVisual, out Sprite[] facingSprites)
        {
            // Cell centre minus the 1px source offset. The root is pivoted here and never moves on a turn.
            float centreX = 7 + level.StartColumn * 75f + 34.5f;
            float centreY = -(7 + level.StartRow * 75f + 34.5f);
            string friendly = FriendlyCharacterName(level.characterId);
            string facingLabel = DirectionName(level.Direction);

            RectTransform hero = CentredRect($"BOKS {friendly} - Start ({level.StartColumn},{level.StartRow}), Facing {facingLabel}", grid, centreX, centreY, 71, 71);
            hero.anchorMin = hero.anchorMax = new Vector2(0, 1);
            hero.gameObject.AddComponent<RectMask2D>();

            // Rotating child pivoted at the cell centre (mirrors the web's transform-origin: 50% 50%).
            heroVisual = CentredRect("BOKS Visual", hero, 0, 0, 71, 71);

            facingSprites = LoadFacingSprites(level.characterId);
            Sprite startSprite = facingSprites[(int)level.Direction] != null
                ? facingSprites[(int)level.Direction]
                : facingSprites[(int)BOKSDirection.Right];

            // Directional artwork inside the rotating child, carrying the source fit offset (scale 1.08, y+4).
            RectTransform art = CentredRect("BOKS Art", heroVisual, 0, -4, 76.68f, 76.68f);
            heroArt = art.gameObject.AddComponent<Image>();
            heroArt.sprite = startSprite;
            heroArt.preserveAspect = false;
            heroArt.raycastTarget = false;
            return hero;
        }

        static string FriendlyCharacterName(string characterId)
        {
            string color = CharacterColor(characterId);
            return char.ToUpperInvariant(color[0]) + color.Substring(1);
        }

        static string CharacterColor(string characterId)
        {
            switch (characterId)
            {
                case "boks_yellow": return "yellow";
                case "boks_blu": return "blu";
                case "boks_green": return "green";
                default: return "red";
            }
        }

        static string DirectionName(BOKSDirection direction)
        {
            switch (direction)
            {
                case BOKSDirection.Up: return "Up";
                case BOKSDirection.Left: return "Left";
                case BOKSDirection.Down: return "Down";
                default: return "Right";
            }
        }

        static Sprite[] LoadFacingSprites(string characterId)
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSpritePath(characterId, BOKSDirection.Right)),
                AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSpritePath(characterId, BOKSDirection.Up)),
                AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSpritePath(characterId, BOKSDirection.Left)),
                AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSpritePath(characterId, BOKSDirection.Down)),
            };
        }

        static Sprite[] LoadAllCharacterSprites()
        {
            var result = new List<Sprite>(16);
            foreach (string id in new[] { "boks_red", "boks_yellow", "boks_blu", "boks_green" })
                result.AddRange(LoadFacingSprites(id));
            return result.ToArray();
        }

        static string CharacterSpritePath(string characterId, BOKSDirection direction)
        {
            string dir = direction switch
            {
                BOKSDirection.Up => "up",
                BOKSDirection.Left => "left",
                BOKSDirection.Down => "down",
                _ => "right"
            };
            return $"Assets/BOKS/RuntimeAssets/character-{CharacterColor(characterId)}-{dir}.png";
        }

        static void BuildObstacle(RectTransform grid, int x, int y)
        {
            RectTransform cell = Rect($"Obstacle ({x},{y})", grid, 7 + x * 75, 7 + y * 75, 71, 71);
            Shape("Obstacle Fill", cell, 0, 0, 71, 71, C("#cdb894"), 8, 1, C("#856843", .74f), BOKSShapeGraphic.ShapeKind.RoundedRectangle, C("#b59c74"));
            Shape("Obstacle Seam A", cell, 4, 26, 63, 4, C("#7d5f39", .4f), 2);
            Shape("Obstacle Seam B", cell, 4, 42, 63, 4, C("#7d5f39", .4f), 2);
        }

        static void BuildPalette(RectTransform root, List<BOKSCommandType> commands, out Button[] paletteButtons, out GameObject[] paletteGlows)
        {
            RectTransform panel = Rect("BOKS Command Palette", root, 30, 529.21875f, 460, 72);
            AddPanelChrome(panel, 460, 72, 7);

            const float stride = 105.75f;
            const float blockSize = 52f;
            const float centre = 232f;
            int count = Mathf.Max(1, commands.Count);
            float firstCentre = centre - (count - 1) * stride * .5f;

            paletteButtons = new Button[count];
            var glows = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                float cx = firstCentre + i * stride;
                float x = cx - blockSize * .5f;
                float y = 10f;

                RectTransform glowOuter = Shape("Guide Glow Outer", panel, x - 13.5f, y - 11.5f, 75, 75, C("#ffcd2e", .055f), 38, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
                RectTransform glowMiddle = Shape("Guide Glow Middle", panel, x - 4.5f, y - 2.5f, 57, 57, C("#ffe768", .11f), 29, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
                RectTransform glowCore = Shape("Guide Glow Core", panel, x + 5.5f, y + 7.5f, 37, 37, C("#fffbd6", .19f), 19, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
                glows.Add(glowOuter.gameObject);
                glows.Add(glowMiddle.gameObject);
                glows.Add(glowCore.gameObject);

                string icon = CommandIconPath(commands[i]);
                string label = CommandLabel(commands[i]);
                RectTransform block = SpriteImage(label + " Block", panel, x, y, blockSize, blockSize, icon, true);
                Image blockImage = block.GetComponent<Image>();
                blockImage.raycastTarget = true;
                Button button = block.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = blockImage;
                paletteButtons[i] = button;
            }
            paletteGlows = glows.ToArray();
        }

        static string CommandIconPath(BOKSCommandType command)
        {
            switch (command)
            {
                case BOKSCommandType.Left: return "Assets/BOKS/RuntimeAssets/turn-left.png";
                case BOKSCommandType.Right: return "Assets/BOKS/RuntimeAssets/turn-right.png";
                case BOKSCommandType.Function: return "Assets/BOKS/RuntimeAssets/function.png";
                default: return "Assets/BOKS/RuntimeAssets/forward.png";
            }
        }

        static string CommandLabel(BOKSCommandType command)
        {
            switch (command)
            {
                case BOKSCommandType.Left: return "Left";
                case BOKSCommandType.Right: return "Right";
                case BOKSCommandType.Function: return "Function";
                default: return "Forward";
            }
        }

        static void BuildProgramBoard(RectTransform root, BOKSLevelDefinition level, bool fullLayout, out GameObject[] commandVisuals,
            out BOKSShapeGraphic[] enabledWells, out RectTransform[] dropSlots)
        {
            int enabledMainCount = level.EnabledMainSlotCount;
            int enabledTotalCount = enabledMainCount;
            for (int i = 0; i < 4; i++) if (level.IsFunctionSlotEnabled(i)) enabledTotalCount++;
            commandVisuals = new GameObject[fullLayout ? 12 : enabledTotalCount];
            enabledWells = new BOKSShapeGraphic[fullLayout ? 12 : enabledTotalCount];
            dropSlots = new RectTransform[12];
            RectTransform board = Rect("BOKS Program Area", root, 30, 607.21875f, 460, 218);
            AddPanelChrome(board, 460, 218, 8);
            RectTransform content = Rect("Tracks and Slots", board, 2, 2, 456, 214);

            Color track = C("#d9c7a5", .78f);
            RectTransform mainPath = Rect("Main Sequence Connector - Behind Slots", content, 0, 0, 456, 214);
            BOKSSequencePathGraphic sequencePath = mainPath.gameObject.AddComponent<BOKSSequencePathGraphic>();
            sequencePath.pathColor = track;
            sequencePath.thickness = 3f;
            sequencePath.raycastTarget = false;
            Shape("Main Arrow", content, 8, 27, 8, 18, C("#d7b98a"), 0, 0, Color.clear, BOKSShapeGraphic.ShapeKind.TriangleRight);
            for (int x = 16; x < 440; x += 14)
                Shape("Dashed Separator", content, x, 142, 9, 2.5f, C("#4db6ff", .82f), 1.2f);
            Shape("Function Track", content, 66, 179, 333, 3, C("#78c4ee", .55f), 2);
            Shape("Function Arrow", content, 8, 171, 8, 18, C("#78c4ee", .78f), 0, 0, Color.clear, BOKSShapeGraphic.ShapeKind.TriangleRight);

            float[] xs = { 23, 128.75f, 234.5f, 340.25f };
            int enabledIndex = 0;
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 4; col++)
            {
                bool enabled = row < 2 ? level.IsMainSlotEnabled(row * 4 + col) : level.IsFunctionSlotEnabled(col);
                string label = row < 2 ? $"Main Slot {row * 4 + col}" : $"Function Slot {col}";
                RectTransform slot = Rect(label + (enabled ? " - Enabled" : " - Locked"), content, xs[col], 10 + row * 72, 92.75f, 50);
                dropSlots[row * 4 + col] = slot;
                // The web track is behind the cards. Locked cards are faded as a card layer,
                // but first occlude the connector with the panel colour so it cannot show through.
                Shape("Connector Occlusion Mask", slot, 0, 0, 92.75f, 50, Panel, 10);
                RectTransform visuals = Rect("Slot Card Visuals", slot, 0, 0, 92.75f, 50);
                if (!enabled) visuals.gameObject.AddComponent<CanvasGroup>().alpha = .34f;
                Color top = row == 2 ? C("#dcc6a2") : C("#d3b487");
                Color bottom = row == 2 ? C("#d3bb94") : C("#caa678");
                Color edge = row == 2 ? C("#c8ac82") : C("#bc996b");
                RectTransform well = Shape("Well", visuals, 0, 0, 92.75f, 50, top, 10, 1, edge, BOKSShapeGraphic.ShapeKind.RoundedRectangle, bottom);
                well.GetComponent<BOKSShapeGraphic>().raycastTarget = true;
                Shape("Inset", visuals, 25.375f, 4, 42, 42, C("#cda976", .24f), 9, 1, C("#9e815d", .16f));
                if (enabled || fullLayout)
                {
                    int storageIndex = fullLayout ? row * 4 + col : enabledIndex;
                    enabledWells[storageIndex] = well.GetComponent<BOKSShapeGraphic>();
                    string placedName = fullLayout ? "Placed Command " : "Placed Forward Command ";
                    RectTransform placed = SpriteImage(placedName + (storageIndex + 1), visuals, 23.875f, 2.5f, 45, 45, "Assets/BOKS/RuntimeAssets/forward.png", true);
                    placed.gameObject.SetActive(false);
                    commandVisuals[storageIndex] = placed.gameObject;
                    enabledIndex++;
                }
            }
        }

        static void BuildRunButton(RectTransform root, out Button playButton, out RectTransform runRoot,
            out RectTransform runShell, out BOKSShapeGraphic[] runDots)
        {
            RectTransform run = Rect("BOKS Run Button", root, 178, 831.21875f, 164, 56);
            Shape("Run Shadow", run, 0, 10, 164, 56, C("#5f4e34", .18f), 14);
            Shape("Run Lower Edge", run, 0, 4, 164, 56, C("#7aa05b", .72f), 14);
            Shape("Run Outer", run, 0, 0, 164, 56, Panel, 14, 7, PanelEdge);
            RectTransform shell = Shape("Run Shell", run, 7, 7, 150, 42, C("#efe9da"), 10);
            runDots = new BOKSShapeGraphic[8];
            for (int i = 0; i < 8; i++)
                runDots[i] = Shape("Unlit Step " + (i + 1), run, 14 + i * 18, 23, 10, 10, C("#b2b196"), 5, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse).GetComponent<BOKSShapeGraphic>();

            Image hitArea = run.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            playButton = run.gameObject.AddComponent<Button>();
            playButton.transition = Selectable.Transition.None;
            playButton.targetGraphic = hitArea;
            runRoot = run;
            runShell = shell;
        }

        static void AddPanelChrome(RectTransform panel, float w, float h, float shadowY)
        {
            Shape("Soft Shadow", panel, 0, shadowY, w, h, C("#5f4e34", .12f), 16);
            Shape("Lower Edge", panel, 0, 2, w, h, C("#cdc2ab"), 16);
            Shape(panel.name + " Surface", panel, 0, 0, w, h, Panel, 16, 2, PanelEdge);
        }

        static void AddGrass(RectTransform cell)
        {
            RectTransform g = Rect("Automatic Grass Mark", cell, 10, 54, 18, 10);
            Color green = C("#82c56e", .7f);
            Shape("Blade 1", g, 1, 3, 6, 5, green, 3, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Blade 2", g, 6, 0, 6, 6, green, 3, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Blade 3", g, 11, 4, 6, 5, green, 3, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
        }

        static void AddTinyFlower(RectTransform cell)
        {
            RectTransform f = Rect("Automatic Flower Mark", cell, 48, 10, 15, 15);
            Vector2[] p = { new Vector2(7.5f, 2), new Vector2(12, 6), new Vector2(10, 11), new Vector2(5, 11), new Vector2(3, 6) };
            foreach (Vector2 v in p) Shape("Petal", f, v.x - 2.5f, v.y - 2.5f, 5, 5, C("#fff8e9", .95f), 3, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Centre", f, 5.5f, 4.5f, 4, 4, C("#f1d34e"), 2, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
        }

        static void AddStones(RectTransform cell)
        {
            RectTransform s = Rect("Automatic Stone Mark", cell, 9, 47, 30, 15);
            Color stone = C("#a9bf8c", .55f);
            Shape("Stone 1", s, 0, 5, 12, 7, stone, 5, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
            Shape("Stone 2", s, 14, 1, 12, 7, stone, 5, 0, Color.clear, BOKSShapeGraphic.ShapeKind.Ellipse);
        }

        static void AddBee(RectTransform parent, string name, float nx, float ny, float size, float rotation)
        {
            RectTransform bee = SpriteImage(name, parent, nx * 460 - size * .5f, ny * 460 - size * .5f, size, size, "Assets/BOKS/RuntimeAssets/bee.png", false);
            bee.localEulerAngles = new Vector3(0, 0, rotation);
        }

        static RectTransform SpriteImage(string name, RectTransform parent, float x, float y, float w, float h, string path, bool preserve)
        {
            RectTransform rt = Rect(name, parent, x, y, w, h);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.preserveAspect = preserve;
            image.raycastTarget = false;
            return rt;
        }

        static RectTransform Shape(string name, RectTransform parent, float x, float y, float w, float h, Color fill,
            float radius, float border = 0, Color? edge = null,
            BOKSShapeGraphic.ShapeKind kind = BOKSShapeGraphic.ShapeKind.RoundedRectangle, Color? bottom = null)
        {
            RectTransform rt = Rect(name, parent, x, y, w, h);
            BOKSShapeGraphic g = rt.gameObject.AddComponent<BOKSShapeGraphic>();
            g.shape = kind;
            g.topColor = fill;
            g.bottomColor = bottom ?? fill;
            g.cornerRadius = radius;
            g.borderWidth = border;
            g.borderColor = edge ?? Color.clear;
            g.raycastTarget = false;
            return rt;
        }

        static RectTransform Rect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static RectTransform CentredRect(string name, RectTransform parent, float centreX, float centreY, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(centreX, centreY);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Color C(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            c.a *= alpha;
            return c;
        }

        static void ConfigureTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
    }
}
