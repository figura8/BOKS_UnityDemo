using System.Collections;
using BOKS.BubbleFX;
using BOKS.Demo;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Lives alongside the sandbox, so the menu's runtime assembly only needs the small interface.
public sealed class BOKSMenuBubble3D : MonoBehaviour, IBOKSMenuBubble
{
    [SerializeField] TextAsset savedSettings;
    [SerializeField] Sprite forwardIcon;
    [SerializeField] Shader membraneShader;
    [SerializeField] Shader popShader;
    BOKSBubble3D bubble;
    Material membrane, pop;
    RenderTexture texture;
    Transform stage;
    RawImage image;
    float floatTime;

    [System.Serializable]
    sealed class Settings
    {
        public float rim, iridescence, highlight, centerAlpha, rimPower, wobble, wobbleSpeed;
        public float popDuration, anticipationSeconds, inflation, arcWidth, dropletSize, expansion, opacity;
        public int arcCount, dropCount;
        public bool membraneTearPop;
        public Color tint;
    }

    public void Build(RectTransform host)
    {
        Settings s = JsonUtility.FromJson<Settings>(savedSettings.text);
        membrane = new Material(membraneShader);
        pop = new Material(popShader);
        membrane.SetFloat("_RimAlpha", s.rim);
        membrane.SetFloat("_Iridescence", s.iridescence);
        membrane.SetFloat("_Highlight", s.highlight);
        membrane.SetFloat("_CenterAlpha", s.centerAlpha);
        membrane.SetFloat("_RimPower", s.rimPower);
        membrane.SetFloat("_Opacity", s.opacity);
        membrane.SetColor("_Tint", s.tint);
        texture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        image = host.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        // Same camera framing as the sandbox: leave room for its one-second mist tail.
        host.sizeDelta = Vector2.one * 560f;
        host.gameObject.AddComponent<BOKSMenuBubbleHitArea>();
        // Keep the preview far away from game cameras without changing project layers.
        stage = new GameObject("Menu Bubble 3D Render Stage").transform;
        stage.SetParent(transform, false);
        stage.localPosition = new Vector3(10000f, 10000f, 10000f);
        var cameraObject = new GameObject("Menu Bubble Texture Camera");
        cameraObject.transform.SetParent(stage, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -8f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 20f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.targetTexture = texture;
        var root = new GameObject("Menu Bubble with Forward");
        root.transform.SetParent(stage, false);
        root.SetActive(false);
        var surface = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        surface.transform.SetParent(root.transform, false);
        surface.transform.localScale = Vector3.one * 2f;
        Destroy(surface.GetComponent<Collider>());
        Renderer renderer = surface.GetComponent<Renderer>();
        renderer.sharedMaterial = membrane;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var payload = new GameObject("Floating Forward", typeof(SpriteRenderer));
        payload.transform.SetParent(root.transform, false);
        payload.transform.localPosition = new Vector3(0f, 0f, -1.03f);
        payload.transform.localScale = Vector3.one * (1.05f / Mathf.Max(forwardIcon.bounds.size.x, .01f));
        SpriteRenderer sprite = payload.GetComponent<SpriteRenderer>();
        sprite.sprite = forwardIcon;
        sprite.color = new Color(1f, 1f, 1f, .92f);
        sprite.sortingOrder = 1;
        bubble = root.AddComponent<BOKSBubble3D>();
        bubble.surface = surface.transform;
        bubble.payload = payload.transform;
        bubble.effectCamera = camera;
        bubble.effectMaterial = pop;
        bubble.wobble = s.wobble;
        bubble.wobbleSpeed = s.wobbleSpeed;
        bubble.popDuration = s.popDuration;
        bubble.membraneTearPop = s.membraneTearPop;
        bubble.anticipationSeconds = s.anticipationSeconds;
        bubble.inflation = s.inflation;
        bubble.arcCount = s.arcCount;
        bubble.dropCount = s.dropCount;
        bubble.arcWidth = s.arcWidth;
        bubble.dropletSize = s.dropletSize;
        bubble.expansion = s.expansion;
        root.SetActive(true);
    }

    void LateUpdate()
    {
        if (bubble == null || bubble.IsPopping || bubble.IsPopped) return;
        floatTime += Time.deltaTime;
        float phase = floatTime * 1.1f;
        bubble.payload.localPosition = new Vector3(Mathf.Sin(phase * .73f) * .035f,
            Mathf.Sin(phase) * .055f, -1.03f);
        bubble.payload.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * .82f) * 4f);
    }

    public IEnumerator Pop()
    {
        bubble.Pop();
        while (bubble.IsPopping) yield return null;
    }

    void OnDestroy()
    {
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (membrane != null) Destroy(membrane);
        if (pop != null) Destroy(pop);
    }
}

public sealed class BOKSMenuBubbleHitArea : MonoBehaviour, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out Vector2 point)) return false;
        float radius = rect.rect.width * (1.1f / 6f);
        return point.sqrMagnitude <= radius * radius;
    }
}
