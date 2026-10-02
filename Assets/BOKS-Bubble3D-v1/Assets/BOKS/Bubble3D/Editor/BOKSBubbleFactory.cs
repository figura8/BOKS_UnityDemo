using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace BOKS.BubbleFX.Editor
{
    public static class BOKSBubbleFactory
    {
        const string Folder = "Assets/BOKS/Bubble3D";

        [MenuItem("BOKS/Bubble 3D/Create bubble in scene")]
        public static void CreateBubble()
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            if (srp && !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
            {
                Debug.LogError("Bubble3D supports URP or Built-in. HDRP is not included.");
                return;
            }
            var bubbleShader = Shader.Find(srp ? "BOKS/Bubble3D URP" : "BOKS/Bubble3D BuiltIn");
            var fxShader = Shader.Find(srp ? "BOKS/BubblePop URP" : "BOKS/BubblePop BuiltIn");
            if (bubbleShader == null || fxShader == null)
            {
                Debug.LogError("Import the shaders for your render pipeline; see README.");
                return;
            }
            Material bubbleMat = GetMaterial("Bubble", bubbleShader);
            Material fxMat = GetMaterial("Pop", fxShader);
            var root = new GameObject("BOKS Bubble 3D");
            Undo.RegisterCreatedObjectUndo(root, "Create BOKS bubble");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Surface";
            sphere.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            var renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = bubbleMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var fx = root.AddComponent<BOKSBubble3D>();
            fx.surface = sphere.transform;
            fx.effectMaterial = fxMat;
            fx.effectCamera = Camera.main;
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            AssetDatabase.SaveAssets();
        }

        static Material GetMaterial(string name, Shader shader)
        {
            string path = Folder + "/" + name + "-" + (GraphicsSettings.currentRenderPipeline != null ? "URP" : "BuiltIn") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
