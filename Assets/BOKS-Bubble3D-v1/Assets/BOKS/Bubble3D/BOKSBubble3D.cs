using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BOKS.BubbleFX
{
    [DisallowMultipleComponent]
    public sealed class BOKSBubble3D : MonoBehaviour
    {
        public Transform surface;
        [Tooltip("Optional visual carried inside the bubble. It is hidden with the membrane during Pop and restored by Reset Bubble.")]
        public Transform payload;
        public Material effectMaterial;
        public Camera effectCamera;
        [Range(0f, 0.1f)] public float wobble = 0.025f;
        [Min(0.1f)] public float wobbleSpeed = 1.5f;
        [Min(0.05f)] public float popDuration = 0.28f;
        [Tooltip("Test variant: membrane tears from the touch point, followed by small droplets.")]
        public bool membraneTearPop;
        [Min(0.001f)] public float anticipationSeconds = 0.055f;
        [Range(0f, 0.3f)] public float inflation = 0.09f;
        [Range(1, 16)] public int arcCount = 8;
        [Range(1, 32)] public int dropCount = 18;
        [Range(0.005f, 0.2f)] public float arcWidth = 0.10f;
        [Range(0.01f, 0.25f)] public float dropletSize = 0.13f;
        [Range(0.1f, 1.5f)] public float expansion = 0.92f;
        public UnityEvent onBurst = new UnityEvent();
        public UnityEvent onPopFinished = new UnityEvent();
        public bool IsPopping { get; private set; }
        public bool IsPopped { get; private set; }

        Vector3 baseScale;
        readonly List<GameObject> effects = new List<GameObject>();
        Coroutine routine;
        float clock;
        Vector3 popPoint;
        Renderer surfaceRenderer;
        MaterialPropertyBlock surfaceProperties;

        void Awake()
        {
            if (surface != null) baseScale = surface.localScale;
            if (surface != null) surfaceRenderer = surface.GetComponent<Renderer>();
            if (effectCamera == null) effectCamera = Camera.main;
        }

        public void SetPopPoint(Vector3 worldPoint) { popPoint = worldPoint; }

        void Update()
        {
            if (surface == null || IsPopping || IsPopped) return;
            clock += Time.deltaTime * wobbleSpeed;
            float a = Mathf.Sin(clock) * wobble;
            float b = Mathf.Sin(clock * 1.37f + 1.1f) * wobble;
            surface.localScale = Vector3.Scale(baseScale,
                new Vector3(1f + a, 1f + b, 1f / ((1f + a) * (1f + b))));
        }

        // Invoke this from the existing tap/goal handler. No input system dependency.
        [ContextMenu("Preview Pop (Play Mode)")]
        public void Pop()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || surface == null || effectMaterial == null || IsPopping || IsPopped) return;
            IsPopping = true;
            routine = StartCoroutine(membraneTearPop ? MembraneTearSequence() : PopSequence());
        }

        [ContextMenu("Reset Bubble (Play Mode)")]
        public void ResetBubble()
        {
            if (!Application.isPlaying) return;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            ClearEffects();
            IsPopping = false;
            IsPopped = false;
            popPoint = Vector3.zero;
            if (surface != null) { surface.localScale = baseScale; surface.gameObject.SetActive(true); }
            if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(null);
            if (payload != null) payload.gameObject.SetActive(true);
        }

        IEnumerator PopSequence()
        {
            Vector3 startScale = surface.localScale;
            float anticipation = Mathf.Max(.001f, anticipationSeconds);
            for (float t = 0; t < anticipation; t += Time.deltaTime)
            {
                surface.localScale = Vector3.Lerp(startScale, baseScale * (1f + inflation), Mathf.Clamp01(t / anticipation));
                yield return null;
            }
            surface.localScale = baseScale * (1f + inflation);
            Vector3 center = surface.position;
            float radius = Mathf.Max(Mathf.Abs(surface.lossyScale.x), Mathf.Abs(surface.lossyScale.y)) * 0.5f;
            Camera cam = effectCamera != null ? effectCamera : Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            surface.gameObject.SetActive(false);
            if (payload != null) payload.gameObject.SetActive(false);
            onBurst.Invoke();

            // The original prototype's four very thin arcs were difficult to read against a dark
            // background. A denser, broader membrane burst keeps the same lightweight approach
            // but makes the pop legible at the bubble's normal on-screen size.
            int arcCount = Mathf.Clamp(this.arcCount, 1, 16), dropCount = Mathf.Clamp(this.dropCount, 1, 32);
            const int segments = 16;
            var arcs = new LineRenderer[arcCount];
            var drops = new Transform[dropCount];
            var renderers = new Renderer[dropCount];
            for (int j = 0; j < arcCount; j++)
            {
                var go = new GameObject("Bubble membrane arc");
                go.layer = gameObject.layer;
                effects.Add(go);
                arcs[j] = go.AddComponent<LineRenderer>();
                arcs[j].sharedMaterial = effectMaterial;
                arcs[j].positionCount = segments;
                arcs[j].useWorldSpace = true;
                arcs[j].numCapVertices = 4;
                arcs[j].alignment = LineAlignment.View;
            }
            for (int j = 0; j < dropCount; j++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Bubble droplet";
                go.layer = gameObject.layer;
                Destroy(go.GetComponent<Collider>());
                effects.Add(go);
                drops[j] = go.transform;
                renderers[j] = go.GetComponent<Renderer>();
                renderers[j].sharedMaterial = effectMaterial;
            }
            var block = new MaterialPropertyBlock();
            for (float elapsed = 0; elapsed < popDuration; elapsed += Time.deltaTime)
            {
                float u = Mathf.Clamp01(elapsed / popDuration);
                float travel = 1f - (1f - u) * (1f - u);
                float fade = (1f - u) * (1f - u);
                block.SetFloat("_Opacity", fade);
                for (int j = 0; j < arcCount; j++)
                {
                    float angle = j * Mathf.PI * 2f / arcCount + 0.2f;
                    float span = Mathf.Lerp(1.45f, 0.10f, u);
                    float r = radius * (1.04f + travel * expansion);
                    arcs[j].widthMultiplier = radius * Mathf.Lerp(arcWidth, arcWidth * .06f, u);
                    arcs[j].SetPropertyBlock(block);
                    for (int k = 0; k < segments; k++)
                    {
                        float a = angle + ((float)k / (segments - 1) - 0.5f) * span;
                        arcs[j].SetPosition(k, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r);
                    }
                }
                for (int j = 0; j < dropCount; j++)
                {
                    float a = j * Mathf.PI * 2f / dropCount + 0.32f;
                    float r = radius * (1.00f + travel * expansion * (0.924f + 0.152f * (j % 3)));
                    drops[j].position = center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
                    drops[j].localScale = Vector3.one * radius * dropletSize * (1f + .169f * (j % 3)) * (1f - u * 0.72f);
                    renderers[j].SetPropertyBlock(block);
                }
                yield return null;
            }
            ClearEffects();
            IsPopping = false;
            IsPopped = true;
            routine = null;
            onPopFinished.Invoke();
        }

        IEnumerator MembraneTearSequence()
        {
            if (surfaceRenderer == null) surfaceRenderer = surface.GetComponent<Renderer>();
            if (surfaceProperties == null) surfaceProperties = new MaterialPropertyBlock();
            Vector3 center = surface.position;
            Camera cam = effectCamera != null ? effectCamera : Camera.main;
            Vector3 direction = popPoint == Vector3.zero
                ? (cam != null ? -cam.transform.forward : Vector3.back)
                : (popPoint - center).normalized;
            surfaceProperties.SetVector("_PopDirection", direction);
            float radius = Mathf.Max(Mathf.Abs(surface.lossyScale.x), Mathf.Abs(surface.lossyScale.y)) * 0.5f;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            const int dropCount = 11;
            var drops = new Transform[dropCount];
            var renderers = new Renderer[dropCount];
            var positions = new Vector3[dropCount];
            var velocities = new Vector3[dropCount];
            var sizes = new float[dropCount];
            var rng = new System.Random(2791);
            for (int j = 0; j < dropCount; j++)
            {
                float angle = (float)(j * 2.39996 + rng.NextDouble() * 0.35);
                float reach = radius * (0.20f + (float)rng.NextDouble() * 0.38f);
                positions[j] = center + direction * radius +
                    (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * reach;
                velocities[j] = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) *
                    (1.6f + (float)rng.NextDouble() * 1.5f) + up * 0.35f;
                sizes[j] = radius * (0.025f + (float)rng.NextDouble() * 0.035f);
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Membrane mist";
                go.layer = gameObject.layer;
                Destroy(go.GetComponent<Collider>());
                go.transform.position = positions[j];
                go.transform.localScale = Vector3.zero;
                renderers[j] = go.GetComponent<Renderer>();
                renderers[j].sharedMaterial = effectMaterial;
                drops[j] = go.transform;
                effects.Add(go);
            }
            // The membrane completes its tear quickly; popDuration controls the mist tail.
            float tearTime = 0.18f;
            float tailTime = Mathf.Max(tearTime, popDuration);
            float elapsed = 0f;
            var dropProperties = new MaterialPropertyBlock();
            while (elapsed < tailTime)
            {
                float tear = Mathf.Clamp01(elapsed / tearTime);
                if (tear < 1f)
                {
                    surfaceProperties.SetFloat("_PopProgress", Mathf.Max(0.001f, tear));
                    surfaceRenderer.SetPropertyBlock(surfaceProperties);
                }
                else if (surface.gameObject.activeSelf)
                {
                    surface.gameObject.SetActive(false);
                    if (payload != null) payload.gameObject.SetActive(false);
                    onBurst.Invoke();
                }
                float fade = Mathf.Pow(1f - Mathf.Clamp01(elapsed / tailTime), 2f);
                dropProperties.SetFloat("_Opacity", fade * 0.65f);
                for (int j = 0; j < dropCount; j++)
                {
                    drops[j].position = positions[j] + velocities[j] * elapsed +
                        Vector3.down * (2.2f * elapsed * elapsed);
                    drops[j].localScale = Vector3.one * sizes[j] *
                        Mathf.Min(1f, elapsed / 0.035f) * fade;
                    renderers[j].SetPropertyBlock(dropProperties);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (surface.gameObject.activeSelf)
            {
                surface.gameObject.SetActive(false);
                if (payload != null) payload.gameObject.SetActive(false);
                onBurst.Invoke();
            }
            ClearEffects();
            IsPopping = false;
            IsPopped = true;
            routine = null;
            onPopFinished.Invoke();
        }

        void ClearEffects()
        {
            foreach (var go in effects) if (go != null) Destroy(go);
            effects.Clear();
        }

        void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            ClearEffects();
            IsPopping = false;
            IsPopped = false;
            if (surface != null) { surface.localScale = baseScale; surface.gameObject.SetActive(true); }
            if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(null);
            if (payload != null) payload.gameObject.SetActive(true);
        }
    }
}
