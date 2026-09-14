using System;
using System.Collections;
using BOKS.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BOKS.Tests
{
    /// <summary>
    /// End-to-end campaign smoke test: drives the shared campaign scene from Level 1 to Level 10,
    /// verifies automatic progression (1→2, 5→6 and 9→10 included) and confirms Level 10 completes
    /// the campaign instead of advancing to a Level 11.
    /// Solutions are derived from data/editor-levels.json with the exact command semantics of
    /// <see cref="BOKSLevel2Controller"/> (Function expands over the function slots at indices 8-11).
    /// </summary>
    public sealed class BOKSCampaignSmokeTests
    {
        BOKSCampaignController campaign;
        BOKSLevel2Controller controller;
        BOKSLevelTransition transition;

        static readonly (BOKSCommandType[] main, BOKSCommandType[] fn)[] Solutions =
        {
            default,
            (new[] { BOKSCommandType.Forward }, new BOKSCommandType[0]),                                                                         // L1
            (new[] { BOKSCommandType.Forward, BOKSCommandType.Forward }, new BOKSCommandType[0]),                                                // L2
            (new[] { BOKSCommandType.Right, BOKSCommandType.Forward, BOKSCommandType.Forward }, new BOKSCommandType[0]),                         // L3
            (new[] { BOKSCommandType.Left, BOKSCommandType.Forward, BOKSCommandType.Forward }, new BOKSCommandType[0]),                          // L4
            (new[] { BOKSCommandType.Right, BOKSCommandType.Forward, BOKSCommandType.Left, BOKSCommandType.Forward }, new BOKSCommandType[0]),   // L5
            (new[] { BOKSCommandType.Function, BOKSCommandType.Function }, new[] { BOKSCommandType.Forward, BOKSCommandType.Forward }),         // L6
            (new[] { BOKSCommandType.Right, BOKSCommandType.Function }, new[] { BOKSCommandType.Forward, BOKSCommandType.Forward }),            // L7
            (new[] { BOKSCommandType.Left, BOKSCommandType.Function }, new[] { BOKSCommandType.Forward, BOKSCommandType.Forward }),             // L8
            (new[] { BOKSCommandType.Right, BOKSCommandType.Forward, BOKSCommandType.Function, BOKSCommandType.Function },
                new[] { BOKSCommandType.Forward, BOKSCommandType.Forward, BOKSCommandType.Left }),                                             // L9
            (new[] { BOKSCommandType.Right, BOKSCommandType.Function, BOKSCommandType.Forward, BOKSCommandType.Function },
                new[] { BOKSCommandType.Forward, BOKSCommandType.Forward, BOKSCommandType.Left }),                                             // L10
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("BOKS_Campaign");
            yield return null;
            yield return null;

            campaign = UnityEngine.Object.FindAnyObjectByType<BOKSCampaignController>();
            Assert.That(campaign, Is.Not.Null, "BOKSCampaignController missing from BOKS_Campaign.");
            controller = campaign.Gameplay;
            Assert.That(controller, Is.Not.Null, "Campaign has no gameplay controller.");
            transition = UnityEngine.Object.FindAnyObjectByType<BOKSLevelTransition>();
            Assert.That(transition, Is.Not.Null, "BOKSLevelTransition missing from BOKS_Campaign.");

            controller.TimingScale = 0f;
            transition.TimingScale = 0f;
        }

        [UnityTest]
        public IEnumerator Campaign_Levels1To10_ProgressesAndCompletesWithoutLevel11()
        {
            Assert.That(campaign.CurrentLevel, Is.EqualTo(BOKSCampaignController.FirstLevel));

            for (int level = 1; level <= 10; level++)
            {
                Assert.That(campaign.CurrentLevel, Is.EqualTo(level), "Expected to start at level " + level + ".");

                (BOKSCommandType[] main, BOKSCommandType[] fn) = Solutions[level];
                PlaceSolution(main, fn);

                Assert.That(controller.TryPlay(), Is.True, "Play should start at level " + level + ".");

                if (level < 10)
                    yield return WaitUntil(
                        () => campaign.CurrentLevel == level + 1,
                        "Level " + level + " did not advance to " + (level + 1) + " (stuck at " + campaign.CurrentLevel + ").");
                else
                    yield return WaitUntil(() => campaign.CampaignComplete, "Level 10 did not complete the campaign.");
            }

            Assert.That(campaign.CampaignComplete, Is.True);
            Assert.That(campaign.CurrentLevel, Is.EqualTo(BOKSCampaignController.FinalLevel),
                "Level 10 must not continue to a level 11.");
        }

        [UnityTest]
        public IEnumerator Level6_FunctionBlock_IsRejectedByFunctionSlots()
        {
            Assert.That(campaign.ApplyLevel(6), Is.True, "Function level 6 should load.");
            yield return null;

            // Drag/drop funnels through these controller calls, so they carry the slot rules:
            // main slots (0-7) accept Function, function slots (8-11) accept Forward/Left/Right only.
            Assert.That(controller.TryPlaceCommand(0, BOKSCommandType.Function), Is.True, "Main slots accept Function.");
            Assert.That(controller.TryPlaceCommand(8, BOKSCommandType.Function), Is.False, "Function slots reject the Function block.");
            Assert.That(controller.IsSlotFilled(8), Is.False, "The rejected Function must not occupy the function slot.");
            Assert.That(controller.TryPlaceCommand(8, BOKSCommandType.Forward), Is.True, "Function slots still accept Forward.");
            Assert.That(controller.TryMovePlacedCommand(0, 8), Is.False, "A placed Function cannot be dragged into a function slot.");
            Assert.That(controller.IsSlotFilled(0), Is.True, "The rejected move leaves the Function in its main slot.");
        }

        [UnityTest]
        public IEnumerator MicroAnimations_EyesBubbleIdleAndRebuke_AreWiredAndGatedByRuns()
        {
            // Blink overlay: built from the source SVG eye geometry of the current facing sprite.
            Assert.That(controller.HeroEyeOverlayVisible, Is.True, "The hero sprite has no eye overlay.");
            Assert.That(controller.HeroEyesSquinting, Is.False, "The eyes start open.");

            // Goal bubble idle lives on the goal root and cached its per-level rest pose.
            BOKSGoalBubbleIdle idle = UnityEngine.Object.FindAnyObjectByType<BOKSGoalBubbleIdle>();
            Assert.That(idle, Is.Not.Null, "The goal bubble idle is missing from BOKS_Campaign.");
            Assert.That(idle.RestPoseCaptured, Is.True, "The goal bubble idle did not cache its rest pose.");

            // While a run owns the input, taps are ignored (source: running || animating).
            controller.SetCampaignInputLocked(true);
            Assert.That(controller.TriggerHeroRebuke(), Is.False, "Taps must be ignored while a run owns the input.");
            controller.SetCampaignInputLocked(false);

            // Touch rebuke: shake/squash plus the annoyed cue, with the reaction squint override.
            Assert.That(controller.TriggerHeroRebuke(), Is.True, "A tap on the hero should rebuke.");
            Assert.That(controller.HeroRebuking, Is.True, "The 560 ms rebuke shake should be playing.");
            Assert.That(controller.HeroEyesSquinting, Is.True, "The rebuke squint must override the blink.");
            Assert.That(controller.TriggerHeroRebuke(), Is.False, "The 950 ms cooldown must reject a second tap.");

            yield return null;
            Assert.That(controller.HeroRebuking, Is.True, "The rebuke shake is still running one frame later.");
        }

        void PlaceSolution(BOKSCommandType[] main, BOKSCommandType[] fn)
        {
            for (int i = 0; i < main.Length; i++)
                Assert.That(controller.TryPlaceCommand(i, main[i]), Is.True,
                    "Main slot " + i + " rejected " + main[i] + " at level " + campaign.CurrentLevel + ".");
            for (int i = 0; i < fn.Length; i++)
                Assert.That(controller.TryPlaceCommand(8 + i, fn[i]), Is.True,
                    "Function slot " + i + " rejected " + fn[i] + " at level " + campaign.CurrentLevel + ".");
        }

        IEnumerator WaitUntil(Func<bool> condition, string failureMessage)
        {
            int frameLimit = 600;
            while (frameLimit-- > 0)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail(failureMessage);
        }
    }
}
