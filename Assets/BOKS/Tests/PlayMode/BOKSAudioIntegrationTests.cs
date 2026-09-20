using System;
using System.Collections;
using System.Collections.Generic;
using BOKS.Demo;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BOKS.Tests
{
    public sealed class BOKSAudioIntegrationTests
    {
        readonly List<BOKSAudioCue> cues = new List<BOKSAudioCue>();
        BOKSLevel2Controller controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadArchivedScene("BOKS_Level02");
            controller = UnityEngine.Object.FindAnyObjectByType<BOKSLevel2Controller>();
            controller.TimingScale = 0f;
            controller.RestartLevel2();
            BOKSAudioManager.Instance.SetSfxEnabled(true);
            BOKSAudioManager.Instance.SfxVolume = 1f;
            cues.Clear();
            BOKSAudioManager.CueTriggered += RecordCue;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BOKSAudioManager.CueTriggered -= RecordCue;
            yield return null;
        }

        void RecordCue(BOKSAudioCue cue) => cues.Add(cue);

        [Test]
        public void CueMap_UsesExportedFilesGainsAndOverlapModes()
        {
            AssertCue(BOKSAudioCue.BlockDetach, "block_detach.ogg", .42f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.BlockDropSuccess, "block_drop_success.mp3", .48f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.SlotHover, "slot_hover.mp3", .22f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.PlayPressed, "play_press_main.mp3", .34f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.ForwardStep, "step_move_02.mp3", .16f, BOKSAudioPlaybackMode.Restart);
            AssertCue(BOKSAudioCue.Turn, "rotation_position_02.mp3", .28f, BOKSAudioPlaybackMode.Restart);
            AssertCue(BOKSAudioCue.BlockedMove, "effort.mp3", .24f, BOKSAudioPlaybackMode.Restart);
            AssertCue(BOKSAudioCue.Failure, "error_action.mp3", .30f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.BoksAnnoyed, "boks_annoyed.ogg", .34f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.GoalBounce, "goal_bubble_bounce.ogg", .28f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.BubblePop, "bubble_pop_main.ogg", .26f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.LevelComplete, "level_complete_main.mp3", .50f, BOKSAudioPlaybackMode.OneShot);
            AssertCue(BOKSAudioCue.Welcome, "wellcome.mp3", .34f, BOKSAudioPlaybackMode.OneShot);

            Assert.That(BOKSAudioManager.Instance.MusicSource, Is.Not.SameAs(BOKSAudioManager.Instance.SfxSource));
            Assert.That(BOKSAudioManager.Instance.MusicIntroSource, Is.Not.SameAs(BOKSAudioManager.Instance.SfxSource));
            Assert.That(BOKSAudioManager.Instance.MusicIntroSource.volume, Is.EqualTo(.72f).Within(.001f));
            Assert.That(BOKSAudioManager.Instance.MusicSource.volume, Is.EqualTo(.18f).Within(.001f));

            cues.Clear();
            foreach (BOKSAudioCue cue in Enum.GetValues(typeof(BOKSAudioCue)))
                BOKSAudioManager.Instance.PlayCue(cue);
            Assert.That(cues.Count, Is.EqualTo(Enum.GetValues(typeof(BOKSAudioCue)).Length),
                "Every mapped source clip must load and emit its semantic cue.");
        }

        [UnityTest]
        public IEnumerator DragStartHoverAndValidDrop_EmitSourceOrder()
        {
            BOKSCommandDragSource palette = FindPalette(BOKSCommandType.Forward);
            BOKSProgramDropSlot slot = GameObject.Find("Main Slot 0 - Enabled").GetComponent<BOKSProgramDropSlot>();
            PointerEventData data = new PointerEventData(EventSystem.current)
            {
                pointerId = -1,
                pressPosition = new Vector2(100, 100),
                position = new Vector2(160, 100)
            };

            palette.OnBeginDrag(data);
            slot.OnPointerEnter(data);
            slot.OnDrop(data);
            palette.OnEndDrag(data);
            yield return null;

            CollectionAssert.AreEqual(new[]
            {
                BOKSAudioCue.BlockDetach,
                BOKSAudioCue.SlotHover,
                BOKSAudioCue.BlockDropSuccess
            }, cues);
        }

        [UnityTest]
        public IEnumerator FailedForward_EmitsPlayStepThenFailureWithoutChangingResetTiming()
        {
            controller.TryAddForward();
            controller.TryPlay();
            yield return WaitForResolution();

            CollectionAssert.AreEqual(new[]
            {
                BOKSAudioCue.PlayPressed,
                BOKSAudioCue.ForwardStep,
                BOKSAudioCue.Failure
            }, cues);
            Assert.That(controller.HeroColumn, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GoalRun_EmitsStepPopAndCompletionSequence()
        {
            controller.TryAddForward();
            controller.TryAddForward();
            controller.TryPlay();
            yield return WaitForResolution();
            yield return null;
            yield return null;

            CollectionAssert.AreEqual(new[]
            {
                BOKSAudioCue.PlayPressed,
                BOKSAudioCue.ForwardStep,
                BOKSAudioCue.ForwardStep,
                BOKSAudioCue.BubblePop,
                BOKSAudioCue.LevelComplete
            }, cues);
            Assert.That(controller.Succeeded, Is.True);
        }

        [UnityTest]
        public IEnumerator BlockedMove_UsesEffortInsteadOfStep_ThenFailure()
        {
            BOKSAudioManager.CueTriggered -= RecordCue;
            yield return LoadArchivedScene("BOKS_Level03");
            controller = UnityEngine.Object.FindAnyObjectByType<BOKSLevel2Controller>();
            controller.TimingScale = 0f;
            controller.RestartLevel2();
            cues.Clear();
            BOKSAudioManager.CueTriggered += RecordCue;

            controller.TryAddForward();
            controller.TryPlay();
            yield return WaitForResolution();

            CollectionAssert.AreEqual(new[]
            {
                BOKSAudioCue.PlayPressed,
                BOKSAudioCue.BlockedMove,
                BOKSAudioCue.Failure
            }, cues);
        }

        [UnityTest]
        public IEnumerator LeftAndRightPrograms_UseTheSharedTurnCueBeforeMovement()
        {
            yield return AssertTurnSequence("BOKS_Level03", BOKSCommandType.Right);
            yield return AssertTurnSequence("BOKS_Level04", BOKSCommandType.Left);
        }

        [UnityTest]
        public IEnumerator ManagerAndSingleListener_PersistAcrossSceneChange()
        {
            BOKSAudioManager manager = BOKSAudioManager.Instance;
            Assert.That(ActiveListenerCount(), Is.EqualTo(1));

            yield return LoadArchivedScene("BOKS_Level03");

            Assert.That(BOKSAudioManager.Instance, Is.SameAs(manager));
            Assert.That(BOKSAudioManager.Instance.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(ActiveListenerCount(), Is.EqualTo(1));
        }

        IEnumerator AssertTurnSequence(string scene, BOKSCommandType turn)
        {
            BOKSAudioManager.CueTriggered -= RecordCue;
            yield return LoadArchivedScene(scene);
            controller = UnityEngine.Object.FindAnyObjectByType<BOKSLevel2Controller>();
            controller.TimingScale = 0f;
            controller.RestartLevel2();
            cues.Clear();
            BOKSAudioManager.CueTriggered += RecordCue;

            controller.TryAddCommand(turn);
            controller.TryAddForward();
            controller.TryAddForward();
            controller.TryPlay();
            yield return WaitForResolution();
            yield return null;
            yield return null;

            Assert.That(cues[0], Is.EqualTo(BOKSAudioCue.PlayPressed));
            Assert.That(cues[1], Is.EqualTo(BOKSAudioCue.Turn));
            Assert.That(cues[2], Is.EqualTo(BOKSAudioCue.ForwardStep));
        }

        IEnumerator WaitForResolution()
        {
            int frameLimit = 40;
            while (!controller.RunResolved && frameLimit-- > 0) yield return null;
            Assert.That(controller.RunResolved, Is.True);
        }

        static IEnumerator LoadArchivedScene(string sceneName)
        {
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Archive/" + sceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        static BOKSCommandDragSource FindPalette(BOKSCommandType command)
        {
            foreach (BOKSCommandDragSource source in UnityEngine.Object.FindObjectsByType<BOKSCommandDragSource>(FindObjectsInactive.Include))
                if (source.IsPaletteSource && source.CommandType == command) return source;
            throw new InvalidOperationException(command + " palette source was not found.");
        }

        static void AssertCue(BOKSAudioCue cue, string file, float gain, BOKSAudioPlaybackMode mode)
        {
            Assert.That(BOKSAudioManager.GetSourceFile(cue), Is.EqualTo(file));
            Assert.That(BOKSAudioManager.GetSourceGain(cue), Is.EqualTo(gain).Within(.001f));
            Assert.That(BOKSAudioManager.GetPlaybackMode(cue), Is.EqualTo(mode));
        }

        static int ActiveListenerCount()
        {
            int count = 0;
            foreach (AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (listener.enabled && listener.gameObject.activeInHierarchy) count++;
            return count;
        }
    }
}
