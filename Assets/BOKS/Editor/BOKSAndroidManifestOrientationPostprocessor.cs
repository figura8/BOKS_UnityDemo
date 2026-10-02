#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace BOKS.Demo.Editor
{
    /// <summary>Do not impose a universal portrait lock that contradicts the managed tablet preference.</summary>
    public sealed class BOKSAndroidManifestOrientationPostprocessor : IPostGenerateGradleAndroidProject
    {
        static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string[] candidates =
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.Combine(Directory.GetParent(path).FullName, "launcher", "src", "main", "AndroidManifest.xml")
            };

            foreach (string manifestPath in candidates.Where(File.Exists))
            {
                XDocument manifest = XDocument.Load(manifestPath);
                XElement activity = manifest.Descendants("activity").FirstOrDefault(element =>
                    (string)element.Attribute(Android + "name") == "com.unity3d.player.UnityPlayerGameActivity");
                if (activity == null) continue;

                // sw600dp resources are NOT reliable here: PackageManager parses this into a fixed
                // ActivityInfo value using a default resource configuration. Keep the manifest neutral.
                // Unity applies the per-device preference at BeforeSplashScreen, once only.
                // No-correction OS startup is still unresolved without an earlier native entry point.
                activity.SetAttributeValue(Android + "screenOrientation", "unspecified");
                manifest.Save(manifestPath);
                Debug.Log("[BOKS ANDROID] Neutral startup declaration; Unity applies the phone/tablet preference before its splash.");
                return;
            }

            throw new InvalidOperationException("Could not find UnityPlayerGameActivity in the generated Android manifest.");
        }

    }
}
#endif
