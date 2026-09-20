# BOKS Unity Port - Agent Context

## Project paths
Unity:
C:\Users\maurizio\Documents\BOKS-Unity-Demo

Original web source:
C:\Users\maurizio\Documents\boks

Migration/reference:
C:\Users\maurizio\Documents\boks\UnityExport

## Source of truth
The original web BOKS implementation is the source of truth for:
- gameplay behaviour
- level data
- timing
- SFX
- UI micro-interactions
- character turn behaviour
- level transitions
- bubble / character / bee VFX

Do not guess when the web implementation can be inspected.

## Current architecture (verified)
- Unity 6.6 (6000.6.0f1)
- One shared, data-driven gameplay system (BOKSLevel2Controller + BOKSLevelDefinition)
- Full data-driven campaign Levels 1-10 implemented (Resources/BOKS/Data/campaign-levels.json; controller caps at 10)
- Automatic campaign progression 1 -> 10; Level 10 completes the campaign (no Level 11)
- Function/sub-routine command implemented (function slots expand in ExecuteProgram)
- Left/Right/Forward turns and obstacle (blocked-move) behaviour implemented
- Centralized audio system implemented and working (BOKSAudioManager: Music + SFX, semantic cues)
- Source-faithful level transition implemented (BOKSLevelTransition + BOKS/Shaders/BOKSLevelTransition.shader)
- Campaign scene: Assets/Scenes/BOKS_Campaign.unity
- Main Menu scene: Assets/Scenes/BOKS_MainMenu.unity (first build scene; Challenge/Campaign only)
- Main Menu entry is scene-owned by BOKSMainMenu; do not create a runtime fallback duplicate.
- The original logo asset is Assets/BOKS/RuntimeAssets/boks-logo.png. It is a Multiple Sprite import; use boks-logo_0 (fileID 7465518780093689404) for menu and Campaign HUD references.
- Decorative logo Images must not receive raycasts.
- Campaign smoke test implemented (Tests/PlayMode/BOKSCampaignSmokeTests.cs)
- Last recorded full test suite: 27/27 passing; rerun after the current menu/audio work before declaring a new baseline.
- Current Unity compile state clean (0 errors)

## Important invariants
- The web game is the source of truth for behaviour, timing and data.
- One shared gameplay system; do NOT create one controller per level.
- Do NOT create one scene per level unless explicitly requested.
- Levels are data-driven (BOKSLevelDefinition / JSON); keep existing Level 2 behaviour intact.
- Character hierarchy: Root -> Visual -> Art.
  - Root: top-left board coordinate system, centered pivot, stable during turns.
  - Visual child carries the turn animation.
  - Art child carries the directional sprite / fit offset.
- Direction is logical state (BOKSDirection), not permanent transform rotation.
- Preserve source timings and behaviour.
- Preserve working drag/drop behaviour.
- Avoid unrelated refactors.

## Character hierarchy
BOKSCharacterRoot
└── BOKS Visual
    └── BOKS Art

## Current menu handoff
- Main Menu -> Campaign retains the existing pop and gate transition, then uses the Campaign-owned soft Level 1 reveal.
- The logo remains fixed above the Challenge bubble and fades opacity only.
- Current menu-music implementation uses Level01Intro as a loop, then fades it out before Game Loop Main starts on Campaign entry. This requires live Play Mode verification; see CURRENT_TASK.md.

## Android handoff
- Android Development APK builds successfully and is about 127 MB because it is a Development IL2CPP build; runtime game content is small.
- The Android player includes only `BOKS_MainMenu`, `BOKS_Campaign`, and campaign Levels 1-10. Level Editor, archive/prototype scenes, and Levels 11+ are not player content.
- The installed device build exposed reverse portrait: its generated manifest declared `reversePortrait`. Player Settings are fixed Portrait with autorotation/other orientations disabled, and the editor-only `BOKSAndroidManifestOrientationPostprocessor` now forces the generated game activity manifest to standard `portrait`.
- The Android iris transition was static. The likely cause was runtime-only `Shader.Find` allowing `BOKS/LevelTransition` to be stripped. The shader is now Always Included; Development builds log shader support, graphics API, transition state, and hole radius.
- A new APK has not yet been built after these two fixes. Next session must rebuild and physically verify upright portrait and the Level 1 -> 2 iris before any further Android work.
- Do not refactor gameplay, optimize assets, touch the Level Editor, or make further release-build changes before that device verification. GitHub preparation is postponed until tomorrow.

## Verification requirements
After any change:
- compile
- run the full test suite (currently 27 tests)
- preserve Level 2 regression and the campaign smoke test
- report files changed and root cause
- avoid unrelated refactors
