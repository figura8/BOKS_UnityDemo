# BÖKS Technical Roadmap

## Goal

Take the current project from a functional development build to a robust cross-platform mobile release without unnecessary architectural rewrites.

BÖKS targets Android phones, Android tablets, iPhone and iPad, including reasonably older/lower-end devices. Its architecture is generally appropriate for a small mobile game. This is a hardening roadmap, not a rewrite plan: focus on concrete shipping, performance, robustness and cross-platform risks identified in the technical audit.

Audit findings describe the inspected implementation, not confirmed failures on every device. Reinspect current code and settings before acting. Writing this document does not implement or authorize any fixes.

## Engineering principles

- Measure before optimizing.
- Prefer the smallest safe fix.
- Do not redesign working systems without evidence.
- Keep Android and iOS behaviour aligned.
- Separate device type, orientation and layout mode.
- Release builds are the performance reference.
- Avoid premature systems such as ECS or Addressables unless future evidence justifies them.

## Phase 1 — Fix now

### 1. Orientation and responsive layout

Original audit findings (layout separation implemented; Android native-startup decision and physical-device validation remain open):

- PlayerSettings, runtime and Android manifest orientation policy is inconsistent.
- The current default orientation is `PortraitUpsideDown`.
- Android manifest postprocessing forces portrait.
- Runtime layout currently assumes phone = portrait and tablet = landscape.
- Layout composition is selected mainly from device classification rather than actual available safe-area dimensions.

Desired outcome:

- One explicit orientation policy shared across Android/iOS.
- No contradictory startup/runtime orientation settings.
- Device type, orientation and layout composition treated independently.
- Existing portrait and landscape compositions selected using available screen/safe-area dimensions where appropriate.

Acceptance criteria:

- Android phone behaves correctly.
- Android tablet behaves correctly.
- iPhone behaves correctly.
- iPad behaves correctly.
- No upside-down launch.
- Orientation does not visibly correct itself after startup.
- Manual layout-test overrides continue working.

Implementation (2026-10-02):

- Separate device class, product orientation preference/request, actual viewport and UI composition.
- Phones request upright portrait; tablets request either landscape direction. Never request portrait upside-down.
- Apply the runtime preference once at Unity's earliest managed `BeforeSplashScreen` hook (retry at the existing first-scene entry only if native device data is not ready); scene/layout refreshes do not re-request orientation when the OS supplies a different viewport.
- Select the existing landscape composition when safe-area width / height >= 1.20; otherwise select the existing portrait composition. The single tunable breakpoint is `BOKSDeviceLayout.LandscapeCompositionAspectThreshold`.
- Preserve Campaign safe-area centring/uniform fitting and both authored compositions; use the current scaler reference immediately when fitting.
- Production Main Menu already has safe-area fitting from the preceding menu task and keeps its single logo/flower-bubble composition.
- Android generated manifest uses neutral `screenOrientation="unspecified"` rather than a universal portrait lock. Runtime classification first reads Android `smallestScreenWidthDp` (tablet >= 600 dp); DPI/pixel heuristics are fallback only. No custom Activity/native plugin.
- Investigated a qualified manifest integer (default portrait=1, sw600dp sensorLandscape=6): AAPT2 and a full Android build accept it, but this does not prove startup selection. AOSP PackageManager parses `screenOrientation` into a fixed ActivityInfo value using default resources, so sw600dp cannot be relied on for per-device native launch. The prototype was removed from production code; it remains only in ignored validation artifacts.
- Android OS launch-screen orientation before Unity initialization cannot be guaranteed by the managed hook. Closing the no-visible-correction requirement needs an approved startup decision; no native complexity was introduced.
- iOS build-time postprocessor writes portrait-only iPhone declarations and both landscape directions for iPad, preserving unrelated plist keys and removing legacy initial-orientation overrides.
- Unity Default Orientation is Auto Rotation with portrait and both landscape directions enabled, upside-down disabled; native build declarations narrow the policy per device.
- All three Layout Test states remain available and checked. Forced selections also choose coherent Game View dimensions; Auto preserves the selected viewport. Test selection survives Play Mode domain reload.

Validation:

- 13 automated policy tests passed (including the exact 1.20 boundary, Android 599/600 dp, iPhone/iPad classification and override independence).
- Campaign portrait 720x1600 and landscape 1280x800 inspected in Play Mode; live switching updates composition and safe-area fitting without restart. Auto landscape survives re-entering Play Mode.
- Android resource/manifest prototype compiled and linked with SDK AAPT2; packaged values default=1 and sw600dp=6 verified. Native parsing investigation rejected it as a reliable production solution.
- Final Android Development APK built successfully, without build errors, at `Builds/BOKSOrientationValidation/BOKS-Orientation.apk` (ignored artifact). AAPT2 inspection confirms packaged `screenOrientation=-1` (unspecified) and no rejected prototype resource. Existing build/tooling warnings were not fixed as part of this task.
- iOS plist fixture verified, including idempotence and preservation of unrelated keys. A real Xcode export is not available on this machine (iOS module absent).

Remaining acceptance work:

- Cold launch/splash-to-menu with no visible orientation correction on Android phone/tablet and iPhone/iPad.
- Resolve the Android OS-launch-screen requirement: the neutral manifest plus managed preference cannot guarantee device-specific orientation before Unity starts. Any native-entry-point work or acceptance of an initial correction needs a separate approved decision. AOSP evidence: [manifest parsing](https://android.googlesource.com/platform/frameworks/base/+/refs/heads/android10-release/core/java/android/content/pm/PackageParser.java) resolves `screenOrientation` to ActivityInfo with default resource configuration, not launch-time sw600dp selection.
- Physically verify no upside-down startup, both tablet landscape directions, OS-imposed viewport fallback, safe-area insets, unusual aspect ratios and background/resume.
- Inspect a real exported iOS plist/Xcode build and test iPhone/iPad; assess the breakpoint after device testing.
- Build-time declarations and Editor checks do not prove native startup/rotation behaviour.

Status: NEEDS DEVICE TEST (layout/policy implementation complete; Android native-startup decision required; not DONE)

### 2. Main Menu safe area

Current problem:

- Campaign already fits `Screen.safeArea`.
- Main Menu currently uses the full Canvas area.

Desired outcome:

- Main Menu composition respects safe area without changing the intended visual design.

Acceptance criteria:

- No important content overlaps notch, Dynamic Island, rounded corners or home indicator.
- Visual centring remains correct on Android, iPhone and iPad.

Status: TODO

### 3. Mobile lifecycle and input robustness

Current problems:

- No explicit pause/focus/background policy.
- Active drag can potentially be replaced by another drag.
- Interrupted drag has incomplete cancellation handling.

Desired outcome:

- Clean behaviour when the app loses focus or enters background.
- Only one command drag can own the interaction at a time.
- Interrupted interactions recover safely.

Acceptance criteria:

- Second finger cannot corrupt active drag state.
- Backgrounding during drag does not leave a ghost or invalid state.
- Backgrounding during level execution or transition does not break progression.
- Audio resumes in a predictable state.

Phase 1A implementation (2026-10-02) — drag ownership and cancellation only:

- One active drag is owned by its source and `pointerId`; another Begin is rejected without overwriting the source/ghost state. Non-owner drag, hover, drop and end events are ignored.
- `CancelCommandDrag()` is idempotent: clears ownership, hover and destination, hides/destroys the ghost and restores the source colour. Cancellation does not move/delete commands or invoke drop completion.
- Cancellation runs on application pause/focus loss, source or controller disable/destruction, level reset/reconfiguration, editor test restoration and immediately before an accepted Play starts execution.
- Lifecycle callbacks live in the existing controller and only cancel drag. No global lifecycle framework, audio changes, transition changes or coroutine timing changes.
- Ten new PlayMode cases passed: same-source/other-source second finger, wrong-pointer events, pause/focus cancellation, cancellation before Play, Reset, source disable/destruction and repeated cancellation/stale events.
- Full PlayMode suite: 52/53 passed, including all new drag cases, existing single-pointer placement/move/swap/outside-drop/locked-slot tests, audio integration and campaign progression. The existing Level Editor test fails because `BOKS_LevelEditor` is not in the active build profile/shared scene list; build settings were not changed. No compilation errors.
- Pause/focus tests invoke Unity callbacks in Editor; physical multi-touch, Home/app switching and screen-lock tests remain required on Android phone/tablet and iPhone/iPad.

Phase 1A status: IMPLEMENTED / NEEDS DEVICE TEST.

Phase 1B remains unimplemented: suspension/resume timing, execution/input gating beyond drag, completion/transition interruption guards, viewport changes during reveal/transition and predictable audio pause/resume. Existing behaviour is intentionally preserved for this step.

Overall Task 3 status: IN PROGRESS (not DONE).

### 4. Iris shader inclusion

Current problem:

- Iris shader is found dynamically with `Shader.Find`.
- Build inclusion is not guaranteed.

Desired outcome:

- Shader/material is explicitly referenced so stripping cannot remove it accidentally.

Acceptance criteria:

- Iris works in Android Release.
- Iris works in iOS Release/TestFlight.
- No global broad shader preservation unless required.

Status: TODO

## Phase 2 — Before beta

### 5. Measure level-transition performance

Current state:

- Transition diagnostics now exist.
- Development builds can display:
  - Clone
  - PreviewApply
  - PreviewActivate
  - ProxySetup
  - ScrollStep average/max and sampled frame count
  - LiveApply
  - Cleanup
  - GoalPop.EnsureAssets
  - Frame delta average/max

Known structural risk:

- Full visual hierarchy clone.
- Next level constructed in preview.
- Same level constructed again in live presentation.
- Old and next visual presentation coexist during scroll.

Device measurements justified a narrowly scoped optimization, now accepted after real Redmi gameplay testing (2026-10-02). The primary scroll bottleneck was UGUI Graphic/Canvas rebuild work during movement, amplified by Pixel Perfect; an explicit global Canvas.ForceUpdateCanvases() also caused a setup hitch.

Accepted implementation:

- Remove the global forced Canvas update from proxy setup.
- Capture proxy geometry after the existing natural frame-end Canvas update, without adding a wait frame.
- Disable Pixel Perfect only for the Bubble Bobble scroll and restore its exact previous value on completion and cancellation/cleanup paths.
- Preserve duration, easing, hero handoff and gameplay/progression. Keep transition diagnostics available.

Redmi device results (approximate before -> after):

- Proxy ForceCanvas: 57 -> 0 ms.
- ProxySetup: 66 -> 10 ms.
- UI graphics: 92 -> 2 ms.
- Canvas callbacks: 107 -> 5.5 ms.
- Main Thread: 122 -> 34 ms.
- Scroll frames: 13 -> 44.
- Visual result is good in real gameplay, confirmed by the device tester.

Canvas isolation, snapshot rendering and a broader transition redesign are not currently justified. Scope timings measure synchronous elapsed work, not GPU time or all deferred Unity work; nested timings must not be summed. Cleanup measures deactivation/destruction scheduling rather than all deferred destruction.

Test devices should include at minimum:

- Redmi 13 or equivalent lower-end Android.
- A representative Android tablet (validation still required).
- A representative iPhone.
- A representative iPad when available.

Prioritize transition 6 → 7 because it includes a higher number of bee decorations. Compare equivalent content and conditions across Development and Release, controlling debugging/tooling overhead.

Acceptance criteria:

- Identify whether observed stutter comes primarily from:
  - Clone
  - PreviewApply
  - UI/Canvas work
  - Scroll
  - LiveApply
  - Cleanup/GC
  - GPU/rendering
  - Development tooling overhead
- Preserve the accepted visuals, alignment, progression and exact Pixel Perfect restoration.
- Validate the accepted solution on Android tablet, iPhone and iPad; compare equivalent Development/Release conditions and decoration-heavy transitions.

Status: IMPROVED / NEEDS BROADER DEVICE TEST

### 6. Minimal persistence

Current problem:

- No persistent campaign progress/settings system currently exists.

Desired outcome:

- Small, versioned save record.

Suggested scope:

- Highest/unlocked level.
- Essential settings only.

Avoid building a large save framework.

Acceptance criteria:

- Progress survives app termination.
- Invalid/old save data fails safely.
- Future save format can be migrated.

Status: TODO

### 7. Mobile asset tuning

Current findings:

- Many textures use default/uncompressed settings.
- No Android/iOS-specific texture overrides were found in the examined set.
- No SpriteAtlas currently exists.
- Some unused Resources assets may still ship.

Desired outcome:

- Reduce memory/bandwidth where useful without damaging the clean graphic style.

Tasks should consider:

- Texture max sizes.
- Android compression.
- iOS ASTC where appropriate.
- Translucent edge quality.
- Unused Resources cleanup.
- SpriteAtlas only for frequently co-rendered sprites if beneficial.

Do not blindly atlas everything. Validate platform formats and visual quality on supported devices; asset size is not a substitute for measuring runtime memory.

Status: TODO

### 8. First-use audio spikes

Current behaviour:

- Audio is loaded synchronously on first request and then cached.

Desired outcome:

- Warm only critical interaction sounds if device testing shows a first-use hitch.

Do not redesign the audio manager.

Status: MEASURE FIRST

## Phase 3 — Before store release

### 9. Android release pipeline

Verify:

- IL2CPP.
- ARM64.
- Target SDK required by Google Play at submission time.
- Generated Android manifest.
- Orientation.
- Release signing.
- Stripping.
- Vulkan/OpenGLES behaviour.
- Clean Release APK/AAB.
- Diagnostics excluded where intended.

Acceptance criteria:

- Reproducible release build.
- Store-ready artifact.
- Tested on phone and tablet.

Status: TODO

### 10. iOS / iPadOS release pipeline

Verify:

- IL2CPP.
- Metal.
- Device architecture and minimum supported OS.
- Xcode archive using the SDK/toolchain required at submission time.
- Signing/team/profile.
- App icons.
- Info.plist orientation declarations.
- Safe area.
- iPhone.
- iPad, including applicable orientation/window-size changes.
- Shader behaviour on Metal.
- Pause/resume.
- TestFlight installation.
- Release stripping and diagnostics excluded where intended.

Acceptance criteria:

- Reproducible signed archive.
- Successful TestFlight build.
- Tested on at least one iPhone and one iPad before release.

Status: TODO

## Explicitly not planned

Unless new evidence requires them, do not introduce:

- ECS.
- Addressables migration.
- Universal UI framework.
- Major gameplay-controller rewrite.
- Complete transition rewrite.
- Broad `link.xml` preservation.
- Complex save framework.

These are currently considered unnecessary for BÖKS.

## Current technical strengths

Preserve these so future work does not accidentally replace good existing systems:

- Small data-driven gameplay architecture.
- Campaign JSON loaded once and cached.
- Shared gameplay controller.
- Input System/EventSystem is cross-platform.
- No problematic native mobile plugin dependency identified in the audit.
- Existing campaign safe-area handling.
- Existing phone/tablet layout compositions.
- IL2CPP on Android/iOS.
- Incremental GC.
- Bounded command execution.

## Working process

For future technical tasks:

1. Read this roadmap first.
2. Inspect current implementation before editing.
3. Make the smallest change that solves the documented problem.
4. Preserve existing gameplay and visual behaviour unless the task explicitly changes it.
5. Report files changed and validation performed.
6. Update this Markdown status only when the task is actually completed and verified.

Use simple status labels for subsequent updates:

- TODO
- IN PROGRESS
- NEEDS DEVICE TEST
- DONE

The initial statuses `INSTRUMENTED / NEEDS DEVICE DATA` and `MEASURE FIRST` above retain the requested measurement gates; neither means completed. Do not mark something DONE solely because code was written: it must satisfy its acceptance criteria, including required device verification.
