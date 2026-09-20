#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace BOKS.Demo.Editor
{
    /// <summary>Keeps Unity's generated Android game activity in the device's upright portrait orientation.</summary>
    public sealed class BOKSAndroidManifestOrientationPostprocessor : IPostGenerateGradleAndroidProject
    {
        static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 0;

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

                activity.SetAttributeValue(Android + "screenOrientation", "portrait");
                manifest.Save(manifestPath);
                Debug.Log("[BOKS ANDROID] Generated manifest locked to upright portrait.");
                return;
            }

            throw new InvalidOperationException("Could not find UnityPlayerGameActivity in the generated Android manifest.");
        }
    }
}
#endif
