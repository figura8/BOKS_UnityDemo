using System.Collections;
using BOKS.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BOKS.Tests
{
    /// <summary>
    /// Campaign Levels 3-5: turns, obstacle blocking and per-level valid solutions.
    /// Level 2 regression coverage lives in <see cref="BOKSLevel2ControllerTests"/>.
    /// </summary>
    public sealed class BOKSCampaignTurnsTests
    {
        BOKSLevel2Controller controller;

        IEnumerator Load(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return null;
            controller = Object.FindAnyObjectByType<BOKSLevel2Controller>();
            Assert.That(controller, Is.Not.Null);
            controller.TimingScale = 0f;
            controller.RestartLevel2();
        }

        IEnumerator WaitForResolution()
        {
            int frameLimit = 30;
            while (!controller.RunResolved && frameLimit-- > 0) yield return null;
            Assert.That(controller.RunResolved, Is.True, "The zero-duration test run did not resolve.");
        }

        [UnityTest]
        public IEnumerator Level3_ValidSolution_ReachesGoal()
        {
            yield return Load("BOKS_Level03");
            Assert.That(controller.TryAddRight(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.True);
            Assert.That(controller.HeroColumn, Is.EqualTo(3));
            Assert.That(controller.HeroRow, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Level3_ObstacleBlock_ForwardDoesNotReachGoal()
        {
            yield return Load("BOKS_Level03");
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.False);
            Assert.That(controller.HeroColumn, Is.EqualTo(1), "Blocked forward must not move into the obstacle.");
            Assert.That(controller.HeroRow, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator RightTurn_ChangesFacingClockwise()
        {
            yield return Load("BOKS_Level03");
            Assert.That(controller.TryAddRight(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.True);
            Assert.That(controller.Facing, Is.EqualTo(BOKSDirection.Right));
        }

        [UnityTest]
        public IEnumerator Level4_ValidSolution_ReachesGoal()
        {
            yield return Load("BOKS_Level04");
            Assert.That(controller.TryAddLeft(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.True);
            Assert.That(controller.HeroColumn, Is.EqualTo(2));
            Assert.That(controller.HeroRow, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator LeftTurn_ChangesFacingCounterClockwise()
        {
            yield return Load("BOKS_Level04");
            Assert.That(controller.TryAddLeft(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.True);
            Assert.That(controller.Facing, Is.EqualTo(BOKSDirection.Left));
        }

        [UnityTest]
        public IEnumerator Level5_ValidSolution_ReachesGoal()
        {
            yield return Load("BOKS_Level05");
            Assert.That(controller.TryAddRight(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryAddLeft(), Is.True);
            Assert.That(controller.TryAddForward(), Is.True);
            Assert.That(controller.TryPlay(), Is.True);

            yield return WaitForResolution();

            Assert.That(controller.Succeeded, Is.True);
            Assert.That(controller.HeroColumn, Is.EqualTo(2));
            Assert.That(controller.HeroRow, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Turns_KeepRootCentred_And_SwapCorrectDirectionalArt()
        {
            yield return Load("BOKS_Level05"); // green character, starts facing Up

            Vector2 start = controller.HeroRootAnchoredPosition;

            // Four consecutive Right turns cover: Up->Right, Right->Down, Down->Left, Left->Up.
            BOKSDirection[] rightCycle = { BOKSDirection.Right, BOKSDirection.Down, BOKSDirection.Left, BOKSDirection.Up };
            for (int i = 0; i < 4; i++)
            {
                controller.TurnImmediate(BOKSCommandType.Right);
                Assert.That(controller.HeroRootAnchoredPosition, Is.EqualTo(start), "Root moved during a right turn.");
                Assert.That(controller.Facing, Is.EqualTo(rightCycle[i]));
                Assert.That(controller.HeroSpriteName, Is.EqualTo(SpriteName(rightCycle[i])));
            }

            // Four consecutive Left turns cover: Up->Left, Left->Down, Down->Right, Right->Up.
            BOKSDirection[] leftCycle = { BOKSDirection.Left, BOKSDirection.Down, BOKSDirection.Right, BOKSDirection.Up };
            for (int i = 0; i < 4; i++)
            {
                controller.TurnImmediate(BOKSCommandType.Left);
                Assert.That(controller.HeroRootAnchoredPosition, Is.EqualTo(start), "Root moved during a left turn.");
                Assert.That(controller.Facing, Is.EqualTo(leftCycle[i]));
                Assert.That(controller.HeroSpriteName, Is.EqualTo(SpriteName(leftCycle[i])));
            }

            Assert.That(controller.HeroRootAnchoredPosition, Is.EqualTo(start));
            Assert.That(controller.Facing, Is.EqualTo(BOKSDirection.Up));
        }

        [UnityTest]
        public IEnumerator DragLeft_StoresLeft_ShowsLeft_AndReaddsAsLeft()
        {
            yield return Load("BOKS_Level05");

            Drag(FindPaletteDrag(BOKSCommandType.Left), FindDrop(0));
            AssertSlot(0, BOKSCommandType.Left, "turn-left");

            Drag(FindPlacedDrag(0), null);
            Assert.That(controller.GetSlotCommand(0), Is.EqualTo(BOKSCommandType.None));

            Drag(FindPaletteDrag(BOKSCommandType.Left), FindDrop(0));
            AssertSlot(0, BOKSCommandType.Left, "turn-left");
        }

        [UnityTest]
        public IEnumerator DragRight_StoresRight_AndShowsRight()
        {
            yield return Load("BOKS_Level05");

            Drag(FindPaletteDrag(BOKSCommandType.Right), FindDrop(0));
            AssertSlot(0, BOKSCommandType.Right, "turn-right");
        }

        [UnityTest]
        public IEnumerator MoveLeftBetweenSlots_PreservesLeftTypeAndVisual()
        {
            yield return Load("BOKS_Level05");

            Drag(FindPaletteDrag(BOKSCommandType.Left), FindDrop(0));
            Drag(FindPlacedDrag(0), FindDrop(1));

            Assert.That(controller.GetSlotCommand(0), Is.EqualTo(BOKSCommandType.None));
            AssertSlot(1, BOKSCommandType.Left, "turn-left");
        }

        [UnityTest]
        public IEnumerator SwapLeftAndRight_PreservesBothTypesAndVisuals()
        {
            yield return Load("BOKS_Level05");

            Drag(FindPaletteDrag(BOKSCommandType.Left), FindDrop(0));
            Drag(FindPaletteDrag(BOKSCommandType.Right), FindDrop(1));
            Drag(FindPlacedDrag(0), FindDrop(1));

            AssertSlot(0, BOKSCommandType.Right, "turn-right");
            AssertSlot(1, BOKSCommandType.Left, "turn-left");
        }

        [UnityTest]
        public IEnumerator AllEightTurns_StartInCurrentVisualPose_ThenSettleAtDestination()
        {
            yield return Load("BOKS_Level05");
            controller.TimingScale = .15f;
            RectTransform visual = GameObject.Find("BOKS Visual").GetComponent<RectTransform>();

            (BOKSDirection start, BOKSCommandType command, BOKSDirection destination, float startAngle)[] transitions =
            {
                (BOKSDirection.Up, BOKSCommandType.Left, BOKSDirection.Left, -90f),
                (BOKSDirection.Up, BOKSCommandType.Right, BOKSDirection.Right, 90f),
                (BOKSDirection.Right, BOKSCommandType.Left, BOKSDirection.Up, -90f),
                (BOKSDirection.Right, BOKSCommandType.Right, BOKSDirection.Down, 90f),
                (BOKSDirection.Down, BOKSCommandType.Left, BOKSDirection.Right, -90f),
                (BOKSDirection.Down, BOKSCommandType.Right, BOKSDirection.Left, 90f),
                (BOKSDirection.Left, BOKSCommandType.Left, BOKSDirection.Down, -90f),
                (BOKSDirection.Left, BOKSCommandType.Right, BOKSDirection.Up, 90f)
            };

            foreach (var transition in transitions)
            {
                controller.RestartLevel2();
                SetStartFacing(transition.start);
                Vector2 rootStart = controller.HeroRootAnchoredPosition;
                string currentSprite = SpriteName(transition.start);

                Assert.That(controller.HeroSpriteName, Is.EqualTo(currentSprite));
                Assert.That(controller.TryAddCommand(transition.command), Is.True);
                Assert.That(controller.TryPlay(), Is.True);
                Assert.That(controller.HeroSpriteName, Is.EqualTo(currentSprite),
                    "Pressing Play changed the sprite before the turn began.");

                int frameLimit = 120;
                while (Mathf.Abs(visual.localScale.x - .97f) > .001f && frameLimit-- > 0)
                    yield return null;
                Assert.That(frameLimit, Is.GreaterThan(0), "The first turn keyframe was not observed.");
                Assert.That(controller.Facing, Is.EqualTo(transition.start), "Logical facing changed before settle.");
                Assert.That(controller.HeroSpriteName, Is.EqualTo(SpriteName(transition.destination)));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(visual.localEulerAngles.z, transition.startAngle)), Is.LessThan(.01f),
                    $"{transition.start} -> {transition.destination} did not begin in the current visual pose.");
                Assert.That(controller.HeroRootAnchoredPosition, Is.EqualTo(rootStart));

                frameLimit = 120;
                while (controller.Facing != transition.destination && frameLimit-- > 0)
                    yield return null;
                Assert.That(frameLimit, Is.GreaterThan(0), "Turn did not settle at its destination.");
                Assert.That(controller.HeroSpriteName, Is.EqualTo(SpriteName(transition.destination)));
                Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.identity), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(visual.localScale, Vector3.one), Is.LessThan(.001f));
                Assert.That(controller.HeroRootAnchoredPosition, Is.EqualTo(rootStart));
            }
        }

        void SetStartFacing(BOKSDirection target)
        {
            while (controller.Facing != target)
                controller.TurnImmediate(BOKSCommandType.Right);
        }

        void AssertSlot(int slot, BOKSCommandType command, string spriteName)
        {
            BOKSCommandDragSource placed = FindPlacedDrag(slot);
            Assert.That(controller.GetSlotCommand(slot), Is.EqualTo(command));
            Assert.That(placed.CommandType, Is.EqualTo(command));
            Assert.That(placed.GetComponent<Image>().sprite.name, Is.EqualTo(spriteName));
        }

        static BOKSCommandDragSource FindPaletteDrag(BOKSCommandType command)
        {
            foreach (BOKSCommandDragSource drag in Object.FindObjectsByType<BOKSCommandDragSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (drag.IsPaletteSource && drag.CommandType == command) return drag;
            Assert.Fail(command + " palette drag source was not found.");
            return null;
        }

        static BOKSCommandDragSource FindPlacedDrag(int slot)
        {
            foreach (BOKSCommandDragSource drag in Object.FindObjectsByType<BOKSCommandDragSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!drag.IsPaletteSource && drag.SlotIndex == slot) return drag;
            Assert.Fail("Placed drag source for slot " + slot + " was not found.");
            return null;
        }

        static BOKSProgramDropSlot FindDrop(int slot)
        {
            GameObject drop = GameObject.Find("Main Slot " + slot + " - Enabled");
            Assert.That(drop, Is.Not.Null);
            return drop.GetComponent<BOKSProgramDropSlot>();
        }

        static void Drag(BOKSCommandDragSource source, BOKSProgramDropSlot target)
        {
            PointerEventData data = new PointerEventData(EventSystem.current)
            {
                pressPosition = new Vector2(100, 100),
                position = new Vector2(160, 100)
            };
            source.OnBeginDrag(data);
            if (target != null) target.OnDrop(data);
            source.OnEndDrag(data);
        }

        static string SpriteName(BOKSDirection direction)
        {
            string dir = direction switch
            {
                BOKSDirection.Up => "up",
                BOKSDirection.Left => "left",
                BOKSDirection.Down => "down",
                _ => "right"
            };
            return "character-green-" + dir;
        }
    }
}
