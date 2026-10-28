# Free asset shortlist for Network Collapse

All links point to the publisher or source page. Download and import packages selectively into Unity; this repository does not include third-party binary files.

| Asset | Use | Source | License stated by publisher |
|---|---|---|---|
| Kenney UI Pack - Sci-Fi | borders, panels, buttons, HUD | https://kenney.nl/assets/ui-pack-sci-fi | CC0 |
| Kenney UI Pack | generic buttons and controls | https://kenney.nl/assets/ui-pack | CC0 |
| Kenney Interface Sounds | interaction clicks, confirmations, warnings | https://opengameart.org/content/interface-sounds | CC0 |
| Kenney Sci-Fi Sounds | ambient electronic effects and alarms | https://opengameart.org/content/sci-fi-sounds | CC0 |

Visual direction: dark network-operations console, cyan/amber telemetry, opaque flat windows, typography and procedural router/switch symbols. Inspired by classic terminal-based games, **do not copy Uplink UI graphics or proprietary audio**.

Import instructions:
1. Download the source archive from the publisher.
2. Preserve a LICENSE/source note next to any imported files.
3. Put PNG UI elements in `Assets/_Game/Art/ThirdParty/Kenney/` and audio clips in `Assets/Resources/Audio/` according to `NetworkAudio.cs` clip-name conventions.
4. Adjust import settings and preview in Unity Editor before committing assets.
5. Only use source files from sources where redistribution of the selected contents is permitted.

Creative Commons CC0 assets are suitable for commercial use according to the linked publisher pages. Brand marks/logos and third-party items within downloaded archives, if any, must be reviewed separately.

## One-click import in Unity Editor

Open **Network Collapse → Import Free CC0 Assets** to download the Kenney CC0 UI Sci-Fi and Interface Sounds zip archives from OpenGameArt. The importer selects PNG panel/button images and OGG sounds, placing them in `Assets/Resources/NetworkTheme` and `Assets/Resources/Audio`. `NetworkHud` reads the optional panel/button textures; `NetworkAudio` reads matching sound cue filenames and falls back to generated audio if they are missing. Review chosen texture/sound variants in Unity before release, because the importer matches filenames heuristically. This command must run in Unity Editor with network access; binary assets are not bundled in GitHub until you import and commit them. The import has not been tested in Unity Editor.
