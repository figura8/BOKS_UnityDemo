#if UNITY_EDITOR
using BOKS.Demo;
using UnityEditor;

namespace BOKS.Editor
{
    [InitializeOnLoad]
    static class BOKSLayoutTestMenu
    {
        const string AutoPath = "BÖKS/Layout Test/Auto";
        const string PortraitPath = "BÖKS/Layout Test/Force Phone Portrait";
        const string LandscapePath = "BÖKS/Layout Test/Force Tablet Landscape";

        static BOKSLayoutTestMenu()
        {
            EditorApplication.delayCall += UpdateChecks;
            EditorApplication.playModeStateChanged += _ => UpdateChecks();
        }

        [MenuItem(AutoPath, false, 100)]
        static void Auto() => Select(BOKSDeviceLayout.TestOverride.Auto);

        [MenuItem(PortraitPath, false, 101)]
        static void Portrait() => Select(BOKSDeviceLayout.TestOverride.ForcePhonePortrait);

        [MenuItem(LandscapePath, false, 102)]
        static void Landscape() => Select(BOKSDeviceLayout.TestOverride.ForceTabletLandscape);

        static void Select(BOKSDeviceLayout.TestOverride value)
        {
            // This API already refreshes all existing campaign responsive layouts.
            BOKSDeviceLayout.SetLayoutTestOverride(value);
            UpdateChecks();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        [MenuItem(AutoPath, true)]
        [MenuItem(PortraitPath, true)]
        [MenuItem(LandscapePath, true)]
        static bool Validate()
        {
            UpdateChecks();
            return true;
        }

        static void UpdateChecks()
        {
            var active = BOKSDeviceLayout.LayoutTestOverride;
            Menu.SetChecked(AutoPath, active == BOKSDeviceLayout.TestOverride.Auto);
            Menu.SetChecked(PortraitPath, active == BOKSDeviceLayout.TestOverride.ForcePhonePortrait);
            Menu.SetChecked(LandscapePath, active == BOKSDeviceLayout.TestOverride.ForceTabletLandscape);
        }
    }
}
#endif
