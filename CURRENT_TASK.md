# CURRENT TASK

## Session handoff — generalized Campaign scroll transition

### Completed state

- The Bubble Bobble-style vertical Campaign transition is generalized for every valid `currentLevel -> nextLevel` step.
- The live current level and a fully prepared next-level visual presentation are visible together during the vertical scroll, separated by the transition gap.
- The staged next level is visual-only: input stays locked and only the live gameplay controller is active.
- BOKS remains continuously visible through the atomic gameplay-art to transition-proxy handoff and travels toward the actual spawn position of the next level.
- The approved softened movement start and slower pacing are retained: approximately 1.8–2.0 seconds total.
- Campaign BOKS colour is initialized from Level 1 and remains green across all levels.
- Direction during transport comes from `nextLevel.Direction`; the proxy uses authored directional sprites from the persistent Campaign colour set, with a maximum ~18° local tilt and a short cross-blend. The prior transient non-green flash is fixed: the visual preview explicitly receives the persistent campaign character before resolving its target sprite.
- The final Campaign level still uses its existing Campaign Complete behaviour rather than scrolling.
- The old iris transition remains only as a safe fallback if the next-level scroll cannot be staged.
- Temporary A/B/C, C0–C4 and V0–V4 scroll diagnostics have been removed; only concise fallback warnings remain.
- `BOKSDeviceLayout.cs` uses the current `FindObjectsByType` overload and `UNITY_EDITOR || DEBUG`; its deprecated API and `DEVELOPMENT_BUILD` warnings are cleaned up.
- DOTween’s two legacy `PluginImporter.serializedVersion: 1` warnings (`DOTween.dll.meta` and `DOTweenEditor.dll.meta`) are intentionally untouched. Do not manually regenerate or edit those plugin metadata files.

### Verification

- Unity C# compilation completed successfully after the persistent-colour preview fix.
- Do not change the approved scroll timing, proxy geometry, handoff, Campaign colour persistence, or final-level behaviour without new visual review.

---

## Session handoff — DOTween Main Menu intro

### Goal and current state

- Goal: create an animated BOKS Main Menu intro using DOTween.
- `Assets/Scenes/BOKS_MainMenu.unity` owns `BOKSMainMenu`, which builds the menu UI at runtime.
- DOTween was already present at `Assets/Plugins/Demigiant/DOTween`; it is configured in Unity and the intro now uses a DOTween `Sequence`.
- The approved static composition is the **end state**. Do not move or resize it when tuning animation:

```text
IntroRoot
├─ CharacterGreen
├─ CharacterRed
├─ CharacterYellow
├─ CharacterBlue
└─ LogoRoot
   ├─ LetterB
   ├─ LetterO
   ├─ UmlautLeft
   ├─ UmlautRight
   ├─ LetterK
   └─ LetterS
```

- Character sprites are existing runtime assets: Green → down, Red → right, Yellow → left, Blue → up.
- `IntroRoot` is centred/responsive for phone and tablet. The four characters and BÖKS logo are visible in the Main Menu.
- Original `Assets/BOKS/RuntimeAssets/boks-logo.png` and `Assets/BOKS/Source/UI/Brand/boks-logo.svg` were not modified.

### Logo asset debugging outcome

- The first six separated logo assets were SVGs. Unity imported them as `UnityEngine.UIElements.VectorImage`, so every `Resources.Load<Sprite>("BOKS/Intro/...")` returned null.
- Those temporary SVGs were replaced with transparent PNG sprites rasterized from the original SVG paths. Current intro assets are in `Assets/Resources/BOKS/Intro/`:
  - `LetterB.png`, `LetterO.png`, `LetterK.png`, `LetterS.png`
  - `UmlautLeft.png`, `UmlautRight.png`
- All six are imported as `Sprite (2D and UI)` single sprites and now load successfully.

### Animation status / next session

- Earlier DOTween character movement was tested successfully. Static-layout debug mode was used during composition work and is now disabled (`ShowFinalLayoutOnly = false`).
- Current Sequence caches each animated UI element's final `anchoredPosition`, `localScale`, `localRotation`, and alpha; it starts from temporary values and restores/tweens to the approved final state.
- Current direction: Green enters from above; Red left; Yellow right; Blue below. Characters use moderate `OutBack`, fade/scale in, and stagger.
- B → O → K → S scale/fade in with overlapping stagger; umlaut dots arrive from slightly above with a subtle `OutBack` settle.
- Target Sequence duration is about 1.2–1.5 seconds. Keep travel offsets, timing, and staggers grouped as easy-to-tune constants. Kill the previous Sequence before recreating it.
- The original flower/Challenge bubble remains part of the existing menu flow. It is shown after the intro sequence and still needs final coordination/polish with the intro.
- Preserve the existing `BOKS_Campaign` transition.

### Files changed during this session

- `Assets/BOKS/Scripts/BOKSMainMenu.cs` — runtime intro hierarchy, PNG logo loading/debugging, cached-final-state DOTween Sequence and tuning constants.
- `Assets/Scenes/BOKS_MainMenu.unity` — serialized references for the four existing character sprites.
- `Assets/Resources/BOKS/Intro/LetterB.png`, `LetterO.png`, `LetterK.png`, `LetterS.png`, `UmlautLeft.png`, `UmlautRight.png` and their `.meta` files — new transparent intro logo sprites.
- `CURRENT_TASK.md`, `AGENT_CONTEXT.md` — session handoff documentation.

### Worktree note

- The worktree was already dirty before this session. Preserve unrelated existing changes; do not assume every modified/untracked file belongs to this intro work.

---

## Session handoff — mobile layout and Program Area visual polish

### Stable GitHub baseline

- Published baseline: `bb70b2999027c0ac01a5960c8ade67e5fcd2daea`.
- Do not rewrite Git history or start new features tonight.

### Current mobile layout state

- Mobile device-layout foundation is implemented: phones use `PhonePortrait`; Android tablets and iPad use `TabletLandscape`.
- The Unity Editor provides `BOKS > Layout Test > Auto`, `Force Phone Portrait`, and `Force Tablet Landscape` for Game View preview. These overrides are Editor/Development-only and do not alter release classification.
- Tablet landscape layout is visually working in Game View.
- Preserve phone/tablet behavior, Android orientation handling, Android iris-transition shader inclusion, campaign Levels 1–10, and the exclusion of the Level Editor from player builds.

### Program Area state

- Main and Function read as separate visual zones while retaining the real 8 Main and 4 Function interactive slots.
- The Function area has a pale-blue background, blue dashed separator, blue connector behind its four centered slots, and a small blue start marker.
- It contains no Function text and no Function icon.

### Next visual polish task

**Remove the visible blue outer border around the Function panel.**

- Keep the pale-blue fill, blue dashed separator, blue connector, and small blue start marker.
- Remove only the rectangular blue outline/stroke.
- Do not move or resize slots, and do not change drag/drop, Function execution, gameplay, responsive layout, or hierarchy structure.

---

## Android phone test build

### Current state

- Android Development APK builds successfully. The current `BOKS-Development.apk` is about 127 MB because it is a Development IL2CPP build; game content itself is small.
- Player scenes are exactly `BOKS_MainMenu` followed by `BOKS_Campaign`. The player campaign is Levels 1-10 only; `BOKSCampaignController` caps progression at Level 10 and rejects out-of-range loads.
- `BOKS_LevelEditor`, archive/prototype scenes, and Level 11+ are excluded from player content. The prior custom Level 11 definition is retained outside Unity runtime assets at `Documentation/CustomLevels/level-11.json`.
- The gameplay UI uses Unity pointer drag/drop interfaces and `InputSystemUIInputModule`, which supports touch operation without mouse hover, right click, keyboard, or Unity Editor code.
- Main Menu and Campaign use the 520 x 1000 portrait reference canvas with `Scale With Screen Size`.

### Android fixes awaiting device verification

- **Orientation:** the physical device showed reverse portrait, and the generated APK manifest declared `reversePortrait`. Player Settings are fixed Portrait with portrait-upside-down and landscape disabled and autorotation off. `BOKSAndroidManifestOrientationPostprocessor` is editor-only and forces the generated Android game activity to standard `portrait`.
- **Level transition:** the iris was static on Android. The likely cause was shader stripping because `BOKS/LevelTransition` was found only at runtime. It is now in Always Included Shaders. Development logging in `BOKSLevelTransition` records shader support, graphics API, close/open state, and hole radius.
- **Important:** no new APK has been built after these two fixes.
- Package identifier is `com.maurizio.boks`; Android uses IL2CPP ARM64, min SDK 26, latest installed target SDK, no custom signing, and debug minification disabled.

### Next session priority

1. Rebuild with `BOKS > Build Android Development APK`.
2. Install the new APK on the physical Android phone.
3. Verify normal upright portrait without rotating the phone 180 degrees.
4. Verify the Level 1 -> 2 iris visibly closes, reaches cover, then reopens with the existing timing. Inspect `[BOKS TRANSITION]` logs if needed.
5. Only after physical-device verification, decide whether further Android fixes are needed.

### Constraints

- Do not refactor gameplay, optimize assets, touch the Level Editor, or make further release-build changes until Android behavior is verified on device.
- GitHub preparation is intentionally postponed until tomorrow. Do not start GitHub work tonight.

---

## Superseded menu/audio handoff (historical context only)

### Current implementation state

- `BOKS_MainMenu` is the first build scene and leads into the existing `BOKS_Campaign` Level 1 flow.
- This build exposes only Challenge/Campaign. Free Play and Tutorial are intentionally not implemented.
- The menu is built by the scene-owned `BOKSMainMenu` component in `Assets/Scenes/BOKS_MainMenu.unity`; do **not** restore a runtime-created fallback `BOKSMainMenu`, which previously created an unassigned duplicate instance.
- The menu contains the BOKS logo and the Challenge bubble. Bubble idle motion, mouse/touch input, pop VFX/SFX, and the menu-to-Campaign transition are working.
- The logo is decorative only. Its `Image.raycastTarget` must remain `false`; do not add a `CanvasGroup` that can block or intercept Challenge-bubble input.
- The menu logo uses `Assets/BOKS/RuntimeAssets/boks-logo.png`, is fixed above the bubble at `(0, 230)` with size `(336, 154)`, and must animate opacity only (no position or scale motion).
- The campaign has its existing soft Level 1 reveal and the BOKS HUD logo remains independent of menu runtime logic.
- `boks-logo.png` is imported as Sprite (2D/UI), **Multiple** mode. The valid `boks-logo_0` Sprite subasset ID is `7465518780093689404`. The Main Menu field and Campaign/Levels 2–5 HUD Images must use that subasset; do not change them back to `21300000` while the importer remains Multiple.
- Existing Campaign logo references restored in `BOKS_Campaign.unity` and `BOKS_Level02` through `BOKS_Level05` use the original coloured logo asset.

### Menu audio flow under active verification

- `BOKSAudioManager.StartMenuMusic()` uses the existing `Level01Intro` clip (`level_01_intro_main.ogg`) as a looping menu bed.
- When Campaign loads after a menu entry, the menu intro is faded out over `0.25s`, then `Game Loop Main` (`game_loop_main.mp3`) starts directly. The Level 1 intro must not replay and the two tracks must not overlap.
- Direct Campaign launches retain the normal non-looping Level 1 intro-to-game-loop sequence.

### Remaining polish / live Play Mode verification

1. Confirm the initial Main Menu logo fade reads as a gentle `1.3–1.5s` opacity-only fade.
2. Confirm `Level01Intro` plays and loops while `BOKS_MainMenu` is idle.
3. Confirm entering Level 1 from the menu does not replay `Level01Intro`.
4. Confirm Level 1 starts directly with `Game Loop Main` after the menu handoff.
5. Confirm menu and gameplay music never overlap during the handoff.

### Regression safeguards

- Preserve `Final Menu Composition` input gating: its `CanvasGroup.blocksRaycasts` becomes true only after its fade, and the shell `Button` becomes interactable at the same point.
- Do not alter Bubble idle, pop, SFX, input, Campaign transition timing, or Campaign UI while polishing the menu.
- Preserve the persistent menu-to-Campaign gate and Campaign-owned soft Level 1 reveal; cleanup must not access destroyed menu-scene UI.
- Check the actual Game View and Console in Play Mode after any menu/audio change. Compilation alone does not validate UI hierarchy, raycasts, or audio handoff.

### Relevant files changed in this session

- `Assets/BOKS/Scripts/BOKSMainMenu.cs` — menu construction, logo fade/presentation, Challenge interaction, menu-to-Campaign handoff.
- `Assets/Scenes/BOKS_MainMenu.unity` — scene-owned menu component and serialized logo Sprite field.
- `ProjectSettings/EditorBuildSettings.asset` — Main Menu first in build order.
- `Assets/BOKS/Scripts/BOKSAudioManager.cs` — menu intro music and direct gameplay-loop handoff.
- `Assets/BOKS/RuntimeAssets/boks-logo.png.meta` — original logo Sprite import mode/subasset identity.
- `Assets/Scenes/BOKS_Campaign.unity`, `BOKS_Level02.unity`–`BOKS_Level05.unity` — HUD logo Sprite references restored after the importer change.

## Current stable baseline — before campaign Levels 1–10

The current Unity project is stable and checkpointed. Preserve this baseline before starting further campaign work.

### Recent completed changes

- Restored `BOKSCharacterRoot` to the board’s top-left coordinate space with centered pivot.
- Preserved the `Root → Visual → Art` character hierarchy and `(0,-4)` art fit offset.
- Fixed placed commands so Forward, Left and Right retain their real `BOKSCommandType` through placement, movement, swapping, removal and re-adding.
- Fixed turn startup so the visible pre-Play pose remains the first animation pose without directional sprite popping.
- Verified all eight Left/Right direction transitions and zero root-position drift.
- Added centralized `BOKSAudioManager` with independent Music and SFX control.
- Imported the original BOKS music and SFX with source-derived gains and semantic cue triggering.
- Implemented gameplay intro → looping music and the original failure/success audio ordering.
- Fixed silent Play Mode audio by providing exactly one persistent active `AudioListener`.
- Verified live music, Play, Forward and drag/drop playback paths with loaded clips and active 2D AudioSources.
- Full regression suite passes: `26/26`.

### Stable checkpoint

- Commit: `011f8fa48ad6986ba3fddb0831f745140023f89c`
- Message: `Checkpoint - audio and gameplay stable before campaign 1-10`
- Working tree was clean at checkpoint creation.

### Constraints for the next task

- Preserve current gameplay, grid placement, turn behaviour, audio timing and listener lifecycle.
- Keep the data-driven shared controller architecture.
- Use the original web project and `UnityExport` as the source of truth.
- Run the complete regression suite after changes.
- Do not perform unrelated refactors.

### Next work

Start with the five live Play Mode verification/polish items in the Session handoff above. Do not start Free Play, Tutorial, or unrelated Campaign work until that menu logo/audio handoff is verified with zero Console errors.
