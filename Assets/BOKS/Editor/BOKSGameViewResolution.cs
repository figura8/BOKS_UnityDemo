using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BOKS.Editor
{
    /// <summary>Editor-only Game View resolution selection for BOKS portrait and editor scenes.</summary>
    [InitializeOnLoad]
    static class BOKSGameViewResolution
    {
        static readonly Resolution Portrait = new Resolution("BOKS Portrait 720x1600", 720, 1600);
        static readonly Resolution Landscape = new Resolution("BOKS Level Editor 1600x900", 1600, 900);

        struct Resolution
        {
            public readonly string label;
            public readonly int width;
            public readonly int height;
            public Resolution(string label, int width, int height) { this.label = label; this.width = width; this.height = height; }
        }

        static BOKSGameViewResolution()
        {
            EditorSceneManager.activeSceneChangedInEditMode += (_, scene) => ApplyFor(scene);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ApplyFor(SceneManager.GetActiveScene());
            };
            EditorApplication.delayCall += () => ApplyFor(SceneManager.GetActiveScene());
        }

        static void ApplyFor(Scene scene)
        {
            if (scene.name == "BOKS_LevelEditor")
            {
                Select(Landscape);
                PrepareLevelEditorSceneView();
            }
            else if (scene.name == "BOKS_MainMenu" || scene.name == "BOKS_Campaign") Select(Portrait);
        }

        static void PrepareLevelEditorSceneView()
        {
            EditorApplication.delayCall += () =>
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null) return;
                sceneView.in2DMode = true;
                GameObject canvasRoot = GameObject.Find("BOKS Level Editor UI");
                if (canvasRoot == null) return;
                Selection.activeGameObject = canvasRoot;
                sceneView.FrameSelected();
            };
        }

        static void Select(Resolution wanted)
        {
            Type sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            Type groupType = Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor");
            Type sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
            Type sizeKind = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (sizesType == null || groupType == null || sizeType == null || sizeKind == null || gameViewType == null) return;

            object sizes = GetGameViewSizesInstance(sizesType);
            if (sizes == null) return;
            object standalone = Enum.Parse(groupType, "Standalone");
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(sizes, new[] { standalone });
            if (group == null) return;

            MethodInfo countMethod = group.GetType().GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo getMethod = group.GetType().GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.Instance);
            int count = (int)countMethod.Invoke(group, null);
            int index = FindSize(getMethod, group, count, wanted.width, wanted.height);
            if (index < 0)
            {
                object fixedResolution = Enum.Parse(sizeKind, "FixedResolution");
                object size = Activator.CreateInstance(sizeType, fixedResolution, wanted.width, wanted.height, wanted.label);
                group.GetType().GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance)?.Invoke(group, new[] { size });
                sizesType.GetMethod("SaveToHDD", BindingFlags.Public | BindingFlags.Instance)?.Invoke(sizes, null);
                count = (int)countMethod.Invoke(group, null);
                index = FindSize(getMethod, group, count, wanted.width, wanted.height);
            }
            if (index < 0) return;

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(gameView, new object[] { index, null });
        }

        static int FindSize(MethodInfo get, object group, int count, int width, int height)
        {
            for (int i = 0; i < count; i++)
            {
                object size = get.Invoke(group, new object[] { i });
                int w = (int)size.GetType().GetProperty("width")?.GetValue(size);
                int h = (int)size.GetType().GetProperty("height")?.GetValue(size);
                if (w == width && h == height) return i;
            }
            return -1;
        }

        static object GetGameViewSizesInstance(Type sizesType)
        {
            // `instance` is declared on ScriptableSingleton<T>, not necessarily as a public
            // member of GameViewSizes itself. Resolve that static property on its declaring type
            // before invoking GetGroup, which is an instance method.
            for (Type type = sizesType; type != null; type = type.BaseType)
            {
                PropertyInfo instance = type.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (instance != null) return instance.GetValue(null);
            }
            return null;
        }
    }
}
