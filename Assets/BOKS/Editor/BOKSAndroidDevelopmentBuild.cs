#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BOKS.Demo.Editor
{
    /// <summary>Builds the two-scene local Android development APK without changing player content.</summary>
    public static class BOKSAndroidDevelopmentBuild
    {
        const string OutputPath = "Builds/Android/BOKS-Development.apk";
        const string ReleaseOutputPath = "Builds/Android/BOKS-Release.apk";

        [MenuItem("BOKS/Build Android Development APK")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/BOKS_MainMenu.unity",
                    "Assets/Scenes/BOKS_Campaign.unity"
                },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android development APK build failed. See the Unity Console for details.");
        }

        [MenuItem("BOKS/Build Android Release APK")]
        public static void BuildRelease()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReleaseOutputPath));
            ManagedStrippingLevel previousStrippingLevel = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Android);
            try
            {
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Scenes/BOKS_MainMenu.unity",
                        "Assets/Scenes/BOKS_Campaign.unity"
                    },
                    locationPathName = ReleaseOutputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Android release APK build failed. See the Unity Console for details.");
            }
            finally
            {
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, previousStrippingLevel);
            }
        }
    }
}
#endif
