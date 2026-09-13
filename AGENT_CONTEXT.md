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
- Campaign smoke test implemented (Tests/PlayMode/BOKSCampaignSmokeTests.cs)
- Full current test suite: 27/27 passing
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

## Currently deferred visual polish (do NOT start yet)
- Goal bubble idle / pop VFX
- BOKS eye movement / blink / idle animation
- Bee / background animation
- Per-level theme overrides
- Available-block glow
- Minor decoration fidelity (e.g. tree tinting)

## Verification requirements
After any change:
- compile
- run the full test suite (currently 27 tests)
- preserve Level 2 regression and the campaign smoke test
- report files changed and root cause
- avoid unrelated refactors
