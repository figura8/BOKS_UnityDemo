using System.Collections;
using System.Reflection;
using BOKS.Demo;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BOKS.Tests
{
    public sealed class BOKSCommandDragOwnershipTests
    {
        BOKSLevel2Controller controller;
        BOKSCommandDragSource palette;
        BOKSProgramDropSlot slot;
        PointerEventData owner;
        PointerEventData other;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Archive/BOKS_Level02.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            controller = Object.FindAnyObjectByType<BOKSLevel2Controller>();
            controller.RestartLevel2();
            foreach (var source in Object.FindObjectsByType<BOKSCommandDragSource>(FindObjectsInactive.Include))
                if (source.IsPaletteSource && source.CommandType == BOKSCommandType.Forward) palette = source;
            slot = GameObject.Find("Main Slot 0 - Enabled").GetComponent<BOKSProgramDropSlot>();
            owner = Pointer(101);
            other = Pointer(202);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            controller.RestartLevel2();
            yield return null;
            Assert.That(GameObject.Find("Forward Drag Layer"), Is.Null);
        }

        static PointerEventData Pointer(int id) => new PointerEventData(EventSystem.current)
        {
            pointerId = id, pressPosition = new Vector2(100, 100), position = new Vector2(160, 100)
        };

        static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        void AssertCancelled(BOKSCommandDragSource source)
        {
            Assert.That(controller.OwnsCommandDrag(source), Is.False);
            Assert.That(Field<bool>(source, "dragging"), Is.False);
            Assert.That(Field<GameObject>(source, "ghostLayer"), Is.Null);
            Assert.That(Field<int>(controller, "pendingDropSlot"), Is.EqualTo(-1));
            Assert.That(Field<bool>(controller, "pendingDropLocked"), Is.False);
            Assert.That(Field<int>(controller, "hoveredSlot"), Is.EqualTo(-1));
        }

        [Test]
        public void SecondFingerOnSameSource_CannotReplaceGhostOrOwner()
        {
            palette.OnBeginDrag(owner);
            var ghost = Field<GameObject>(palette, "ghostLayer");
            palette.OnBeginDrag(other);
            palette.OnEndDrag(other);
            Assert.That(controller.OwnsCommandDrag(palette, owner.pointerId), Is.True);
            Assert.That(Field<GameObject>(palette, "ghostLayer"), Is.SameAs(ghost));
            slot.OnDrop(owner);
            palette.OnEndDrag(owner);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(1));
            AssertCancelled(palette);
        }

        [Test]
        public void SecondFingerOnAnotherSource_CannotReplaceOwner()
        {
            controller.TryAddForward();
            var placed = GameObject.Find("Main Slot 0 - Enabled").GetComponentInChildren<BOKSCommandDragSource>();
            Assert.That(placed, Is.Not.Null);
            palette.OnBeginDrag(owner);
            placed.OnBeginDrag(other);
            placed.OnEndDrag(other);
            Assert.That(controller.OwnsCommandDrag(palette, owner.pointerId), Is.True);
            Assert.That(Field<GameObject>(placed, "ghostLayer"), Is.Null);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongPointerDragHoverDropAndEnd_AreIgnored()
        {
            palette.OnBeginDrag(owner);
            var ghost = Field<RectTransform>(palette, "ghost");
            var position = ghost.anchoredPosition;
            other.position = new Vector2(300, 300);
            palette.OnDrag(other);
            slot.OnPointerEnter(other);
            slot.OnDrop(other);
            palette.OnEndDrag(other);
            Assert.That(ghost.anchoredPosition, Is.EqualTo(position));
            Assert.That(Field<int>(controller, "pendingDropSlot"), Is.EqualTo(-1));
            Assert.That(Field<int>(controller, "hoveredSlot"), Is.EqualTo(-1));
            slot.OnPointerEnter(owner);
            slot.OnPointerExit(other);
            Assert.That(Field<int>(controller, "hoveredSlot"), Is.EqualTo(0));
            Assert.That(controller.OwnsCommandDrag(palette, owner.pointerId), Is.True);
        }

        [TestCase("OnApplicationPause", true)]
        [TestCase("OnApplicationFocus", false)]
        public void LifecycleInterruption_CancelsWithoutEditingProgram(string message, bool value)
        {
            controller.TryAddForward();
            var placed = GameObject.Find("Main Slot 0 - Enabled").GetComponentInChildren<BOKSCommandDragSource>();
            var image = placed.GetComponent<Image>();
            var originalColor = image.color;
            placed.OnBeginDrag(owner);
            slot.OnPointerEnter(owner);
            slot.OnDrop(owner);
            controller.SendMessage(message, value);
            AssertCancelled(placed);
            placed.OnEndDrag(owner);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(1));
            Assert.That(image.color, Is.EqualTo(originalColor));
            Assert.That(controller.InputLocked, Is.False);
            controller.SendMessage(message, !value);
            palette.OnBeginDrag(other);
            Assert.That(controller.OwnsCommandDrag(palette, other.pointerId), Is.True);
        }

        [Test]
        public void Play_CancelsBeforeStartingProgram()
        {
            controller.TryAddForward();
            palette.OnBeginDrag(owner);
            Assert.That(controller.TryPlay(), Is.True);
            AssertCancelled(palette);
            Assert.That(controller.IsRunning, Is.True);
        }

        [Test]
        public void Reset_CancelsGhostAndOwnership()
        {
            palette.OnBeginDrag(owner);
            slot.OnPointerEnter(owner);
            slot.OnDrop(owner);
            controller.RestartLevel2();
            AssertCancelled(palette);
            palette.OnEndDrag(owner);
            Assert.That(controller.LogicalProgramCount, Is.Zero);
        }

        [Test]
        public void SourceDisabled_CancelsWithoutDeletingCommand()
        {
            controller.TryAddForward();
            var placed = GameObject.Find("Main Slot 0 - Enabled").GetComponentInChildren<BOKSCommandDragSource>();
            placed.OnBeginDrag(owner);
            placed.enabled = false;
            AssertCancelled(placed);
            Assert.That(controller.LogicalProgramCount, Is.EqualTo(1));
        }

        [Test]
        public void SourceDestroyed_CancelsOwnership()
        {
            palette.OnBeginDrag(owner);
            Object.DestroyImmediate(palette);
            Assert.That(Field<BOKSCommandDragSource>(controller, "activeDrag"), Is.Null);
            Assert.That(GameObject.Find("Forward Drag Layer"), Is.Null);
        }

        [Test]
        public void RepeatedCancellation_IsSafeAndStaleEventsCannotStealNewDrag()
        {
            palette.OnBeginDrag(owner);
            controller.CancelCommandDrag();
            controller.CancelCommandDrag();
            AssertCancelled(palette);
            palette.OnBeginDrag(other);
            palette.OnEndDrag(owner);
            slot.OnDrop(owner);
            Assert.That(controller.OwnsCommandDrag(palette, other.pointerId), Is.True);
            Assert.That(Field<int>(controller, "pendingDropSlot"), Is.EqualTo(-1));
        }
    }
}
