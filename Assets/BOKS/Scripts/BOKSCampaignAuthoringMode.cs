#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Editor-only authoring overlay for the live campaign UI. It is excluded from player builds.
    /// The board, palette, slots and Run button remain the real campaign controls.
    /// </summary>
    [ExecuteAlways]
    public sealed class BOKSCampaignAuthoringMode : MonoBehaviour
    {
        const string CampaignPath = "Assets/BOKS/Resources/BOKS/Data/campaign-levels.json";
        const string TestDraftKey = "BOKS.LevelEditor.TestDraft";
        const string TestLevelNumberKey = "BOKS.LevelEditor.TestLevelNumber";
        const string TestNewLevelKey = "BOKS.LevelEditor.TestNewLevel";
        const string TestProgramKey = "BOKS.LevelEditor.TestProgram";
        enum Tool { None, Boks, Goal, Brick }

        BOKSCampaignView view;
        BOKSLevelDefinition draft;
        Tool tool;
        int levelNumber = 2;
        bool newLevel;
        string message;
        BOKSCommandType[] testProgram;
        int testColumn;
        int testRow;
        BOKSDirection testFacing;
        Vector2 sidebarScroll;
        readonly Dictionary<string, Texture2D> uiTextures = new Dictionary<string, Texture2D>();

        void Awake() => Enter();

        public void Enter()
        {
            view = GetComponent<BOKSCampaignView>();
            ConfigureDesktopLayout();
            BOKSCampaignController campaign = GetComponent<BOKSCampaignController>();
            if (campaign != null) campaign.AuthoringMode = true;
            string pendingTestDraft = SessionState.GetString(TestDraftKey, string.Empty);
            if (!string.IsNullOrEmpty(pendingTestDraft))
            {
                draft = BOKSLevelLoader.FromJson(pendingTestDraft);
                levelNumber = SessionState.GetInt(TestLevelNumberKey, Mathf.Max(1, view.Gameplay.LevelNumber));
                newLevel = SessionState.GetBool(TestNewLevelKey, false);
                BOKSCommandType[] pendingProgram = DeserializeProgram(SessionState.GetString(TestProgramKey, string.Empty));
                if (draft != null)
                {
                    draft.levelNumber = draft.number = levelNumber;
                    EnsureDraft();
                    ApplyDraft();
                    if (Application.isPlaying)
                        StartCoroutine(ActivateTestDraftNextFrame(pendingProgram));
                    else
                    {
                        view.Gameplay.RestoreEditorTestState(draft.StartColumn, draft.StartRow, draft.Direction, pendingProgram);
                        LogDraftApplied();
                    }
                    message = Application.isPlaying
                        ? (newLevel ? "Testing unsaved level — use command blocks and Play" : "Testing Level " + levelNumber + " — use command blocks and Play")
                        : (newLevel ? "New Level — unsaved" : "Editing Level " + levelNumber);
                }
                if (!Application.isPlaying)
                {
                    SessionState.EraseString(TestDraftKey);
                    SessionState.EraseInt(TestLevelNumberKey);
                    SessionState.EraseBool(TestNewLevelKey);
                    SessionState.EraseString(TestProgramKey);
                }
            }
            else
            {
                levelNumber = Mathf.Max(1, view.Gameplay.LevelNumber);
                LoadLevel(levelNumber);
            }
            view.Gameplay.PlayPressed -= RememberTestProgram;
            view.Gameplay.PlayPressed += RememberTestProgram;
        }

        // This scene deliberately uses a desktop composition: the campaign root is a live preview
        // on the right, independent of the regular portrait game's presentation.
        void ConfigureDesktopLayout()
        {
            CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null) scaler.referenceResolution = new Vector2(1280, 760);
            RectTransform liveGame = transform as RectTransform;
            if (liveGame == null) return;
            liveGame.anchorMin = liveGame.anchorMax = new Vector2(.5f, .5f);
            liveGame.pivot = new Vector2(.5f, .5f);
            // Desktop columns at 1600x900: 390px sidebar | 44px gutter | 395px live group.
            // The live root is a sibling of the sidebar presentation, never an overlay target.
            liveGame.anchoredPosition = new Vector2(175f, 0f);
            liveGame.localScale = Vector3.one * .76f;
        }

        void OnDestroy()
        {
            if (view != null) view.Gameplay.PlayPressed -= RememberTestProgram;
            BOKSCampaignController campaign = GetComponent<BOKSCampaignController>();
            if (campaign != null) campaign.AuthoringMode = false;
        }

        void LoadLevel(int number)
        {
            BOKSLevelLoader.ClearCampaignCache();
            BOKSLevelDefinition source = BOKSLevelLoader.CampaignLevel(number);
            if (source == null) { message = "Level not found."; return; }
            draft = BOKSLevelLoader.FromJson(JsonUtility.ToJson(source));
            draft.levelNumber = draft.number = number;
            EnsureDraft();
            ApplyDraft();
        }

        void EnsureDraft()
        {
            if (draft.grid == null) draft.grid = new BOKSGrid();
            draft.grid.columns = draft.grid.rows = 6;
            if (draft.obstacles == null) draft.obstacles = Array.Empty<BOKSGridCell>();
            if (draft.enabledBlocks == null) draft.enabledBlocks = new BOKSEnabledBlocks();
            Resize(ref draft.mainSlotEnabled, 8); Resize(ref draft.fnSlotEnabled, 4);
        }

        void ApplyDraft()
        {
            draft.startDirection = draft.startOri;
            draft.NormalizeSourceFields();
            view.ApplyLevel(draft);
            BOKSGoalBubbleIdle idle = FindAnyObjectByType<BOKSGoalBubbleIdle>();
            if (idle != null) idle.enabled = false; // editor placement must remain cell-anchored
        }

        void OnGUI()
        {
            if (draft == null) return;
            // 390 + 44 + ~395px: centered desktop composition at 1600x900.
            const float sidebarWidth = 390f;
            const float gameWidth = 395f;
            const float gutter = 44f;
            float shellWidth = sidebarWidth + gutter + gameWidth;
            Rect sidebar = new Rect(Mathf.Max(24f, (Screen.width - shellWidth) * .5f), 30, sidebarWidth, Screen.height - 60);
            DrawCard(sidebar, new Color32(247, 244, 232, 252), new Color32(210, 201, 180, 255));

            const float innerPadding = 22f;
            const float headerHeight = 52f;
            const float footerHeight = 42f;
            float footerY = Screen.height - 92f;

            // Header and footer stay fixed; only the controls between them can scroll
            // when an unusually large level list needs more vertical room.
            GUILayout.BeginArea(new Rect(sidebar.x + innerPadding, sidebar.y + 18, sidebar.width - innerPadding * 2, headerHeight));
            GUILayout.Label("BOKS Level Editor", TitleStyle());
            GUILayout.Label("Build and test a campaign board", SubtitleStyle());
            GUILayout.EndArea();

            Rect contentArea = new Rect(
                sidebar.x + innerPadding,
                sidebar.y + 18 + headerHeight + 4,
                sidebar.width - innerPadding * 2,
                footerY - (sidebar.y + 18 + headerHeight + 4) - 8);
            GUILayout.BeginArea(contentArea);
            sidebarScroll = GUILayout.BeginScrollView(sidebarScroll, false, false);
            Section("Levels");
            List<int> existingLevels = ExistingLevelNumbers();
            for (int rowStart = 0; rowStart < existingLevels.Count; rowStart += 8)
            {
                GUILayout.BeginHorizontal();
                for (int i = rowStart; i < Mathf.Min(existingLevels.Count, rowStart + 8); i++)
                {
                    int listedLevel = existingLevels[i];
                    bool selected = !newLevel && levelNumber == listedLevel;
                    if (GUILayout.Toggle(selected, listedLevel.ToString(), LevelStyle(selected), GUILayout.Width(38), GUILayout.Height(27)) && !selected)
                    {
                        newLevel = false;
                        levelNumber = listedLevel;
                        LoadLevel(listedLevel);
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ New Level", ButtonStyle(new Color32(0, 170, 80, 255), Color.white, true), GUILayout.Height(32))) CreateNewLevel();
            Section("Board tools");
            GUILayout.BeginHorizontal(); ToolButton("BOKS", Tool.Boks); ToolButton("Goal", Tool.Goal); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Goal", ButtonStyle(new Color32(245, 28, 36, 255), Color.white), GUILayout.Height(28))) { draft.goal = null; tool = Tool.None; ApplyDraft(); }
            ToolButton("Obstacle", Tool.Brick);
            GUILayout.EndHorizontal();
            Section("Start direction");
            GUILayout.BeginHorizontal();
            DirectionButton("Right", "right"); DirectionButton("Up", "up");
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
            DirectionButton("Down", "down"); DirectionButton("Left", "left");
            GUILayout.EndHorizontal();
            Section("Available blocks");
            GUILayout.BeginHorizontal();
            CommandToggle(ref draft.enabledBlocks.forward, "Forward", new Color(.56f,.78f,.34f), BOKSCommandType.Forward);
            CommandToggle(ref draft.enabledBlocks.left, "Left", new Color(.91f,.71f,.43f), BOKSCommandType.Left);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            CommandToggle(ref draft.enabledBlocks.right, "Right", new Color(.91f,.54f,.43f), BOKSCommandType.Right);
            CommandToggle(ref draft.enabledBlocks.function, "Function", new Color(.72f,.63f,.82f), BOKSCommandType.Function);
            GUILayout.EndHorizontal();
            Section("Program slots");
            GUILayout.Label("Main", SmallLabelStyle()); SlotButtons(draft.mainSlotEnabled, 0);
            GUILayout.Label("Function", SmallLabelStyle()); SlotButtons(draft.fnSlotEnabled, 8);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, MessageStyle());
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Fixed cream footer: these actions remain visible at desktop height without scrolling.
            GUILayout.BeginArea(new Rect(sidebar.x + innerPadding, footerY, sidebar.width - innerPadding * 2, footerHeight));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", ButtonStyle(new Color32(0, 170, 80, 255), Color.white, true), GUILayout.Height(34))) Save();
            bool canDelete = !newLevel && levelNumber > 10 && existingLevels.Contains(levelNumber);
            bool priorEnabled = GUI.enabled;
            GUI.enabled = priorEnabled && canDelete;
            if (GUILayout.Button("Delete Level", ButtonStyle(new Color32(196, 55, 48, 255), Color.white, true), GUILayout.Height(34))) DeleteSelectedLevel();
            GUI.enabled = priorEnabled;
            if (GUILayout.Button("Test Level", ButtonStyle(new Color32(253, 181, 21, 255), new Color32(68, 55, 24, 255), true), GUILayout.Height(34))) TestLevel();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !sidebar.Contains(e.mousePosition))
                TryEditBoard(e);
        }

        void Section(string label) { GUILayout.Space(7); GUILayout.Label(label, SectionStyle()); GUILayout.Space(3); }
        bool ToggleButton(bool value, string label, Color accent)
        {
            Color fill = value ? accent : new Color32(241, 235, 216, 255);
            Color text = value ? Color.white : new Color32(91, 77, 55, 255);
            return GUILayout.Toggle(value, label, ButtonStyle(fill, text, value), GUILayout.Height(28), GUILayout.ExpandWidth(true));
        }
        void ToolButton(string label, Tool value)
        {
            bool selected = tool == value;
            if (ToggleButton(selected, label, new Color32(0, 170, 80, 255)) && !selected) tool = value;
        }
        void DirectionButton(string label, string value)
        {
            bool selected = draft.startOri == value;
            if (ToggleButton(selected, label, new Color32(117, 182, 82, 255)) && !selected)
            {
                draft.startOri = value;
                ApplyDraft();
            }
        }
        void CommandToggle(ref bool value, string label, Color accent, BOKSCommandType command)
        {
            bool next = ToggleButton(value, label, accent);
            if (next == value) return;
            value = next;
            draft.NormalizeSourceFields();
            view.SetEditorCommandEnabled(command, next);
        }

        void SlotButtons(bool[] slots, int runtimeOffset)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < slots.Length; i++)
            {
                bool next = GUILayout.Toggle(slots[i], (i + 1).ToString(), SlotStyle(slots[i]), GUILayout.Height(25), GUILayout.ExpandWidth(true));
                if (next == slots[i]) continue;
                slots[i] = next;
                draft.NormalizeSourceFields();
                view.SetEditorSlotEnabled(runtimeOffset + i, next);
            }
            GUILayout.EndHorizontal();
        }

        GUIStyle TitleStyle() => LabelStyle(20, new Color32(62, 82, 44, 255), FontStyle.Bold);
        GUIStyle SubtitleStyle() => LabelStyle(11, new Color32(125, 111, 82, 255), FontStyle.Normal);
        GUIStyle SectionStyle() => LabelStyle(12, new Color32(82, 103, 53, 255), FontStyle.Bold);
        GUIStyle SmallLabelStyle() => LabelStyle(11, new Color32(105, 91, 62, 255), FontStyle.Bold);
        GUIStyle MessageStyle() => LabelStyle(10, new Color32(105, 91, 62, 255), FontStyle.Normal, true);

        GUIStyle LabelStyle(int fontSize, Color color, FontStyle fontStyle, bool wordWrap = false)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = fontStyle, normal = { textColor = color }, wordWrap = wordWrap };
        }

        GUIStyle LevelStyle(bool selected) => ButtonStyle(selected ? new Color32(93, 170, 55, 255) : new Color32(235, 227, 203, 255), selected ? Color.white : new Color32(86, 75, 51, 255), true);
        GUIStyle SlotStyle(bool selected) => ButtonStyle(selected ? new Color32(117, 182, 82, 255) : new Color32(233, 226, 207, 255), selected ? Color.white : new Color32(118, 104, 74, 255), true);

        GUIStyle ButtonStyle(Color fill, Color text, bool bold = false)
        {
            Texture2D texture = RoundedTexture(fill);
            return new GUIStyle(GUI.skin.button) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(8, 8, 5, 5),
                normal = { background = texture, textColor = text },
                hover = { background = texture, textColor = text },
                active = { background = texture, textColor = text },
                onNormal = { background = texture, textColor = text },
                onHover = { background = texture, textColor = text },
                onActive = { background = texture, textColor = text }
            };
        }

        void DrawCard(Rect rect, Color fill, Color edge)
        {
            EditorGUI.DrawRect(rect, edge);
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), fill);
        }

        Texture2D RoundedTexture(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color);
            if (uiTextures.TryGetValue(key, out Texture2D existing)) return existing;
            const int size = 24;
            const float radius = 7f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                Color pixel = color; pixel.a *= dx * dx + dy * dy <= radius * radius ? 1f : 0f;
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply();
            uiTextures[key] = texture;
            return texture;
        }

        void TryEditBoard(Event e)
        {
            if (tool == Tool.None || view.Gameplay.IsRunning) return;
            RectTransform grid = GameObject.Find("Grid Stage (460 px)")?.GetComponent<RectTransform>();
            Canvas canvas = grid != null ? grid.GetComponentInParent<Canvas>() : null;
            Vector2 screen = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
            if (grid == null || !RectTransformUtility.RectangleContainsScreenPoint(grid, screen, canvas != null ? canvas.worldCamera : null)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(grid, screen, canvas != null ? canvas.worldCamera : null, out Vector2 local);
            int x = Mathf.FloorToInt(local.x / 75f); int y = Mathf.FloorToInt(-local.y / 75f);
            if (x < 0 || x > 5 || y < 0 || y > 5) return;
            if (tool == Tool.Boks && !Blocked(x,y) && !At(draft.goal,x,y)) draft.start = Cell(x,y);
            else if (tool == Tool.Goal && !Blocked(x,y) && !At(draft.start,x,y)) draft.goal = Cell(x,y);
            else if (tool == Tool.Brick && !At(draft.start,x,y) && !At(draft.goal,x,y)) ToggleBrick(x,y);
            else return;
            ApplyDraft(); e.Use();
        }

        void RememberTestProgram()
        {
            testColumn = view.Gameplay.HeroColumn;
            testRow = view.Gameplay.HeroRow;
            testFacing = view.Gameplay.Facing;
            testProgram = CaptureProgram();
            SessionState.SetString(TestProgramKey, SerializeProgram(testProgram));
            StartCoroutine(RestoreAfterTest());
        }

        IEnumerator RestoreAfterTest()
        {
            yield return new WaitUntil(() => !view.Gameplay.IsRunning && view.Gameplay.RunResolved);
            Debug.Log("[BOKS TEST] Run finished: " + (view.Gameplay.Succeeded ? "win" : "fail"));
            yield return null;
            view.Gameplay.RestoreEditorTestState(testColumn, testRow, testFacing, testProgram);
            Debug.Log("[BOKS TEST] Draft restored");
        }

        void Save()
        {
            if (!Valid()) return;
            try
            {
                string document = File.ReadAllText(CampaignPath);
                bool savingNew = newLevel;
                int saveNumber = savingNew ? HighestLevelNumber() + 1 : levelNumber;
                Debug.Log("[BOKS EDITOR] Save requested -> Level " + saveNumber);
                BOKSLevelDefinition saveLevel = BOKSLevelLoader.FromJson(JsonUtility.ToJson(draft));
                saveLevel.levelNumber = saveLevel.number = saveNumber;
                if (savingNew)
                {
                    saveLevel.id = "custom-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    saveLevel.name = "Level " + saveNumber;
                    saveLevel.icon = "leaf";
                    saveLevel.baseLevel = "level1";
                    saveLevel.characterId = "boks_green";
                }

                bool alreadyExists = TryGetLevelRecord(document, saveNumber, out _);
                if (newLevel && alreadyExists)
                {
                    message = "Save failed: Level " + saveNumber + " already exists.";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                string next = savingNew ? AppendLevel(document, saveLevel) : ReplaceLevel(document, saveNumber, saveLevel);
                if (next == null)
                {
                    message = "Save failed: level record was not found.";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                File.WriteAllText(CampaignPath, next);

                // Verify the bytes on disk before reporting success or replacing the draft state.
                string savedDocument = File.ReadAllText(CampaignPath);
                if (!TryGetLevelRecord(savedDocument, saveNumber, out string savedRecord))
                {
                    message = "Save failed verification: Level " + saveNumber + " is missing.";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                BOKSLevelDefinition saved = BOKSLevelLoader.FromJson(savedRecord);
                if (saved == null || !EditableFieldsMatch(saved, saveLevel) || (savingNew && !IdentityFieldsMatch(saved, saveLevel)))
                {
                    message = "Save failed verification: editable fields do not match the draft.";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                AssetDatabase.ImportAsset(CampaignPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                BOKSLevelLoader.ClearCampaignCache();
                BOKSLevelDefinition reloaded = BOKSLevelLoader.CampaignLevel(saveNumber);
                if (reloaded == null || !EditableFieldsMatch(reloaded, saveLevel) || (savingNew && !IdentityFieldsMatch(reloaded, saveLevel)))
                {
                    message = "Save failed verification: campaign cache did not reload Level " + saveNumber + ".";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                if (savingNew)
                {
                    levelNumber = saveNumber;
                    draft.levelNumber = draft.number = saveNumber;
                    draft.id = saveLevel.id;
                    draft.name = saveLevel.name;
                    draft.icon = saveLevel.icon;
                    draft.baseLevel = saveLevel.baseLevel;
                    draft.characterId = saveLevel.characterId;
                }
                newLevel = false;
                message = "Level " + saveNumber + " saved";
                Debug.Log("[BOKS EDITOR] Save verified -> Level " + saveNumber);
            }
            catch (Exception exception)
            {
                message = "Save failed: " + exception.Message;
                Debug.LogError("[BOKS EDITOR] " + message);
            }
        }

        int HighestLevelNumber()
        {
            List<int> levels = ExistingLevelNumbers();
            return levels.Count > 0 ? levels[levels.Count - 1] : 0;
        }

        List<int> ExistingLevelNumbers()
        {
            var levels = new List<int>();
            try
            {
                string document = File.ReadAllText(CampaignPath);
                int key = document.IndexOf("\"levels\"", StringComparison.Ordinal);
                int cursor = key < 0 ? -1 : document.IndexOf('[', key) + 1;
                while (cursor > 0 && cursor < document.Length)
                {
                    while (cursor < document.Length && (char.IsWhiteSpace(document[cursor]) || document[cursor] == ',')) cursor++;
                    if (cursor >= document.Length || document[cursor] == ']') break;
                    int end = Match(document, cursor, '{', '}');
                    if (end < 0) break;
                    BOKSLevelDefinition level = BOKSLevelLoader.FromJson(document.Substring(cursor, end - cursor + 1));
                    if (level != null && level.levelNumber > 0 && !levels.Contains(level.levelNumber)) levels.Add(level.levelNumber);
                    cursor = end + 1;
                }
                levels.Sort();
                return levels;
            }
            catch (Exception exception)
            {
                Debug.LogError("[BOKS EDITOR] Could not inspect campaign levels: " + exception.Message);
                foreach (BOKSLevelDefinition level in BOKSLevelLoader.LoadCampaign())
                    if (level != null && level.levelNumber > 0 && !levels.Contains(level.levelNumber)) levels.Add(level.levelNumber);
                levels.Sort();
                return levels;
            }
        }

        void DeleteSelectedLevel()
        {
            if (newLevel || levelNumber <= 10)
            {
                message = "Levels 1–10 cannot be deleted.";
                return;
            }
            int deletedLevel = levelNumber;
            if (!EditorUtility.DisplayDialog("Delete Level", "Delete Level " + deletedLevel + "?", "Delete", "Cancel")) return;

            try
            {
                string document = File.ReadAllText(CampaignPath);
                if (!TryGetLevelRecord(document, deletedLevel, out _))
                {
                    message = "Delete failed: Level " + deletedLevel + " was not found.";
                    return;
                }
                string next = RemoveLevel(document, deletedLevel);
                if (next == null)
                {
                    message = "Delete failed: campaign data could not be updated.";
                    return;
                }

                File.WriteAllText(CampaignPath, next);
                string savedDocument = File.ReadAllText(CampaignPath);
                if (TryGetLevelRecord(savedDocument, deletedLevel, out _))
                {
                    message = "Delete failed verification: Level " + deletedLevel + " still exists.";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                AssetDatabase.ImportAsset(CampaignPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                BOKSLevelLoader.ClearCampaignCache();
                BOKSLevelLoader.LoadCampaign();
                if (BOKSLevelLoader.CampaignLevel(deletedLevel) != null)
                {
                    message = "Delete failed verification: campaign cache still contains Level " + deletedLevel + ".";
                    Debug.LogError("[BOKS EDITOR] " + message);
                    return;
                }

                List<int> remaining = ExistingLevelNumbers();
                int fallback = FindDeleteFallback(remaining, deletedLevel);
                newLevel = false;
                if (fallback > 0)
                {
                    levelNumber = fallback;
                    LoadLevel(fallback);
                }
                message = "Level " + deletedLevel + " deleted.";
                Debug.Log("[BOKS EDITOR] Delete verified -> Level " + deletedLevel);
            }
            catch (Exception exception)
            {
                message = "Delete failed: " + exception.Message;
                Debug.LogError("[BOKS EDITOR] " + message);
            }
        }

        static int FindDeleteFallback(List<int> levels, int deletedLevel)
        {
            if (levels.Contains(10)) return 10;
            int fallback = 0;
            int bestDistance = int.MaxValue;
            foreach (int candidate in levels)
            {
                int distance = Math.Abs(candidate - deletedLevel);
                if (distance < bestDistance || (distance == bestDistance && candidate < fallback))
                {
                    fallback = candidate;
                    bestDistance = distance;
                }
            }
            return fallback;
        }

        void CreateNewLevel()
        {
            levelNumber = 0;
            newLevel = true;
            tool = Tool.None;
            draft = new BOKSLevelDefinition {
                levelNumber = 0, number = 0, characterId = "boks_green", startOri = "right", startDirection = "right",
                grid = new BOKSGrid { columns = 6, rows = 6 }, start = null, goal = null,
                obstacles = Array.Empty<BOKSGridCell>(), mainSlotEnabled = new bool[8],
                fnSlotEnabled = new bool[4], enabledBlocks = new BOKSEnabledBlocks(), glowEnabled = false
            };
            EnsureDraft();
            ApplyDraft();
            message = "New Level — unsaved";
            Debug.Log("[BOKS EDITOR] New Level -> unsaved blank draft");
        }

        void TestLevel()
        {
            Debug.Log("[BOKS TEST] Test Level pressed");
            if (!Valid())
            {
                Debug.Log("[BOKS TEST] Run rejected: editor draft validation failed");
                return;
            }
            tool = Tool.None;
            BOKSCommandType[] pendingProgram = CaptureProgram();
            SessionState.SetString(TestDraftKey, JsonUtility.ToJson(draft));
            SessionState.SetInt(TestLevelNumberKey, levelNumber);
            SessionState.SetBool(TestNewLevelKey, newLevel);
            SessionState.SetString(TestProgramKey, SerializeProgram(pendingProgram));
            int displayNumber = newLevel ? 0 : levelNumber;
            message = newLevel ? "Testing unsaved level — use command blocks and Play" : "Testing Level " + displayNumber + " — use command blocks and Play";
            Debug.Log(newLevel ? "[BOKS EDITOR] Test Level -> unsaved draft" : "[BOKS EDITOR] Test Level -> " + displayNumber);
            ApplyDraft();
            view.Gameplay.RestoreEditorTestState(draft.StartColumn, draft.StartRow, draft.Direction, pendingProgram);
            LogDraftApplied();
            if (!Application.isPlaying)
                EditorApplication.isPlaying = true;
        }

        IEnumerator ActivateTestDraftNextFrame(BOKSCommandType[] program)
        {
            // AuthoringMode.Awake can run before the gameplay controller's Awake. Wait one frame,
            // then apply the draft/program after every runtime component has initialized.
            yield return null;
            ApplyDraft();
            view.Gameplay.RestoreEditorTestState(draft.StartColumn, draft.StartRow, draft.Direction, program);
            LogDraftApplied();
        }

        BOKSCommandType[] CaptureProgram()
        {
            var program = new BOKSCommandType[12];
            for (int i = 0; i < program.Length; i++) program[i] = view.Gameplay.GetSlotCommand(i);
            return program;
        }

        static string SerializeProgram(BOKSCommandType[] program)
        {
            if (program == null) return string.Empty;
            string[] values = new string[program.Length];
            for (int i = 0; i < program.Length; i++) values[i] = ((int)program[i]).ToString();
            return string.Join(",", values);
        }

        static BOKSCommandType[] DeserializeProgram(string serialized)
        {
            var program = new BOKSCommandType[12];
            if (string.IsNullOrEmpty(serialized)) return program;
            string[] values = serialized.Split(',');
            for (int i = 0; i < program.Length && i < values.Length; i++)
                if (int.TryParse(values[i], out int value)) program[i] = (BOKSCommandType)value;
            return program;
        }

        void LogDraftApplied()
        {
            string label = newLevel ? "Draft" : "Level " + levelNumber;
            string start = draft.start == null ? "none" : "(" + draft.start.x + "," + draft.start.y + ") " + draft.startOri;
            string goal = draft.goal == null ? "none" : "(" + draft.goal.x + "," + draft.goal.y + ")";
            Debug.Log("[BOKS TEST] Draft applied: " + label + ", start " + start + ", goal " + goal);
        }

        bool Valid()
        {
            if ((draft.start != null && !Inside(draft.start)) || (draft.goal != null && !Inside(draft.goal))) { message = "BOKS start or goal is outside the board."; return false; }
            if (draft.start != null && draft.goal != null && SameCell(draft.start, draft.goal)) { message = "BOKS and goal cannot overlap."; return false; }
            foreach (var c in draft.obstacles)
                if (!Inside(c) || (draft.start != null && SameCell(c, draft.start)) || (draft.goal != null && SameCell(c, draft.goal))) { message = "Obstacle is invalid."; return false; }
            return true;
        }
        static void Resize(ref bool[] values, int count) { if (values != null && values.Length == count) return; var next = new bool[count]; if (values != null) Array.Copy(values,next,Mathf.Min(values.Length,count)); values = next; }
        static BOKSGridCell Cell(int x,int y) => new BOKSGridCell { x=x,y=y };
        static bool At(BOKSGridCell c,int x,int y) => c != null && c.x==x && c.y==y;
        static bool SameCell(BOKSGridCell a, BOKSGridCell b) => a != null && b != null && a.x == b.x && a.y == b.y;
        static bool SameNullableCell(BOKSGridCell a, BOKSGridCell b) => a == null ? b == null : b != null && a.x == b.x && a.y == b.y;
        static bool Inside(BOKSGridCell c) => c != null && c.x>=0 && c.x<6 && c.y>=0 && c.y<6;
        bool Blocked(int x,int y) { foreach(var c in draft.obstacles) if(At(c,x,y)) return true; return false; }
        void ToggleBrick(int x,int y) { var list=new List<BOKSGridCell>(draft.obstacles); int n=list.FindIndex(c=>At(c,x,y)); if(n>=0) list.RemoveAt(n); else list.Add(Cell(x,y)); draft.obstacles=list.ToArray(); }

        // Patch only this record's authorable fields, retaining its metadata and all other levels.
        static string ReplaceLevel(string json, int number, BOKSLevelDefinition level)
        {
            int key=json.IndexOf("\"levels\"",StringComparison.Ordinal), cursor=key<0?-1:json.IndexOf('[',key)+1;
            while(cursor>0 && cursor<json.Length) { while(cursor<json.Length && (char.IsWhiteSpace(json[cursor])||json[cursor]==','))cursor++; if(cursor>=json.Length||json[cursor]==']')break; int end=Match(json,cursor,'{','}'); if(end<0)return null; string item=json.Substring(cursor,end-cursor+1); var parsed=BOKSLevelLoader.FromJson(item); if(parsed!=null&&parsed.levelNumber==number) return json.Substring(0,cursor)+Patch(item,level)+json.Substring(end+1); cursor=end+1; } return null;
        }
        static string RemoveLevel(string json, int number)
        {
            int key = json.IndexOf("\"levels\"", StringComparison.Ordinal);
            int arrayStart = key < 0 ? -1 : json.IndexOf('[', key);
            int cursor = arrayStart < 0 ? -1 : arrayStart + 1;
            while (cursor > 0 && cursor < json.Length)
            {
                while (cursor < json.Length && (char.IsWhiteSpace(json[cursor]) || json[cursor] == ',')) cursor++;
                if (cursor >= json.Length || json[cursor] == ']') break;
                int itemStart = cursor;
                int itemEnd = Match(json, itemStart, '{', '}');
                if (itemEnd < 0) return null;
                BOKSLevelDefinition parsed = BOKSLevelLoader.FromJson(json.Substring(itemStart, itemEnd - itemStart + 1));
                if (parsed != null && parsed.levelNumber == number)
                {
                    int removeStart = itemStart;
                    int removeEnd = itemEnd + 1;
                    int next = removeEnd;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (next < json.Length && json[next] == ',')
                    {
                        removeEnd = next + 1;
                    }
                    else
                    {
                        int previous = removeStart - 1;
                        while (previous > arrayStart && char.IsWhiteSpace(json[previous])) previous--;
                        if (previous > arrayStart && json[previous] == ',') removeStart = previous;
                    }
                    return json.Substring(0, removeStart) + json.Substring(removeEnd);
                }
                cursor = itemEnd + 1;
            }
            return null;
        }
        static string AppendLevel(string json, BOKSLevelDefinition level)
        {
            int end = json.LastIndexOf(']'); if (end < 0) return null;
            string record = "{\"id\":" + JsonString(level.id) +
                ",\"number\":" + level.levelNumber +
                ",\"baseStepIndex\":null,\"campaignIndex\":null" +
                ",\"name\":" + JsonString(level.name) +
                ",\"icon\":" + JsonString(level.icon) +
                ",\"baseLevel\":" + JsonString(level.baseLevel) +
                ",\"characterId\":" + JsonString(level.characterId) +
                ",\"start\":" + Point(level.start) +
                ",\"goal\":" + Point(level.goal) +
                ",\"startOri\":" + JsonString(level.startOri) +
                ",\"obstacles\":" + Cells(level.obstacles) +
                ",\"decorations\":[]" +
                ",\"mainSlotEnabled\":" + Bools(level.mainSlotEnabled) +
                ",\"fnSlotEnabled\":" + Bools(level.fnSlotEnabled) +
                ",\"enabledBlocks\":{\"forward\":" + Bool(level.enabledBlocks.forward) + ",\"left\":" + Bool(level.enabledBlocks.left) + ",\"right\":" + Bool(level.enabledBlocks.right) + ",\"function\":" + Bool(level.enabledBlocks.function) + "}" +
                ",\"themeOverrides\":{},\"levelHints\":{\"availableBlockGlow\":" + Bool(level.glowEnabled) + "}}";
            return json.Substring(0, end).TrimEnd() + (json.Substring(0, end).TrimEnd().EndsWith("[") ? "" : ",") + record + json.Substring(end);
        }
        static bool TryGetLevelRecord(string json, int number, out string record)
        {
            record = null;
            int key = json.IndexOf("\"levels\"", StringComparison.Ordinal);
            int cursor = key < 0 ? -1 : json.IndexOf('[', key) + 1;
            while (cursor > 0 && cursor < json.Length)
            {
                while (cursor < json.Length && (char.IsWhiteSpace(json[cursor]) || json[cursor] == ',')) cursor++;
                if (cursor >= json.Length || json[cursor] == ']') break;
                int end = Match(json, cursor, '{', '}');
                if (end < 0) return false;
                string item = json.Substring(cursor, end - cursor + 1);
                BOKSLevelDefinition parsed = BOKSLevelLoader.FromJson(item);
                if (parsed != null && parsed.levelNumber == number)
                {
                    record = item;
                    return true;
                }
                cursor = end + 1;
            }
            return false;
        }
        static string Patch(string s,BOKSLevelDefinition l) { s=Set(s,"start",Point(l.start)); s=Set(s,"goal",Point(l.goal)); s=Set(s,"startOri","\""+l.startOri+"\""); s=Set(s,"obstacles",Cells(l.obstacles)); s=Set(s,"mainSlotEnabled",Bools(l.mainSlotEnabled)); s=Set(s,"fnSlotEnabled",Bools(l.fnSlotEnabled)); return Set(s,"enabledBlocks",$"{{\"forward\":{Bool(l.enabledBlocks.forward)},\"left\":{Bool(l.enabledBlocks.left)},\"right\":{Bool(l.enabledBlocks.right)},\"function\":{Bool(l.enabledBlocks.function)}}}"); }
        static string Set(string s,string name,string value) { int p=Property(s,name); if(p>=0){int e=End(s,p);return s.Substring(0,p)+value+s.Substring(e);} int close=s.LastIndexOf('}');return s.Substring(0,close)+",\""+name+"\":"+value+s.Substring(close); }
        static int Property(string s,string name) { int d=0;for(int i=0;i<s.Length;i++){if(s[i]=='"'){int q=QuoteEnd(s,i);if(d==1&&s.Substring(i+1,q-i-1)==name){int c=q+1;while(char.IsWhiteSpace(s[c]))c++;if(s[c]==':'){c++;while(char.IsWhiteSpace(s[c]))c++;return c;}}i=q;}else if(s[i]=='{')d++;else if(s[i]=='}')d--;}return -1; }
        static int End(string s,int p) { if(s[p]=='"')return QuoteEnd(s,p)+1;if(s[p]=='{' )return Match(s,p,'{','}')+1;if(s[p]=='[')return Match(s,p,'[',']')+1;while(p<s.Length&&s[p]!=','&&s[p]!='}')p++;return p; }
        static int Match(string s,int p,char open,char close) { int d=0;for(int i=p;i<s.Length;i++){if(s[i]=='"'){i=QuoteEnd(s,i);continue;}if(s[i]==open)d++;if(s[i]==close&&--d==0)return i;}return -1; }
        static int QuoteEnd(string s,int p){for(int i=p+1;i<s.Length;i++)if(s[i]=='"'&&s[i-1]!='\\')return i;return s.Length-1;}
        static bool EditableFieldsMatch(BOKSLevelDefinition a, BOKSLevelDefinition b)
        {
            return a != null && b != null &&
                SameNullableCell(a.start, b.start) && SameNullableCell(a.goal, b.goal) &&
                string.Equals(a.startOri, b.startOri, StringComparison.Ordinal) &&
                SameCells(a.obstacles, b.obstacles) && SameBools(a.mainSlotEnabled, b.mainSlotEnabled) &&
                SameBools(a.fnSlotEnabled, b.fnSlotEnabled) && SameBlocks(a.enabledBlocks, b.enabledBlocks);
        }
        static bool IdentityFieldsMatch(BOKSLevelDefinition a, BOKSLevelDefinition b) =>
            a != null && b != null && a.levelNumber == b.levelNumber &&
            string.Equals(a.id, b.id, StringComparison.Ordinal) && string.Equals(a.name, b.name, StringComparison.Ordinal) &&
            string.Equals(a.icon, b.icon, StringComparison.Ordinal) && string.Equals(a.baseLevel, b.baseLevel, StringComparison.Ordinal) &&
            string.Equals(a.characterId, b.characterId, StringComparison.Ordinal);
        static bool SameCells(BOKSGridCell[] a, BOKSGridCell[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!SameNullableCell(a[i], b[i])) return false;
            return true;
        }
        static bool SameBools(bool[] a, bool[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        static bool SameBlocks(BOKSEnabledBlocks a, BOKSEnabledBlocks b) =>
            a != null && b != null && a.forward == b.forward && a.left == b.left && a.right == b.right && a.function == b.function;
        static string JsonString(string value) => "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string Point(BOKSGridCell c)=>c == null ? "null" : $"{{\"x\":{c.x},\"y\":{c.y}}}"; static string Cells(BOKSGridCell[] a){var x=new List<string>();if(a!=null)foreach(var c in a)x.Add(Point(c));return "["+string.Join(",",x)+"]";} static string Bools(bool[] a){if(a==null)return "[]";var x=new string[a.Length];for(int i=0;i<a.Length;i++)x[i]=Bool(a[i]);return "["+string.Join(",",x)+"]";} static string Bool(bool x)=>x?"true":"false";
    }
}
#endif
