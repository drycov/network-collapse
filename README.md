# Network Collapse

Unity 6 prototype for GitHub Game Off 2026: keep a city's network online despite congestion and failures.

## Requirements

- Unity 6.3 (6000.3.x)
- Web Build Support module for browser export
- Active Input Handling set to **Input Manager (Old)** or **Both** (the prototype uses `UnityEngine.Input`)
- UI Toolkit (built into Unity 6)

## Run

1. Clone the repository and open it in Unity Hub.
2. Open any scene, or choose **Network Collapse → Create Main Scene** if the editor menu is available.
3. Press Play. `NetworkGame` automatically bootstraps after scene load when absent.
4. Click a node to select it. Use the equipment panel to fail or repair it (15 credits).

## Controls

| Control | Function |
| --- | --- |
| Left click | Select node and inspect details |
| Right or middle mouse drag | Pan camera |
| Mouse wheel | Zoom around cursor |
| F | Center map and reset zoom |
| Space | Trigger random outage |
| R | Reset game |
| Equipment panel | Trigger outage or repair selected node |

## Architecture

- `NetworkSimulation.cs`: network model, routing, load and economy
- `NetworkGame.cs`: composition root and input orchestration
- `NetworkMapView.cs`: sprite-based nodes, link renderers and traffic particles
- `NetworkCameraController.cs`: orthographic camera pan/zoom/reset
- `NetworkHud.cs`: runtime UI Toolkit operations and device inspector

No imported art assets are needed. Nodes and traffic particles are created procedurally.

## Build

File → Build Profiles → Web → Switch Profile → Build. Publish the exported Web build to itch.io as an HTML game with `index.html` at the root of the uploaded ZIP.

## Remaining work

- Unity Editor and Web compilation must be performed locally (not available in the connected environment).
- Add responsive/mobile HUD, keyboard shortcuts customization, audible feedback and tutorial.
- Improve traffic routing: track actual packet flow by direction and remove order bias.
- Add real repair times, link-level actions, player objectives and progression.
- Check official Game Off theme on November 1 and adapt gameplay accordingly.
