# Network Collapse

Unity 6 proof-of-concept for GitHub Game Off 2026. Network infrastructure strategy game.

## Run

1. Open this directory via Unity Hub using Unity 6.3 (6000.3.x), with WebGL Build Support installed.
2. In the Unity editor choose **Network Collapse → Create Main Scene**.
3. Press **Play**. The simulation also bootstraps itself into an otherwise empty scene.
4. Click a node to fail it; click again to repair for 15 credits. Space or the UI button triggers a random failure.

## Build

Open File → Build Profiles → Web → Switch Profile → Build. Upload the exported web build as an HTML game to itch.io, ensuring `index.html` is at the ZIP root.

## MVP scope

- 18 nodes with ring and crosslinks, deterministic generated topology
- shortest-hop traffic delivery to the core node
- finite link bandwidth and availability KPI
- manually triggered failures and credit-cost repairs
- no external art dependencies

## Known limitations

The bootstrap UI is temporary IMGUI; a future version should implement UI Toolkit, a proper input abstraction, deterministic traffic simulation tests and a distinct rendering layer. Unity editor/WebGL compilation has not been validated in this environment.

The official Game Off 2026 theme will be announced on November 1. Gameplay should be adapted to it before final submission.
