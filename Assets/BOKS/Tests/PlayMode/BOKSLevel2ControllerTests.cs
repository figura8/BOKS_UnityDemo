using System.Collections;
using System.Reflection;
using BOKS.Demo;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BOKS.Tests
{
    public sealed class BOKSLevel2ControllerTests
    {
        BOKSLevel2Controller controller;

        [UnitySetUp]
        public IEnumerator OpenLevel2()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Archive/BOKS_Level02.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            controller = Object.FindAnyObjectByType<BOKSLevel2Controller>();
            Assert.That(controller, Is.Not.Null);
            controller.TimingScale = 0f;
            controller.RestartLevel2();
        }

        [UnityTest]
        public IEnumerator ForwardForwardPlay_ReachesGoal_AndKeepsBlocksUntilRedraw()
        {
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddForward(), Is.False, "Only two main slots are enabled.");
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.True);
            Assert.That(controller.HeroColumn, Is.EqualTo(3));
            Assert.That(controller.LogicalProgramCount, Is.Zero, "Program data clears on success.");
            Assert.That(controller.VisualCommandCount, Is.EqualTo(2), "Placed commands stay visible until a level redraw.");
        }

        [UnityTest]
        public IEnumerator PaletteDrag_PlacesForwardIntoBothEnabledSlots_AndShowsGhostAndHover()
        {
            BOKSCommandDragSource palette = FindPaletteDrag();
            BOKSProgramDropSlot slotOne = FindDrop("Main Slot 0 - Enabled");
            BOKSProgramDropSlot slotTwo = FindDrop("Main Slot 1 - Enabled");

            palette.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(controller.LogicalProgramCount, Is.Zero, "Palette click must not place a command.");

            Drag(palette, slotOne);
            Assert.That(controller.IsSlotFilled(0), Is.True);
            yield return null;
            Assert.That(GameObject.Find("Forward Drag Ghost"), Is.Null);

            Drag(palette, slotTwo);
            Assert.That(controller.IsSlotFilled(1), Is.True);
            Assert.That(controller.VisualCommandCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PlacedCommandDrag_MovesAndSwapsBetweenEnabledSlots()
        {
            controller.TryPlacePaletteCommand(0);
            BOKSCommandDragSource placedOne = GameObject.Find("Placed Forward Command 1").GetComponent<BOKSCommandDragSource>();
            Drag(placedOne, FindDrop("Main Slot 1 - Enabled"));
            Assert.That(controller.IsSlotFilled(0), Is.False);
            Assert.That(controller.IsSlotFilled(1), Is.True);

            controller.TryPlacePaletteCommand(0);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(2));
            placedOne = GameObject.Find("Placed Forward Command 1").GetComponent<BOKSCommandDragSource>();
            Drag(placedOne, FindDrop("Main Slot 1 - Enabled"));
            Assert.That(controller.IsSlotFilled(0), Is.True);
            Assert.That(controller.IsSlotFilled(1), Is.True, "Two identical Forward commands swap without changing occupancy.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidDrops_CancelPaletteDrag_AndRemovePlacedCommand()
        {
            Drag(FindPaletteDrag(), null);
            Assert.That(controller.LogicalProgramCount, Is.Zero, "Invalid palette drop returns the prototype to the palette.");

            controller.TryPlacePaletteCommand(0);
            BOKSCommandDragSource placed = GameObject.Find("Placed Forward Command 1").GetComponent<BOKSCommandDragSource>();
            Drag(placed, null);
            Assert.That(controller.LogicalProgramCount, Is.Zero, "Placed command dropped outside is removed.");
            Assert.That(controller.VisualCommandCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LockedSlotRejectsDropAndRestoresPlacedCommand()
        {
            controller.TryPlacePaletteCommand(0);
            BOKSCommandDragSource placed = GameObject.Find("Placed Forward Command 1").GetComponent<BOKSCommandDragSource>();
            Drag(placed, FindDrop("Main Slot 2 - Locked"));
            Assert.That(controller.IsSlotFilled(0), Is.True);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OneForwardPlay_ResetsAfterIncompleteRun()
        {
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.False);
            Assert.That(controller.HeroColumn, Is.EqualTo(1));
            Assert.That(controller.LogicalProgramCount, Is.Zero);
            Assert.That(controller.VisualCommandCount, Is.Zero);
            Assert.That(controller.InputLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ProgramCannotBeEditedWhileRunning()
        {
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);
            Assert.That(controller.IsRunning, Is.True);
            Assert.That(controller.TryAddForward(), Is.False);
            Assert.That(controller.TryPlacePaletteCommand(1), Is.False);
            Assert.That(controller.TryMovePlacedCommand(0, 1), Is.False);
            Assert.That(controller.TryRemovePlacedCommand(0), Is.False);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(1));
            controller.RestartLevel2();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelEditor_TestLevel_VisiblePlayRunsAndRestoresDraftProgram()
        {
            SceneManager.LoadScene("BOKS_LevelEditor");
            yield return null;
            yield return null;

            BOKSCampaignAuthoringMode authoring = Object.FindAnyObjectByType<BOKSCampaignAuthoringMode>();
            Assert.That(authoring, Is.Not.Null);
            BOKSCampaignView view = authoring.GetComponent<BOKSCampaignView>();
            controller = view.Gameplay;
            controller.TimingScale = 0f;

            var draft = new BOKSLevelDefinition
            {
                levelNumber = 0,
                number = 0,
                characterId = "boks_green",
                startOri = "right",
                startDirection = "right",
                grid = new BOKSGrid { columns = 6, rows = 6 },
                start = new BOKSGridCell { x = 2, y = 2 },
                goal = new BOKSGridCell { x = 3, y = 2 },
                obstacles = new BOKSGridCell[0],
                mainSlotEnabled = new[] { true, false, false, false, false, false, false, false },
                fnSlotEnabled = new bool[4],
                enabledBlocks = new BOKSEnabledBlocks { forward = true }
            };
            SetPrivateField(authoring, "draft", draft);
            SetPrivateField(authoring, "levelNumber", 0);
            SetPrivateField(authoring, "newLevel", true);
            draft.NormalizeSourceFields();
            view.ApplyLevel(draft);
            Assert.That(controller.TryPlaceCommand(0, BOKSCommandType.Forward), Is.True);

            InvokePrivate(authoring, "TestLevel");
            Assert.That(controller.GetSlotCommand(0), Is.EqualTo(BOKSCommandType.Forward),
                "Test Level must retain the visible program when it reapplies the draft.");

            // Recreate the Level Editor scene as the Edit Mode -> Play Mode transition does.
            // The pending draft and real program must be restored after all runtime Awakes.
            SceneManager.LoadScene("BOKS_LevelEditor");
            yield return null;
            yield return null;
            yield return null;
            authoring = Object.FindAnyObjectByType<BOKSCampaignAuthoringMode>();
            view = authoring.GetComponent<BOKSCampaignView>();
            controller = view.Gameplay;
            controller.TimingScale = 0f;
            Assert.That(controller.HeroColumn, Is.EqualTo(2));
            Assert.That(controller.HeroRow, Is.EqualTo(2));
            Assert.That(controller.GetSlotCommand(0), Is.EqualTo(BOKSCommandType.Forward),
                "The Edit Mode -> Play Mode handoff lost Main slot 1.");

            bool reachedGoal = false;
            controller.LevelCompleted += _ => reachedGoal = true;
            controller.PlayButton.onClick.Invoke();
            int frameLimit = 30;
            while (!reachedGoal && frameLimit-- > 0) yield return null;
            Assert.That(reachedGoal, Is.True, "The visible Play button did not reach the authored goal.");

            yield return null;
            yield return null;
            Assert.That(controller.HeroColumn, Is.EqualTo(2));
            Assert.That(controller.HeroRow, Is.EqualTo(2));
            Assert.That(controller.Facing, Is.EqualTo(BOKSDirection.Right));
            Assert.That(controller.GetSlotCommand(0), Is.EqualTo(BOKSCommandType.Forward));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BOKS_LevelEditor"));

            SessionState.EraseString("BOKS.LevelEditor.TestDraft");
            SessionState.EraseInt("BOKS.LevelEditor.TestLevelNumber");
            SessionState.EraseBool("BOKS.LevelEditor.TestNewLevel");
            SessionState.EraseString("BOKS.LevelEditor.TestProgram");
        }

        static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name + " field was not found.");
            field.SetValue(target, value);
        }

        static void InvokePrivate(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name + " method was not found.");
            method.Invoke(target, null);
        }

        IEnumerator WaitForResolution()
        {
            int frameLimit = 30;
            while (!controller.RunResolved && frameLimit-- > 0) yield return null;
            Assert.That(controller.RunResolved, Is.True, "The zero-duration test run did not resolve.");
        }

        BOKSCommandDragSource FindPaletteDrag()
        {
            foreach (BOKSCommandDragSource drag in Object.FindObjectsByType<BOKSCommandDragSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (drag.IsPaletteSource) return drag;
            Assert.Fail("Palette drag source was not found.");
            return null;
        }

        static BOKSProgramDropSlot FindDrop(string objectName)
        {
            GameObject slot = GameObject.Find(objectName);
            Assert.That(slot, Is.Not.Null, objectName + " was not found.");
            return slot.GetComponent<BOKSProgramDropSlot>();
        }

        static void Drag(BOKSCommandDragSource source, BOKSProgramDropSlot target)
        {
            PointerEventData data = new PointerEventData(EventSystem.current)
            {
                pressPosition = new Vector2(100, 100),
                position = new Vector2(160, 100)
            };
            source.OnBeginDrag(data);
            Assert.That(GameObject.Find("Forward Drag Ghost"), Is.Not.Null, "Drag preview was not created.");
            if (target != null)
            {
                target.OnPointerEnter(data);
                target.OnDrop(data);
            }
            source.OnEndDrag(data);
        }
    }
}
