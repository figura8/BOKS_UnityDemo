using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Campaign Level 2 only: two Forward slots, one-cell movement, success and reset.
    /// Kept deliberately small so the execution order is easy to follow in class.
    /// </summary>
    public sealed class BOKSLevel2Controller : MonoBehaviour
    {
        public const float RunLeadSeconds = 0.200f;
        public const float SlotLeadSeconds = 0.180f;
        public const float MoveSeconds = 1.050f;
        public const float NonGoalSettleSeconds = 0.080f;
        public const float PostCommandSeconds = 0.180f;
        public const float GoalResolutionSeconds = 1.410f;
        public const float FailureResetSeconds = 0.400f;
        public const float SuccessHoldSeconds = 0.260f;
        public const float TurnSeconds = 1.050f;             // source TURN_MS
        public const float BlockedMoveSeconds = 0.120f;      // source blockedMoveWaitMs (effort wait before continuing)
        public const float ObstacleStruggleSeconds = 0.340f; // source obstacleShakeMs (struggle visual)
        public const float EmptyFunctionSeconds = 0.300f;

        // Presentation constant: 460px board / 6 cells = 75px cell stride. Identical for every 6x6
        // level, so it stays here rather than in level data.
        const float CellStride = 75f;

        [SerializeField] BOKSLevelDefinition levelDefinition;
        [SerializeField] RectTransform hero;
        [SerializeField] Image heroArt;
        [SerializeField] RectTransform heroVisual;
        [SerializeField] Sprite[] facingSprites;
        [SerializeField] Button[] paletteButtons;
        [SerializeField] Button playButton;
        [SerializeField] GameObject[] commandVisuals;
        [SerializeField] BOKSShapeGraphic[] enabledSlotWells;
        [SerializeField] BOKSShapeGraphic[] runDots;
        [SerializeField] RectTransform runButtonRoot;
        [SerializeField] RectTransform runShell;
        [SerializeField] GameObject[] paletteGlowLayers;

        BOKSCommandType[] logicalProgram;
        readonly HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
        int startColumn = 1;
        int startRow = 3;
        int goalColumn = 3;
        int goalRow = 3;
        int gridColumns = 6;
        int gridRows = 6;
        BOKSDirection facing = BOKSDirection.Right;
        Color[] wellTop;
        Color[] wellBottom;
        Vector2 heroStart;
        Vector2 runStart;
        Vector2 shellStart;
        int programCount;
        int heroColumn = 1;
        int heroRow = 3;
        bool inputLocked;
        bool running;
        bool succeeded;
        bool runResolved;
        BOKSCommandDragSource activeDrag;
        int pendingDropSlot = -1;
        bool pendingDropLocked;
        int hoveredSlot = -1;
        bool[] enabledSlots;

        public event Action<BOKSLevel2Controller> LevelCompleted;
        public event Action<BOKSLevel2Controller> GoalCelebration;

        [SerializeField] BOKSGoalPopVFX goalPopVfx;

        public void BindGoalPopVFX(BOKSGoalPopVFX vfx) => goalPopVfx = vfx;

        BOKSGoalPopVFX GoalPopVfx
        {
            get
            {
                if (goalPopVfx == null) goalPopVfx = FindAnyObjectByType<BOKSGoalPopVFX>();
                return goalPopVfx;
            }
        }

        // Automated tests set this to zero; normal play always uses 1.
        public float TimingScale { get; set; } = 1f;
        public int LogicalProgramCount => programCount;
        public int VisualCommandCount => CountVisibleCommands();
        public int HeroColumn => heroColumn;
        public int HeroRow => heroRow;
        public BOKSDirection Facing => facing;
        public bool IsRunning => running;
        public bool InputLocked => inputLocked;
        public bool Succeeded => succeeded;
        public bool RunResolved => runResolved;
        public bool CanEditProgram => !inputLocked;
        public int LevelNumber => levelDefinition != null ? levelDefinition.levelNumber : 0;

        /// <summary>Grid-cell anchored position of the character root. Must never change during a turn.</summary>
        public Vector2 HeroRootAnchoredPosition => hero != null ? hero.anchoredPosition : Vector2.zero;

        /// <summary>Name of the currently displayed directional sprite (test/observability hook).</summary>
        public string HeroSpriteName => (heroArt != null && heroArt.sprite != null) ? heroArt.sprite.name : string.Empty;

        public void Configure(BOKSLevelDefinition definition, RectTransform heroTransform, Image heroArtImage, RectTransform heroVisualTransform, Sprite[] sprites,
            Button[] buttons, Button play, GameObject[] commands, BOKSShapeGraphic[] wells, BOKSShapeGraphic[] dots,
            RectTransform runRoot, RectTransform shell, GameObject[] glows)
        {
            levelDefinition = definition;
            hero = heroTransform;
            heroArt = heroArtImage;
            heroVisual = heroVisualTransform;
            facingSprites = sprites;
            paletteButtons = buttons;
            playButton = play;
            commandVisuals = commands;
            enabledSlotWells = wells;
            runDots = dots;
            runButtonRoot = runRoot;
            runShell = shell;
            paletteGlowLayers = glows;

            InitializeFromDefinition();

            heroStart = hero.anchoredPosition;
            runStart = runButtonRoot.anchoredPosition;
            shellStart = runShell.anchoredPosition;
            wellTop = new Color[enabledSlotWells.Length];
            wellBottom = new Color[enabledSlotWells.Length];
            for (int i = 0; i < enabledSlotWells.Length; i++)
            {
                wellTop[i] = enabledSlotWells[i].topColor;
                wellBottom[i] = enabledSlotWells[i].bottomColor;
            }
        }

        /// <summary>
        /// Derives gameplay state from the level definition (start/goal cell, grid size, facing,
        /// obstacles and the size of the main-slot command array). Falls back to the Level 2 defaults
        /// when no definition is present (e.g. a scene built before the definition was serialised).
        /// </summary>
        void InitializeFromDefinition()
        {
            if (levelDefinition == null)
                levelDefinition = BOKSLevelLoader.CreateLevel2Fallback();

            startColumn = levelDefinition.StartColumn;
            startRow = levelDefinition.StartRow;
            goalColumn = levelDefinition.GoalColumn;
            goalRow = levelDefinition.GoalRow;
            gridColumns = levelDefinition.GridColumns;
            gridRows = levelDefinition.GridRows;
            facing = levelDefinition.Direction;

            blockedCells.Clear();
            if (levelDefinition.obstacles != null)
                foreach (BOKSGridCell cell in levelDefinition.obstacles)
                    if (cell != null) blockedCells.Add(new Vector2Int(cell.x, cell.y));

            int slotCount = commandVisuals != null && commandVisuals.Length > 0
                ? commandVisuals.Length
                : Mathf.Max(1, levelDefinition.EnabledMainSlotCount);

            logicalProgram = new BOKSCommandType[slotCount];
            enabledSlots = new bool[slotCount];
            for (int i = 0; i < logicalProgram.Length; i++)
            {
                logicalProgram[i] = BOKSCommandType.None;
                enabledSlots[i] = commandVisuals != null && commandVisuals.Length == 12
                    ? (i < 8 ? levelDefinition.IsMainSlotEnabled(i) : levelDefinition.IsFunctionSlotEnabled(i - 8))
                    : true;
            }
        }

        public void LoadLevel(BOKSLevelDefinition definition, Sprite[] sprites)
        {
            StopAllCoroutines();
            levelDefinition = definition;
            if (sprites != null && sprites.Length == 4) facingSprites = sprites;
            InitializeFromDefinition();
            // Reposition the root to the new start cell before caching, so CacheInitialState
            // records the correct heroStart instead of the previous level's stale position.
            hero.anchoredPosition = new Vector2(7 + startColumn * CellStride + 34.5f, -(7 + startRow * CellStride + 34.5f));
            CacheInitialState();
            RestartLevel2();
        }

        void Awake()
        {
            InitializeFromDefinition();
            CacheInitialState();
            playButton.onClick.AddListener(Play);
            RestartLevel2();
        }

        void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Play);
        }

        public void AddForward() => TryAddForward();

        public bool TryAddForward() => TryAddCommand(BOKSCommandType.Forward);
        public bool TryAddLeft() => TryAddCommand(BOKSCommandType.Left);
        public bool TryAddRight() => TryAddCommand(BOKSCommandType.Right);

        /// <summary>Places the given command in the first empty enabled main slot.</summary>
        public bool TryAddCommand(BOKSCommandType command)
        {
            if (inputLocked) return false;
            for (int i = 0; i < logicalProgram.Length; i++)
                if (logicalProgram[i] == BOKSCommandType.None) return TryPlaceCommand(i, command);
            return false;
        }

        public void Play() => TryPlay();

        public bool TryPlay()
        {
            if (inputLocked || CountLogicalCommands() == 0) return false;
            inputLocked = true;
            running = true;
            succeeded = false;
            runResolved = false;
            SetPaletteInteractable(false);
            playButton.interactable = false;
            SetRunPressed(true);
            BOKSAudioManager.Play(BOKSAudioCue.PlayPressed);
            StartCoroutine(ExecuteProgram());
            return true;
        }

        IEnumerator ExecuteProgram()
        {
            yield return Wait(RunLeadSeconds);

            BOKSCommandType[] commandsToRun = (BOKSCommandType[])logicalProgram.Clone();
            int last = LastCommandIndex(commandsToRun, 0, Mathf.Min(7, commandsToRun.Length - 1));
            bool won = false;
            for (int i = 0; i <= last; i++)
            {
                SetSlotActive(i);
                yield return Wait(SlotLeadSeconds);

                if (commandsToRun[i] == BOKSCommandType.Function)
                {
                    int functionLast = LastCommandIndex(commandsToRun, 8, Mathf.Min(11, commandsToRun.Length - 1));
                    if (functionLast < 8)
                        yield return Wait(EmptyFunctionSeconds);
                    else
                    {
                        for (int fn = 8; fn <= functionLast; fn++)
                        {
                            SetSlotActive(fn);
                            yield return Wait(SlotLeadSeconds);
                            if (commandsToRun[fn] != BOKSCommandType.None)
                            {
                                bool commandWon = false;
                                yield return ExecuteCommand(commandsToRun[fn], value => commandWon = value);
                                if (commandWon) { won = true; break; }
                                yield return Wait(PostCommandSeconds);
                            }
                            else yield return Wait(PostCommandSeconds);
                        }
                        if (won) break;
                    }
                }
                else if (commandsToRun[i] == BOKSCommandType.None)
                {
                    // Empty interior slots consume the same extra STEP_MS as the web queue.
                    yield return Wait(PostCommandSeconds);
                }
                else
                {
                    bool commandWon = false;
                    yield return ExecuteCommand(commandsToRun[i], value => commandWon = value);
                    if (commandWon) { won = true; break; }
                    yield return Wait(PostCommandSeconds);
                }
            }

            ClearExecutionHighlights();
            SetRunPressed(false);
            running = false;

            // Web campaign behavior: data clears now, while placed block visuals remain.
            ClearLogicalProgramOnly();
            SetPaletteGlow(true);

            if (won)
            {
                succeeded = true;
                yield return Wait(SuccessHoldSeconds);
                // Level transitions/redraw are intentionally not implemented yet, so placed commands
                // remain visible until the next redraw, matching the source's campaign flow.
                runResolved = true;
                LevelCompleted?.Invoke(this);
                yield break;
            }

            BOKSAudioManager.Play(BOKSAudioCue.Failure);
            yield return Wait(FailureResetSeconds);
            RedrawLevel2AfterFailure();
            inputLocked = false;
            SetPaletteInteractable(true);
            playButton.interactable = true;
            runResolved = true;
        }

        IEnumerator ExecuteCommand(BOKSCommandType command, Action<bool> completion)
        {
            bool won = false;
            if (command == BOKSCommandType.Forward)
            {
                Vector2Int destination = ClampToGrid(ForwardCell());
                if (blockedCells.Contains(destination)) yield return MoveBlocked();
                else
                {
                    bool enteringGoal = destination.x == goalColumn && destination.y == goalRow;
                    yield return MoveForward(destination, enteringGoal);
                    won = enteringGoal;
                    if (!enteringGoal) yield return Wait(NonGoalSettleSeconds);
                }
            }
            else if (command == BOKSCommandType.Left) yield return Turn(TurnLeft(facing), -90f);
            else if (command == BOKSCommandType.Right) yield return Turn(TurnRight(facing), 90f);
            completion(won);
        }

        int LastCommandIndex(BOKSCommandType[] commands, int first, int last)
        {
            for (int i = Mathf.Min(last, commands.Length - 1); i >= first; i--)
                if (commands[i] != BOKSCommandType.None) return i;
            return -1;
        }

        IEnumerator MoveForward(Vector2Int destination, bool enteringGoal)
        {
            BOKSAudioManager.Play(BOKSAudioCue.ForwardStep);
            if (enteringGoal) StartCoroutine(PlayGoalAudioSequence());

            Vector2 from = hero.anchoredPosition;
            Vector2 to = CellScreenPosition(destination);
            float duration = MoveSeconds * Mathf.Max(0f, TimingScale);
            if (duration <= 0f)
            {
                hero.anchoredPosition = to;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float eased = CubicBezierYForX(progress, .2f, .9f, .2f, 1f);
                    hero.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                    yield return null;
                }
                hero.anchoredPosition = to;
            }

            heroColumn = destination.x;
            heroRow = destination.y;
            if (enteringGoal)
                yield return Wait(GoalResolutionSeconds - MoveSeconds);
        }

        IEnumerator PlayGoalAudioSequence()
        {
            yield return Wait(MoveSeconds * .20f);
            BOKSAudioManager.Play(BOKSAudioCue.BubblePop);
            GoalPopVfx?.Play(CellScreenPosition(new Vector2Int(goalColumn, goalRow)));
            yield return Wait(1f - MoveSeconds * .20f);
            BOKSAudioManager.Play(BOKSAudioCue.LevelComplete);
            GoalCelebration?.Invoke(this);
        }

        /// <summary>Blocked forward: the character stays put; a short struggle plays and the queue continues.</summary>
        IEnumerator MoveBlocked()
        {
            BOKSAudioManager.Play(BOKSAudioCue.BlockedMove);
            StartCoroutine(ObstacleStruggle());
            yield return Wait(BlockedMoveSeconds);
        }

        IEnumerator ObstacleStruggle()
        {
            float duration = ObstacleStruggleSeconds * Mathf.Max(0f, TimingScale);
            if (duration <= 0f) yield break;
            // Shake the visual child, not the grid root, so the root's cell position stays stable.
            RectTransform shakeTarget = heroVisual != null ? heroVisual : hero;
            Vector2 basePos = shakeTarget.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float shake = Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * 3f;
                shakeTarget.anchoredPosition = basePos + new Vector2(shake, 0f);
                yield return null;
            }
            shakeTarget.anchoredPosition = basePos;
        }

        /// <summary>
        /// Matches the web turn interpolation: render the destination art counter-rotated into the
        /// current pose, overshoot at 72%, settle at identity, then commit logical direction.
        /// The visual child is pivoted at the grid-cell centre, so the root never moves.
        /// </summary>
        IEnumerator Turn(BOKSDirection newFacing, float deltaDegrees)
        {
            BOKSAudioManager.Play(BOKSAudioCue.Turn);
            float duration = TurnSeconds * Mathf.Max(0f, TimingScale);
            if (duration <= 0f)
            {
                SetFacing(newFacing);
                yield break;
            }

            Vector2 basePosition = heroVisual != null ? heroVisual.anchoredPosition : Vector2.zero;
            Vector3 baseScale = heroVisual != null ? heroVisual.localScale : Vector3.one;
            ApplyFacingSprite(newFacing);

            float overshoot = Mathf.Sign(deltaDegrees) * Mathf.Min(10f, Mathf.Max(4f, Mathf.Abs(deltaDegrees) * .12f));
            ApplyTurnKeyframes(0f, deltaDegrees, overshoot, basePosition, baseScale);
            float startedAt = Time.unscaledTime;
            yield return null;

            float elapsed = Time.unscaledTime - startedAt;
            while (elapsed < duration)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = CubicBezierYForX(progress, .22f, 1f, .36f, 1f);
                ApplyTurnKeyframes(eased, deltaDegrees, overshoot, basePosition, baseScale);
                yield return null;
                elapsed = Time.unscaledTime - startedAt;
            }

            facing = newFacing;
            ResetHeroVisual(basePosition, baseScale);
        }

        void ApplyTurnKeyframes(float progress, float startAngle, float overshoot, Vector2 basePosition, Vector3 baseScale)
        {
            if (heroVisual == null) return;

            const float overshootOffset = .72f;
            bool approachingOvershoot = progress <= overshootOffset;
            float segmentProgress = approachingOvershoot
                ? progress / overshootOffset
                : (progress - overshootOffset) / (1f - overshootOffset);
            float angle = approachingOvershoot
                ? Mathf.Lerp(startAngle, -overshoot, segmentProgress)
                : Mathf.Lerp(-overshoot, 0f, segmentProgress);
            float scale = approachingOvershoot
                ? Mathf.Lerp(.97f, 1.02f, segmentProgress)
                : Mathf.Lerp(1.02f, 1f, segmentProgress);
            float lift = approachingOvershoot
                ? Mathf.Lerp(0f, heroVisual.rect.height * .015f, segmentProgress)
                : Mathf.Lerp(heroVisual.rect.height * .015f, 0f, segmentProgress);

            heroVisual.localRotation = Quaternion.Euler(0, 0, angle);
            heroVisual.localScale = Vector3.Scale(baseScale, new Vector3(scale, scale, 1f));
            heroVisual.anchoredPosition = basePosition + Vector2.up * lift;
        }

        void ResetHeroVisual(Vector2 anchoredPosition, Vector3 scale)
        {
            if (heroVisual == null) return;
            heroVisual.localRotation = Quaternion.identity;
            heroVisual.localScale = scale;
            heroVisual.anchoredPosition = anchoredPosition;
        }

        void SetFacing(BOKSDirection direction)
        {
            facing = direction;
            ApplyFacingSprite();
        }

        /// <summary>
        /// Immediately applies a single turn (clockwise for Right, counter-clockwise for Left) without run
        /// timing. Swaps facing/art but never moves the root. Used by tests and future UI previews.
        /// </summary>
        public void TurnImmediate(BOKSCommandType turn)
        {
            if (turn == BOKSCommandType.Left) SetFacing(TurnLeft(facing));
            else if (turn == BOKSCommandType.Right) SetFacing(TurnRight(facing));
        }

        void ApplyFacingSprite()
        {
            ApplyFacingSprite(facing);
        }

        void ApplyFacingSprite(BOKSDirection direction)
        {
            if (heroArt != null && facingSprites != null && facingSprites.Length == 4)
            {
                Sprite sprite = facingSprites[(int)direction];
                if (sprite != null) heroArt.sprite = sprite;
            }
            if (heroVisual != null)
                heroVisual.localRotation = Quaternion.identity;
        }

        Vector2Int ForwardCell()
        {
            switch (facing)
            {
                case BOKSDirection.Up: return new Vector2Int(heroColumn, heroRow - 1);
                case BOKSDirection.Left: return new Vector2Int(heroColumn - 1, heroRow);
                case BOKSDirection.Down: return new Vector2Int(heroColumn, heroRow + 1);
                default: return new Vector2Int(heroColumn + 1, heroRow);
            }
        }

        Vector2Int ClampToGrid(Vector2Int cell)
        {
            return new Vector2Int(
                Mathf.Clamp(cell.x, 0, gridColumns - 1),
                Mathf.Clamp(cell.y, 0, gridRows - 1));
        }

        Vector2 CellScreenPosition(Vector2Int cell)
        {
            return heroStart
                + Vector2.right * ((cell.x - startColumn) * CellStride)
                + Vector2.down * ((cell.y - startRow) * CellStride);
        }

        static BOKSDirection TurnLeft(BOKSDirection direction)
        {
            switch (direction)
            {
                case BOKSDirection.Up: return BOKSDirection.Left;
                case BOKSDirection.Left: return BOKSDirection.Down;
                case BOKSDirection.Down: return BOKSDirection.Right;
                case BOKSDirection.Right: return BOKSDirection.Up;
                default: return direction;
            }
        }

        static BOKSDirection TurnRight(BOKSDirection direction)
        {
            switch (direction)
            {
                case BOKSDirection.Up: return BOKSDirection.Right;
                case BOKSDirection.Right: return BOKSDirection.Down;
                case BOKSDirection.Down: return BOKSDirection.Left;
                case BOKSDirection.Left: return BOKSDirection.Up;
                default: return direction;
            }
        }

        void SetPaletteInteractable(bool interactable)
        {
            if (paletteButtons != null)
                foreach (Button button in paletteButtons)
                    if (button != null) button.interactable = interactable;
        }

        IEnumerator Wait(float seconds)
        {
            float duration = seconds * Mathf.Max(0f, TimingScale);
            if (duration <= 0f) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void SetSlotActive(int index)
        {
            Color active = new Color32(198, 227, 255, 255);
            Color done = new Color32(213, 187, 147, 255);
            for (int i = 0; i < enabledSlotWells.Length; i++)
            {
                Color c = i < index ? done : i == index ? active : wellTop[i];
                enabledSlotWells[i].topColor = c;
                enabledSlotWells[i].bottomColor = c;
                enabledSlotWells[i].SetVerticesDirty();
            }
            for (int i = 0; i < runDots.Length; i++)
                SetShapeColor(runDots[i], i <= index ? new Color32(212, 247, 124, 255) : new Color32(178, 177, 150, 255));
        }

        void ClearExecutionHighlights()
        {
            for (int i = 0; i < enabledSlotWells.Length; i++)
            {
                enabledSlotWells[i].topColor = wellTop[i];
                enabledSlotWells[i].bottomColor = wellBottom[i];
                enabledSlotWells[i].SetVerticesDirty();
            }
            foreach (BOKSShapeGraphic dot in runDots)
                SetShapeColor(dot, new Color32(178, 177, 150, 255));
        }

        static void SetShapeColor(BOKSShapeGraphic shape, Color color)
        {
            shape.topColor = color;
            shape.bottomColor = color;
            shape.SetVerticesDirty();
        }

        void SetRunPressed(bool pressed)
        {
            runButtonRoot.anchoredPosition = runStart + (pressed ? Vector2.down * 3f : Vector2.zero);
            runShell.anchoredPosition = shellStart + (pressed ? Vector2.down : Vector2.zero);
        }

        void SetPaletteGlow(bool visible)
        {
            foreach (GameObject glow in paletteGlowLayers) glow.SetActive(visible);
        }

        void ClearLogicalProgramOnly()
        {
            for (int i = 0; i < logicalProgram.Length; i++) logicalProgram[i] = BOKSCommandType.None;
            programCount = 0;
        }

        public bool IsSlotFilled(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < logicalProgram.Length && logicalProgram[slotIndex] != BOKSCommandType.None;
        }

        public BOKSCommandType GetSlotCommand(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < logicalProgram.Length
                ? logicalProgram[slotIndex]
                : BOKSCommandType.None;
        }

        public bool TryPlacePaletteCommand(int targetSlot) => TryPlaceCommand(targetSlot, BOKSCommandType.Forward);

        public bool TryPlaceCommand(int targetSlot, BOKSCommandType command)
        {
            if (inputLocked || targetSlot < 0 || targetSlot >= logicalProgram.Length) return false;
            if (enabledSlots != null && targetSlot < enabledSlots.Length && !enabledSlots[targetSlot]) return false;
            if (levelDefinition != null && !levelDefinition.IsCommandEnabled(command)) return false;
            // Palette prototypes are unlimited and replace destination contents in the web game.
            logicalProgram[targetSlot] = command;
            RefreshCommandVisuals();
            return true;
        }

        public bool TryMovePlacedCommand(int sourceSlot, int targetSlot)
        {
            if (inputLocked || sourceSlot < 0 || sourceSlot >= logicalProgram.Length ||
                targetSlot < 0 || targetSlot >= logicalProgram.Length || logicalProgram[sourceSlot] == BOKSCommandType.None) return false;
            if (sourceSlot == targetSlot) return true;
            BOKSCommandType destination = logicalProgram[targetSlot];
            logicalProgram[targetSlot] = logicalProgram[sourceSlot];
            logicalProgram[sourceSlot] = destination;
            RefreshCommandVisuals();
            return true;
        }

        public bool TryRemovePlacedCommand(int sourceSlot)
        {
            if (inputLocked || sourceSlot < 0 || sourceSlot >= logicalProgram.Length || logicalProgram[sourceSlot] == BOKSCommandType.None) return false;
            logicalProgram[sourceSlot] = BOKSCommandType.None;
            RefreshCommandVisuals();
            return true;
        }

        public bool BeginCommandDrag(BOKSCommandDragSource source)
        {
            if (inputLocked) return false;
            activeDrag = source;
            pendingDropSlot = -1;
            pendingDropLocked = false;
            BOKSAudioManager.Play(BOKSAudioCue.BlockDetach);
            return true;
        }

        public void RecordDropTarget(int slotIndex, bool enabled)
        {
            if (activeDrag == null) return;
            pendingDropSlot = slotIndex;
            pendingDropLocked = !enabled;
        }

        public void SetDropHover(int slotIndex, bool enabled, bool hovering, bool playHoverCue = true)
        {
            if (activeDrag == null || !enabled || slotIndex < 0 || slotIndex >= enabledSlotWells.Length) return;
            if (hovering)
            {
                if (!activeDrag.IsPaletteSource && activeDrag.SlotIndex == slotIndex) return;
                if (hoveredSlot == slotIndex) return;
                ClearDropHover();
                hoveredSlot = slotIndex;
                BOKSShapeGraphic well = enabledSlotWells[slotIndex];
                well.borderColor = new Color32(185, 138, 78, 255);
                well.borderWidth = 2f;
                well.SetVerticesDirty();
                if (playHoverCue) BOKSAudioManager.Play(BOKSAudioCue.SlotHover);
            }
            else if (hoveredSlot == slotIndex)
            {
                ClearDropHover();
            }
        }

        public void CompleteCommandDrag(BOKSCommandDragSource source, float dragDistance)
        {
            if (source != activeDrag) return;

            bool didDropSuccessfully = false;
            if (dragDistance >= 20f)
            {
                if (pendingDropSlot >= 0)
                {
                    if (!pendingDropLocked)
                    {
                        if (source.IsPaletteSource)
                            didDropSuccessfully = TryPlaceCommand(pendingDropSlot, source.CommandType);
                        else if (source.SlotIndex != pendingDropSlot)
                            didDropSuccessfully = TryMovePlacedCommand(source.SlotIndex, pendingDropSlot);
                    }
                    // A locked destination consumes the drop but leaves the source unchanged.
                }
                else if (!source.IsPaletteSource)
                {
                    // Placed commands dropped outside every slot are deleted.
                    TryRemovePlacedCommand(source.SlotIndex);
                }
            }

            if (didDropSuccessfully) BOKSAudioManager.Play(BOKSAudioCue.BlockDropSuccess);

            ClearDropHover();
            activeDrag = null;
            pendingDropSlot = -1;
            pendingDropLocked = false;
        }

        void ClearDropHover()
        {
            if (hoveredSlot >= 0 && hoveredSlot < enabledSlotWells.Length)
            {
                BOKSShapeGraphic well = enabledSlotWells[hoveredSlot];
                well.borderColor = new Color32(188, 153, 107, 255);
                well.borderWidth = 1f;
                well.SetVerticesDirty();
            }
            hoveredSlot = -1;
        }

        void RefreshCommandVisuals()
        {
            programCount = CountLogicalCommands();
            for (int i = 0; i < logicalProgram.Length; i++)
            {
                BOKSCommandType command = logicalProgram[i];
                GameObject visual = commandVisuals[i];
                bool filled = command != BOKSCommandType.None;
                if (filled)
                {
                    Image image = visual.GetComponent<Image>();
                    Sprite sprite = PaletteSpriteFor(command);
                    if (image != null && sprite != null) image.sprite = sprite;

                    BOKSCommandDragSource drag = visual.GetComponent<BOKSCommandDragSource>();
                    if (drag != null) drag.Configure(this, false, i, command);
                }
                visual.SetActive(filled);
            }
            SetPaletteGlow(programCount == 0);
        }

        Sprite PaletteSpriteFor(BOKSCommandType command)
        {
            foreach (Button paletteButton in paletteButtons)
            {
                if (paletteButton == null) continue;
                BOKSCommandDragSource drag = paletteButton.GetComponent<BOKSCommandDragSource>();
                if (drag != null && drag.CommandType == command)
                    return paletteButton.image != null ? paletteButton.image.sprite : null;
            }
            return null;
        }

        int CountLogicalCommands()
        {
            int count = 0;
            foreach (BOKSCommandType command in logicalProgram) if (command != BOKSCommandType.None) count++;
            return count;
        }

        void RedrawLevel2AfterFailure()
        {
            foreach (GameObject command in commandVisuals) command.SetActive(false);
            hero.anchoredPosition = heroStart;
            heroColumn = startColumn;
            heroRow = startRow;
            SetFacing(levelDefinition.Direction);
            ClearExecutionHighlights();
        }

        public void RestartLevel2()
        {
            StopAllCoroutines();
            GoalPopVfx?.Reset();
            activeDrag = null;
            pendingDropSlot = -1;
            ClearDropHover();
            ClearLogicalProgramOnly();
            foreach (GameObject command in commandVisuals) command.SetActive(false);
            hero.anchoredPosition = heroStart;
            heroColumn = startColumn;
            heroRow = startRow;
            SetFacing(levelDefinition.Direction);
            inputLocked = false;
            running = false;
            succeeded = false;
            runResolved = true;
            SetPaletteInteractable(true);
            playButton.interactable = true;
            SetRunPressed(false);
            SetPaletteGlow(true);
            ClearExecutionHighlights();
        }

        public void SetCampaignInputLocked(bool locked)
        {
            inputLocked = locked;
            SetPaletteInteractable(!locked);
            if (playButton != null) playButton.interactable = !locked;
        }

        void CacheInitialState()
        {
            heroStart = hero.anchoredPosition;
            runStart = runButtonRoot.anchoredPosition;
            shellStart = runShell.anchoredPosition;
            wellTop = new Color[enabledSlotWells.Length];
            wellBottom = new Color[enabledSlotWells.Length];
            for (int i = 0; i < enabledSlotWells.Length; i++)
            {
                wellTop[i] = enabledSlotWells[i].topColor;
                wellBottom[i] = enabledSlotWells[i].bottomColor;
            }
        }

        int CountVisibleCommands()
        {
            int count = 0;
            foreach (GameObject command in commandVisuals)
                if (command.activeSelf) count++;
            return count;
        }

        static float CubicBezierYForX(float x, float x1, float y1, float x2, float y2)
        {
            float low = 0f;
            float high = 1f;
            float t = x;
            for (int i = 0; i < 10; i++)
            {
                t = (low + high) * .5f;
                float estimate = Cubic(t, x1, x2);
                if (estimate < x) low = t; else high = t;
            }
            return Cubic(t, y1, y2);
        }

        static float Cubic(float t, float p1, float p2)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }
    }
}
