# CURRENT TASK

## Current priority — Audio integration

### Goal

Reproduce the original BOKS web music and SFX behaviour in Unity without changing gameplay logic or visual layout unnecessarily.

### Source of truth

Use:

- the original web project
- `UnityExport`
- existing audio analysis/spec files

Do not invent sounds or timing when the original implementation can be inspected.

### Scope

Implement a centralized Unity audio system with:

- separate Music and SFX channels
- semantic audio cue triggering
- original source audio files
- source-derived gain/volume values where available

### Events to cover

- block detach
- valid block drop
- slot hover
- Play press
- Forward step
- Left turn
- Right turn
- blocked Forward / effort
- failure
- annoyed reaction if used by the source
- goal bubble bounce
- bubble pop
- level complete
- welcome/menu

### Music

Support:

- gameplay intro
- looping gameplay music

Match the source behaviour for intro, looping and transitions.

### Constraints

- preserve current gameplay behaviour
- preserve current timings
- audio must not alter gameplay logic
- do not wait for clip length unless the source explicitly requires it
- keep Music and SFX independently controllable
- do not do unrelated feature work

### Acceptance criteria

- each cue is triggered by the correct semantic event
- timing/order matches the source as closely as practical
- volumes follow source-derived gain values where available
- overlapping cues are supported where required
- existing gameplay regressions still pass

### Deliverables

After implementation, report:

- files changed
- imported audio files
- cue → clip mapping
- gain values
- exact event timing/order
- overlapping cue behaviour
- any differences from the web source
- test results
