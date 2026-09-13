# CURRENT TASK

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

No new implementation task has been assigned yet. The next planned phase is campaign Levels 1–10.

Current priority — Main Menu + campaign entry flow
Reproduce the original BOKS web menu/start gate, campaign start/resume behaviour, welcome audio and menu-to-game transition. Preserve the completed Levels 1–10 campaign and existing transition/audio systems
