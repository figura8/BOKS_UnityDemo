using BOKS.Demo;
using NUnit.Framework;
using UnityEngine;

namespace BOKS.Tests
{
    public sealed class BOKSDeviceLayoutTests
    {
        BOKSDeviceLayout.TestOverride previous;

        [SetUp]
        public void SetUp()
        {
            previous = BOKSDeviceLayout.LayoutTestOverride;
            BOKSDeviceLayout.SetLayoutTestOverride(BOKSDeviceLayout.TestOverride.Auto);
        }

        [TearDown]
        public void TearDown() => BOKSDeviceLayout.SetLayoutTestOverride(previous);

        [TestCase(520, 1000, BOKSLayoutComposition.Portrait)]
        [TestCase(1280, 800, BOKSLayoutComposition.Landscape)]
        [TestCase(1000, 1000, BOKSLayoutComposition.Portrait)]
        [TestCase(1199, 1000, BOKSLayoutComposition.Portrait)]
        [TestCase(1200, 1000, BOKSLayoutComposition.Landscape)]
        [TestCase(0, 0, BOKSLayoutComposition.Portrait)]
        public void CompositionUsesSafeArea(int width, int height, BOKSLayoutComposition expected)
            => Assert.That(BOKSDeviceLayout.SelectComposition(new Rect(37, 61, width, height)), Is.EqualTo(expected));

        [TestCase("iPhone14,5", BOKSDeviceClass.Phone)]
        [TestCase("iPad14,5", BOKSDeviceClass.Tablet)]
        public void IOSClassificationDoesNotDependOnViewport(string model, BOKSDeviceClass expected)
        {
            Assert.That(BOKSDeviceLayout.ClassifyDevice(RuntimePlatform.IPhonePlayer, model, 1600, 720, 0,
                out _, out _), Is.EqualTo(expected));
        }

        [TestCase(599, BOKSDeviceClass.Phone)]
        [TestCase(600, BOKSDeviceClass.Tablet)]
        public void AndroidConfigurationTakesPrecedenceOverDpi(int smallestWidthDp, BOKSDeviceClass expected)
        {
            Assert.That(BOKSDeviceLayout.ClassifyDevice(RuntimePlatform.Android, "test", 2000, 3000, 100,
                out _, out float reportedDp, smallestWidthDp), Is.EqualTo(expected));
            Assert.That(reportedDp, Is.EqualTo(smallestWidthDp));
        }

        [Test]
        public void ProductPreferenceIsExplicit()
        {
            Assert.That(BOKSDeviceLayout.PreferenceFor(BOKSDeviceClass.Phone), Is.EqualTo(BOKSOrientationPreference.UprightPortrait));
            Assert.That(BOKSDeviceLayout.PreferenceFor(BOKSDeviceClass.Tablet), Is.EqualTo(BOKSOrientationPreference.LandscapeEither));
        }

        [TestCase(BOKSDeviceLayout.TestOverride.ForcePhonePortrait, BOKSLayoutComposition.Portrait)]
        [TestCase(BOKSDeviceLayout.TestOverride.ForceTabletLandscape, BOKSLayoutComposition.Landscape)]
        public void OverrideDoesNotChangeDeviceOrRequestedOrientation(BOKSDeviceLayout.TestOverride value, BOKSLayoutComposition expected)
        {
            var device = BOKSDeviceLayout.DeviceClass;
            var requested = BOKSDeviceLayout.RequestedOrientation;
            BOKSDeviceLayout.SetLayoutTestOverride(value);
            Assert.That(BOKSDeviceLayout.SelectComposition(new Rect(0, 0, 900, 900)), Is.EqualTo(expected));
            Assert.That(BOKSDeviceLayout.DeviceClass, Is.EqualTo(device));
            Assert.That(BOKSDeviceLayout.RequestedOrientation, Is.EqualTo(requested));
        }
    }
}
