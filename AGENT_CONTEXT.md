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

Do not guess when the web implementation can be inspected.

## Current architecture
- Unity 6.6
- One data-driven gameplay system
- Level 2 converted to BOKSLevelDefinition / JSON-driven data
- Drag/drop command system
- Direction state via BOKSDirection
- Root/visual/art hierarchy for BOKS character
- Tests currently cover Level 2 and turn behaviour

## Important invariants
- Do not create one controller per level.
- Do not create one scene per level unless explicitly requested.
- Keep existing Level 2 behaviour intact.
- Character root position is gameplay position and must not move during turns.
- Visual turn animation happens on the visual child.
- Direction is logical state, not permanent transform rotation.
- Existing timings should match web source.
- Preserve working drag/drop behaviour.

## Character hierarchy
BOKSCharacterRoot
└── BOKS Visual
    └── BOKS Art

Root:
- top-left board coordinate system
- centered pivot
- stable during turn

## Known current bugs / work in progress
1. Turn command blocks lose their type after drop and become Forward.
2. Turn execution starts from the wrong visual orientation and causes a visible pop before rotation.

## Current task
Fix only those two bugs before continuing feature work.

## Verification requirements
After any change:
- compile
- run relevant tests
- preserve Level 2 regression
- report files changed
- report root cause
- avoid unrelated refactors