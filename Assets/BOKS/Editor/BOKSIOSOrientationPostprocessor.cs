#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BOKS.Demo.Editor
{
    /// <summary>Editor/build-only universal-app declarations. No native plugin or Xcode assembly required.</summary>
    public sealed class BOKSIOSOrientationPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.iOS)
                ApplyToPlist(Path.Combine(report.summary.outputPath, "Info.plist"));
        }

        public static void ApplyToPlist(string path)
        {
            var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore, XmlResolver = null };
            XDocument plist;
            using (var reader = System.Xml.XmlReader.Create(path, settings)) plist = XDocument.Load(reader);
            XElement dict = plist.Root?.Element("dict");
            if (dict == null) throw new InvalidOperationException("Expected an XML Info.plist root dictionary: " + path);
            SetOrientations(dict, "UISupportedInterfaceOrientations", "UIInterfaceOrientationPortrait");
            SetOrientations(dict, "UISupportedInterfaceOrientations~iphone", "UIInterfaceOrientationPortrait");
            SetOrientations(dict, "UISupportedInterfaceOrientations~ipad",
                "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight");
            // Legacy initial-orientation keys must not override the device-specific declarations.
            foreach (string key in new[] { "UIInterfaceOrientation", "UIInterfaceOrientation~iphone", "UIInterfaceOrientation~ipad" })
                RemoveKey(dict, key);
            plist.Save(path);
        }

        static void SetOrientations(XElement dict, string key, params string[] orientations)
        {
            RemoveKey(dict, key);
            dict.Add(new XElement("key", key), new XElement("array", orientations.Select(value => new XElement("string", value))));
        }

        static void RemoveKey(XElement dict, string name)
        {
            foreach (XElement key in dict.Elements("key").Where(element => element.Value == name).ToArray())
            {
                key.ElementsAfterSelf().FirstOrDefault()?.Remove();
                key.Remove();
            }
        }
    }
}
#endif
