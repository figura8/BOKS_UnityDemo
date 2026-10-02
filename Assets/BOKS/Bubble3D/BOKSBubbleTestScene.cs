using BOKS.BubbleFX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Standalone visual sandbox for the existing Bubble3D prototype.
public sealed class BOKSBubbleTestScene : MonoBehaviour
{
    [SerializeField] Sprite forwardIcon;
    Camera previewCamera;
    BOKSBubble3D bubble;
    Material membrane, pop;
    float rim = 0.73f, iridescence = 0.74f, highlight = 0.95f;
    bool dark = true;
    string state = "Pronta";
    Rect panel;
    Vector2 panelScroll;
    GUIStyle largeLabel, largeButton, largeToggle;
    const float PanelWidth = 440f;
    public enum BubblePreset { Attuale, Delicato }
    BubblePreset preset;
    bool goalSize;
    [SerializeField, Min(16)] float goalDiameterPixels = 62f;
    float centerAlpha = .025f, rimPower = 3.2f;
    Vector3 forwardRestPosition;
    float forwardFloatTime;

    public void ApplyPreset(BubblePreset value)
    {
        bubble.ResetBubble();
        state = "Pronta";
        preset = value;
        bool gentle = value == BubblePreset.Delicato;
        bubble.membraneTearPop = !gentle;
        bubble.wobble = gentle ? .012f : .1f;
        bubble.popDuration = gentle ? .24f : .28f;
        bubble.anticipationSeconds = gentle ? .06f : .055f;
        bubble.inflation = gentle ? .10f : .09f;
        bubble.arcCount = gentle ? 4 : 8;
        bubble.dropCount = gentle ? 6 : 18;
        bubble.arcWidth = gentle ? .025f : .10f;
        bubble.dropletSize = gentle ? .055f : .13f;
        bubble.expansion = gentle ? .30f : .92f;
        rim = gentle ? .40f : .73f;
        iridescence = gentle ? 0f : .74f;
        highlight = gentle ? .65f : .95f;
        centerAlpha = gentle ? 0f : .025f;
        rimPower = gentle ? 5f : 3.2f;
        membrane.SetColor("_Tint", gentle ? new Color(.96f, .98f, 1f) : new Color(.76f, .92f, .96f));
    }

    void Awake()
    {
        var cameraObject = new GameObject("Bubble preview camera");
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.transform.position = new Vector3(0, 0, -8);
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 3;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        var srp = GraphicsSettings.currentRenderPipeline != null;
        var surfaceShader = Shader.Find(srp ? "BOKS/Bubble3D URP" : "BOKS/Bubble3D BuiltIn");
        var popShader = Shader.Find(srp ? "BOKS/BubblePop URP" : "BOKS/BubblePop BuiltIn");
        if (surfaceShader == null || popShader == null)
        {
            state = "Shader della bolla mancanti: controllare la Console.";
            Debug.LogError(state, this);
            return;
        }
        membrane = new Material(surfaceShader);
        pop = new Material(popShader);
        var root = new GameObject("Test bubble");
        // Configure while inactive so Awake caches the authored surface scale.
        root.SetActive(false);
        var surface = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        surface.name = "Surface";
        surface.transform.SetParent(root.transform, false);
        surface.transform.localScale = Vector3.one * 2;
        Destroy(surface.GetComponent<Collider>());
        var renderer = surface.GetComponent<Renderer>();
        renderer.sharedMaterial = membrane;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (forwardIcon != null)
        {
            // A real BOKS command card floats inside the transparent membrane. It is placed just
            // in front of the near sphere surface so it reads as contained rather than occluded.
            var payload = new GameObject("Forward command inside bubble", typeof(SpriteRenderer));
            payload.transform.SetParent(root.transform, false);
            payload.transform.localPosition = new Vector3(0, 0, -1.03f);
            payload.transform.localScale = Vector3.one * (1.05f / Mathf.Max(forwardIcon.bounds.size.x, 0.01f));
            var payloadRenderer = payload.GetComponent<SpriteRenderer>();
            payloadRenderer.sprite = forwardIcon;
            payloadRenderer.color = new Color(1f, 1f, 1f, 0.92f);
            payloadRenderer.sortingOrder = 1;
            bubble = root.AddComponent<BOKSBubble3D>();
            bubble.payload = payload.transform;
            forwardRestPosition = payload.transform.localPosition;
        }
        else
        {
            Debug.LogError("Forward sprite is missing from the Bubble3D test scene.", this);
            bubble = root.AddComponent<BOKSBubble3D>();
        }
        bubble.surface = surface.transform;
        bubble.effectMaterial = pop;
        bubble.effectCamera = previewCamera;
        bubble.membraneTearPop = true;
        bubble.wobble = 0.1f;
        bubble.popDuration = 0.28f;
        bubble.onBurst.AddListener(() => state = "Scoppio");
        bubble.onPopFinished.AddListener(() => state = "Terminato — premi Ripristina");
        root.SetActive(true);
        ApplyPreset(BubblePreset.Attuale);
    }

    void Update()
    {
        previewCamera.rect = Screen.width >= 900
            ? new Rect(PanelWidth / Screen.width, 0, 1 - PanelWidth / Screen.width, 1)
            : new Rect(0, 0, 1, 0.48f);
        previewCamera.backgroundColor = dark ? new Color(0.075f, 0.10f, 0.16f) : new Color(0.85f, 0.91f, 0.94f);
        if (bubble == null) return;
        // The goal shell is 56 x 62 reference pixels. Keep the large bubble geometry and
        // payload together; change only camera magnification for the final-size comparison.
        previewCamera.orthographicSize = goalSize
            ? Mathf.Max(1f, previewCamera.pixelHeight / Mathf.Max(16f, goalDiameterPixels)) : 3f;
        membrane.SetFloat("_RimAlpha", rim);
        membrane.SetFloat("_CenterAlpha", centerAlpha);
        membrane.SetFloat("_RimPower", rimPower);
        membrane.SetFloat("_Iridescence", iridescence);
        membrane.SetFloat("_Highlight", highlight);
        Vector2 point = default;
        bool pressed = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            point = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            point = Mouse.current.position.ReadValue();
            pressed = true;
        }
        if (!pressed || !previewCamera.pixelRect.Contains(point) || panel.Contains(new Vector2(point.x, Screen.height - point.y))) return;
        var ray = previewCamera.ScreenPointToRay(point);
        var center = bubble.surface.position;
        float along = Vector3.Dot(center - ray.origin, ray.direction);
        var closest = ray.origin + ray.direction * along;
        float radius = bubble.surface.lossyScale.x * 0.5f;
        float missSquared = (closest - center).sqrMagnitude;
        if (along > 0 && missSquared <= radius * radius)
        {
            float entry = along - Mathf.Sqrt(radius * radius - missSquared);
            bubble.SetPopPoint(ray.origin + ray.direction * entry);
            bubble.Pop();
        }
    }

    void LateUpdate()
    {
        if (bubble == null || bubble.payload == null || bubble.surface == null ||
            bubble.IsPopping || bubble.IsPopped || !bubble.payload.gameObject.activeSelf) return;

        // The shared parent carries the bubble's movement. Add a small independent sway,
        // phase-linked to the membrane wobble, without scaling the command graphic.
        forwardFloatTime += Time.deltaTime;
        float phase = forwardFloatTime * 1.1f;
        float swayX = Mathf.Sin(phase * .73f) * .035f;
        float swayY = Mathf.Sin(phase) * .055f;
        Vector3 centre = bubble.surface.localPosition;
        bubble.payload.localPosition = new Vector3(centre.x + swayX, centre.y + swayY,
            forwardRestPosition.z + centre.z);
        bubble.payload.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * .82f) * 4f);
    }

    void OnGUI()
    {
        if (largeLabel == null)
        {
            largeLabel = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
            largeButton = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            largeToggle = new GUIStyle(GUI.skin.toggle) { fontSize = 24, padding = new RectOffset(28, 8, 8, 8) };
        }
        panel = new Rect(12, 12, Mathf.Min(PanelWidth - 24, Screen.width - 24),
            Screen.width >= 900 ? Screen.height - 24 : Screen.height * 0.52f - 24);
        // The preview camera excludes this area. Clear it explicitly when scrolling or switching
        // presets, otherwise old IMGUI text remains in the uncovered backbuffer.
        Color previousColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width >= 900 ? PanelWidth : Screen.width,
            Screen.width >= 900 ? Screen.height : Screen.height * .52f), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUILayout.BeginArea(panel, GUI.skin.box);
        panelScroll = GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label("BOKS — Bubble 3D test", largeLabel);
        GUILayout.Label("Click / tap sulla bolla per scoppiare", largeLabel);
        GUILayout.Label("Payload: blocco Forward", largeLabel);
        GUILayout.Label(state, largeLabel);
        if (bubble != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Attuale", largeButton, GUILayout.Height(50))) ApplyPreset(BubblePreset.Attuale);
            if (GUILayout.Button("Delicato", largeButton, GUILayout.Height(50))) ApplyPreset(BubblePreset.Delicato);
            GUILayout.EndHorizontal();
            GUILayout.Label("Preset: " + preset, largeLabel);
            goalSize = GUILayout.Toggle(goalSize, "Dimensione goal in gioco", largeToggle);
            if (goalSize) goalDiameterPixels = Slider("Diametro apparente (px)", goalDiameterPixels, 32, 120);
            GUI.enabled = !bubble.IsPopping && !bubble.IsPopped;
            if (GUILayout.Button("Scoppia", largeButton, GUILayout.Height(56))) bubble.Pop();
            GUI.enabled = true;
            if (GUILayout.Button("Ripristina", largeButton, GUILayout.Height(56)))
            {
                bubble.ResetBubble();
                state = "Pronta";
            }
            dark = GUILayout.Toggle(dark, "Sfondo scuro", largeToggle);
            bubble.membraneTearPop = GUILayout.Toggle(bubble.membraneTearPop, "Membrana che si lacera", largeToggle);
            bubble.popDuration = Slider("Durata scoppio (s)", bubble.popDuration, 0.08f, 1);
            if (!bubble.membraneTearPop)
            {
                GUILayout.Label("Totale: " + (bubble.anticipationSeconds + bubble.popDuration).ToString("0.00") + " s", largeLabel);
                bubble.anticipationSeconds = Slider("Anticipazione (s)", bubble.anticipationSeconds, .01f, .15f);
                bubble.inflation = Slider("Gonfiaggio", bubble.inflation, 0f, .2f);
                bubble.arcCount = Mathf.RoundToInt(Slider("Archi", bubble.arcCount, 1, 16));
                bubble.dropCount = Mathf.RoundToInt(Slider("Goccioline", bubble.dropCount, 1, 32));
                bubble.arcWidth = Slider("Spessore archi", bubble.arcWidth, .005f, .15f);
                bubble.dropletSize = Slider("Dimensione gocce", bubble.dropletSize, .01f, .2f);
                bubble.expansion = Slider("Espansione", bubble.expansion, .1f, 1.2f);
            }
            bubble.wobble = Slider("Wobble", bubble.wobble, 0, 0.1f);
            rim = Slider("Opacità bordo", rim, 0, 1);
            iridescence = Slider("Iridescenza", iridescence, 0, 1);
            highlight = Slider("Riflessi", highlight, 0, 1);
            centerAlpha = Slider("Opacità centro", centerAlpha, 0, .25f);
            rimPower = Slider("Bordo sottile", rimPower, 1, 8);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    float Slider(string label, float value, float min, float max)
    {
        GUILayout.Space(10);
        GUILayout.Label(label + ": " + value.ToString("0.00"), largeLabel);
        return GUILayout.HorizontalSlider(value, min, max, GUILayout.Height(28));
    }

    void OnDestroy()
    {
        if (membrane != null) Destroy(membrane);
        if (pop != null) Destroy(pop);
    }
}
