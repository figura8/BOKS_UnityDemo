# BOKS Unity Demo

This project is a Unity recreation of the BOKS programming-puzzle experience, prepared as a compact, data-driven interview demo.

## Main scenes

- `Assets/Scenes/BOKS_MainMenu.unity` — application entry point.
- `Assets/Scenes/BOKS_Campaign.unity` — campaign gameplay.
- `Assets/Scenes/BOKS_LevelEditor.unity` — desktop level authoring and testing.

Levels are data, not individual scenes. Campaign definitions live in `Assets/BOKS/Resources/BOKS/Data/campaign-levels.json`. The Level Editor edits and tests the same live board, command UI, program slots, and gameplay runner used by the campaign.

## Folder map

- `Assets/BOKS/Source` — canonical and original source artwork.
- `Assets/BOKS/RuntimeAssets` — runtime-ready derivatives from source artwork.
- `Assets/BOKS/Resources` — assets and data loaded at runtime.
- `Assets/Scenes` — primary, presentable scenes.
- `Assets/Scenes/Archive` — prototype and milestone scenes retained for reference.

Open `BOKS_MainMenu` for the normal game flow, or `BOKS_LevelEditor` to author, save, and test data-driven levels.
