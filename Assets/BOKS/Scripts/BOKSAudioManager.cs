using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BOKS.Demo
{
    public enum BOKSAudioCue
    {
        BlockDetach,
        BlockDropSuccess,
        SlotHover,
        PlayPressed,
        ForwardStep,
        Turn,
        BlockedMove,
        Failure,
        BoksAnnoyed,
        GoalBounce,
        BubblePop,
        LevelComplete,
        Welcome
    }

    public enum BOKSAudioPlaybackMode
    {
        OneShot,
        Restart
    }

    /// <summary>
    /// Owns all BOKS audio playback. Gameplay reports semantic cues; clip paths, gains and
    /// overlap policy stay here and mirror UnityExport/Data/audio-cues.json.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class BOKSAudioManager : MonoBehaviour
    {
        const string ResourceRoot = "BOKS/Audio/";
        const float IntroGain = .72f;
        const float LoopGain = .18f;
        const float IntroFallbackSeconds = 3.2f;

        sealed class CueDefinition
        {
            public readonly string ResourcePath;
            public readonly string FileName;
            public readonly float Gain;
            public readonly BOKSAudioPlaybackMode Mode;

            public CueDefinition(string resourcePath, string fileName, float gain, BOKSAudioPlaybackMode mode)
            {
                ResourcePath = ResourceRoot + resourcePath;
                FileName = fileName;
                Gain = gain;
                Mode = mode;
            }
        }

        static readonly Dictionary<BOKSAudioCue, CueDefinition> Definitions =
            new Dictionary<BOKSAudioCue, CueDefinition>
            {
                { BOKSAudioCue.BlockDetach, new CueDefinition("sfx/ui/block_detach", "block_detach.ogg", .42f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.BlockDropSuccess, new CueDefinition("sfx/ui/block_drop_success", "block_drop_success.mp3", .48f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.SlotHover, new CueDefinition("sfx/ui/slot_hover", "slot_hover.mp3", .22f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.PlayPressed, new CueDefinition("sfx/ui/play_press_main", "play_press_main.mp3", .34f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.ForwardStep, new CueDefinition("sfx/gameplay/step_move_02", "step_move_02.mp3", .16f, BOKSAudioPlaybackMode.Restart) },
                { BOKSAudioCue.Turn, new CueDefinition("sfx/gameplay/rotation_position_02", "rotation_position_02.mp3", .28f, BOKSAudioPlaybackMode.Restart) },
                { BOKSAudioCue.BlockedMove, new CueDefinition("sfx/gameplay/effort", "effort.mp3", .24f, BOKSAudioPlaybackMode.Restart) },
                { BOKSAudioCue.Failure, new CueDefinition("sfx/gameplay/error_action", "error_action.mp3", .30f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.BoksAnnoyed, new CueDefinition("sfx/gameplay/boks_annoyed", "boks_annoyed.ogg", .34f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.GoalBounce, new CueDefinition("sfx/gameplay/goal_bubble_bounce", "goal_bubble_bounce.ogg", .28f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.BubblePop, new CueDefinition("sfx/gameplay/bubble_pop_main", "bubble_pop_main.ogg", .26f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.LevelComplete, new CueDefinition("sfx/gameplay/level_complete_main", "level_complete_main.mp3", .50f, BOKSAudioPlaybackMode.OneShot) },
                { BOKSAudioCue.Welcome, new CueDefinition("sfx/gameplay/wellcome", "wellcome.mp3", .34f, BOKSAudioPlaybackMode.OneShot) }
            };

        static BOKSAudioManager instance;
        static bool applicationQuitting;

        AudioSource musicIntroSource;
        AudioSource musicLoopSource;
        AudioSource oneShotSource;
        AudioListener ownedListener;
        readonly Dictionary<BOKSAudioCue, AudioSource> restartSources = new Dictionary<BOKSAudioCue, AudioSource>();
        readonly Dictionary<BOKSAudioCue, AudioClip> clips = new Dictionary<BOKSAudioCue, AudioClip>();
        Coroutine introTransition;
        bool musicSequenceStarted;
        float musicVolume = 1f;
        float sfxVolume = 1f;

        public static BOKSAudioManager Instance => EnsureInstance();
        public static event Action<BOKSAudioCue> CueTriggered;

        public AudioSource MusicSource => musicLoopSource;
        public AudioSource MusicIntroSource => musicIntroSource;
        public AudioSource SfxSource => oneShotSource;
        public bool MusicEnabled { get; private set; } = true;
        public bool SfxEnabled { get; private set; } = true;

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                ApplyMusicVolumes();
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            applicationQuitting = false;
            CueTriggered = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureInstance();
        }

        static BOKSAudioManager EnsureInstance()
        {
            if (instance != null) return instance;
            if (applicationQuitting) return null;

            instance = FindAnyObjectByType<BOKSAudioManager>();
            if (instance == null)
            {
                GameObject root = new GameObject("BOKS Audio Manager");
                instance = root.AddComponent<BOKSAudioManager>();
            }
            return instance;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ownedListener = gameObject.AddComponent<AudioListener>();
            musicIntroSource = CreateSource("Music Intro", false);
            musicLoopSource = CreateSource("Music Loop", true);
            oneShotSource = CreateSource("SFX One Shots", false);
            ApplyMusicVolumes();
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartGameplayMusic();
        }

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        AudioSource CreateSource(string sourceName, bool loop)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        public static void Play(BOKSAudioCue cue)
        {
            BOKSAudioManager manager = EnsureInstance();
            if (manager != null) manager.PlayCue(cue);
        }

        public void PlayCue(BOKSAudioCue cue)
        {
            if (!SfxEnabled || !Definitions.TryGetValue(cue, out CueDefinition definition)) return;
            AudioClip clip = GetClip(cue, definition);
            if (clip == null) return;

            if (definition.Mode == BOKSAudioPlaybackMode.OneShot)
            {
                oneShotSource.PlayOneShot(clip, definition.Gain * sfxVolume);
            }
            else
            {
                if (!restartSources.TryGetValue(cue, out AudioSource source))
                {
                    source = CreateSource("SFX " + cue, false);
                    restartSources.Add(cue, source);
                }
                source.Stop();
                source.clip = clip;
                source.time = 0f;
                source.volume = definition.Gain * sfxVolume;
                source.Play();
            }

            CueTriggered?.Invoke(cue);
        }

        AudioClip GetClip(BOKSAudioCue cue, CueDefinition definition)
        {
            if (!clips.TryGetValue(cue, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(definition.ResourcePath);
                clips.Add(cue, clip);
                if (clip == null) Debug.LogWarning("BOKS audio clip is missing: " + definition.FileName, this);
            }
            return clip;
        }

        public static string GetSourceFile(BOKSAudioCue cue) => Definitions[cue].FileName;
        public static float GetSourceGain(BOKSAudioCue cue) => Definitions[cue].Gain;
        public static BOKSAudioPlaybackMode GetPlaybackMode(BOKSAudioCue cue) => Definitions[cue].Mode;

        public void SetMusicEnabled(bool enabled)
        {
            if (MusicEnabled == enabled) return;
            MusicEnabled = enabled;
            if (!enabled)
            {
                if (introTransition != null) StopCoroutine(introTransition);
                introTransition = null;
                musicIntroSource.Stop();
                musicLoopSource.Stop();
            }
            else if (musicSequenceStarted)
            {
                StartLoopIfNeeded();
            }
            else
            {
                StartGameplayMusic();
            }
        }

        public void SetSfxEnabled(bool enabled)
        {
            SfxEnabled = enabled;
            if (enabled) return;
            oneShotSource.Stop();
            foreach (AudioSource source in restartSources.Values) source.Stop();
        }

        /// <summary>Starts the campaign intro once, then the persistent gameplay loop.</summary>
        public void StartGameplayMusic()
        {
            if (musicSequenceStarted || !MusicEnabled) return;
            musicSequenceStarted = true;
            AudioClip intro = Resources.Load<AudioClip>(ResourceRoot + "music/level_01_intro_main");
            if (intro == null)
            {
                StartLoopIfNeeded();
                return;
            }

            musicIntroSource.clip = intro;
            musicIntroSource.time = 0f;
            musicIntroSource.Play();
            introTransition = StartCoroutine(QueueLoopAfterIntro());
        }

        IEnumerator QueueLoopAfterIntro()
        {
            float startedAt = Time.realtimeSinceStartup;
            yield return null;
            while (MusicEnabled && musicIntroSource.isPlaying && Time.realtimeSinceStartup - startedAt < IntroFallbackSeconds)
                yield return null;

            introTransition = null;
            if (MusicEnabled) StartLoopIfNeeded();
        }

        void StartLoopIfNeeded()
        {
            if (!MusicEnabled || musicLoopSource.isPlaying) return;
            if (musicLoopSource.clip == null)
                musicLoopSource.clip = Resources.Load<AudioClip>(ResourceRoot + "music/game_loop_main");
            if (musicLoopSource.clip == null)
            {
                Debug.LogWarning("BOKS gameplay music is missing: game_loop_main.mp3", this);
                return;
            }
            musicLoopSource.loop = true;
            musicLoopSource.Play();
        }

        void ApplyMusicVolumes()
        {
            if (musicIntroSource != null) musicIntroSource.volume = IntroGain * musicVolume;
            if (musicLoopSource != null) musicLoopSource.volume = LoopGain * musicVolume;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            KeepExactlyOneActiveListener();
            StartGameplayMusic();
            StartCoroutine(InstallIdleCueTargetsNextFrame());
        }

        void KeepExactlyOneActiveListener()
        {
            bool sceneHasListener = false;
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (listener != ownedListener && listener.enabled && listener.gameObject.activeInHierarchy)
                {
                    sceneHasListener = true;
                    break;
                }
            }
            ownedListener.enabled = !sceneHasListener;
        }

        IEnumerator InstallIdleCueTargetsNextFrame()
        {
            yield return null;
            BOKSLevel2Controller controller = FindAnyObjectByType<BOKSLevel2Controller>();
            if (controller == null) yield break;

            Transform[] transforms = controller.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == "BOKS Art")
                    ConfigurePointerCue(candidate.gameObject, controller, BOKSAudioCue.BoksAnnoyed, .95f);
                else if (candidate.name == "Bubble Fill")
                    ConfigurePointerCue(candidate.gameObject, controller, BOKSAudioCue.GoalBounce, .70f);
            }
        }

        static void ConfigurePointerCue(GameObject target, BOKSLevel2Controller controller, BOKSAudioCue cue, float cooldown)
        {
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
            BOKSAudioPointerCue trigger = target.GetComponent<BOKSAudioPointerCue>();
            if (trigger == null) trigger = target.AddComponent<BOKSAudioPointerCue>();
            trigger.Configure(controller, cue, cooldown);
        }
    }

    sealed class BOKSAudioPointerCue : MonoBehaviour, IPointerDownHandler
    {
        BOKSLevel2Controller controller;
        BOKSAudioCue cue;
        float cooldownSeconds;
        float nextAllowedTime;

        public void Configure(BOKSLevel2Controller levelController, BOKSAudioCue audioCue, float cooldown)
        {
            controller = levelController;
            cue = audioCue;
            cooldownSeconds = cooldown;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controller == null || controller.InputLocked || controller.IsRunning || Time.unscaledTime < nextAllowedTime) return;
            nextAllowedTime = Time.unscaledTime + cooldownSeconds;
            BOKSAudioManager.Play(cue);
        }
    }
}
