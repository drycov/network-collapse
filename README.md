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
| A | Auto-repair critical failures (nodes + important links) |
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

## Changelog (2026-10-29)

### Simulation core
- **Residual-capacity traffic allocation**: path selection and multipath split now reserve against a per-tick residual bandwidth array instead of mutating `link.Load` mid-pass. Removes residual order bias when many access nodes compete for the same uplink.
- **Customer churn**: prolonged availability below 85% / 70% causes gradual subscriber loss (after early grace period). Makes availability matter for long-term economy.
- **Auto-repair critical** (`AutoRepairCritical`): prioritises Core → Distribution nodes, then high-capacity / player-built links. Starts up to 3 timed repair jobs per call. Exposed in Operations panel and hotkey **A**.

### Persistence
- Network snapshot now carries `Version = 2`. Restore accepts older saves (Version 0/1) and rejects future versions.

### Controls
| Control | Function |
| --- | --- |
| **A** | Auto-repair critical failures (nodes + important links) |

### Still outstanding
- Full Editor / WebGL compile & visual playtest
- Save migration helpers for future schema changes
- Stronger cascade incidents and repair-crew limits
- Mobile-responsive HUD layout validation

## Remaining work

- Unity Editor and Web compilation must be performed locally (not available in the connected environment).
- Add responsive/mobile HUD, keyboard shortcuts customization, audible feedback and tutorial.
- Improve traffic routing: track actual packet flow by direction and remove order bias.
- Add real repair times, link-level actions, player objectives and progression.
- Check official Game Off theme on November 1 and adapt gameplay accordingly.


## Interactive gameplay

- **Core / Distribution / Access** equipment categories with tier colors and capacity.
- Select node by left-click; press **B** and click second node to plan a link.
- Right panel: build new link (35), upgrade capacity (25), or initiate repair.
- Left panel: fiber cut, power outage and cascading incident scenarios.
- **F** resets camera; right/middle drag pans and wheel zooms.
- Unity Editor: **Network Collapse → Create Main Scene** generates `Assets/_Game/Scenes/Main.unity` and adds it to Build Settings. Save the generated scene in version control.
- Playable logic is written; Unity compilation and WebGL runtime still require local verification.

## Troubleshooting: CS1069 UIElements

When `UIDocument`, `VisualElement`, `Label` or `Button` cannot be found in `UnityEngine.UIElements`, the Unity built-in UIElements module is unavailable to the compiler. Both `com.unity.modules.ui` and `com.unity.modules.uielements` are explicitly enabled in `Packages/manifest.json`.

1. Close the Unity Editor.
2. Run `git pull origin main`, verify the manifest contains both modules.
3. Open the project in the Unity version listed in `ProjectSettings/ProjectVersion.txt` (currently 6000.6.7f1).
4. In Package Manager select **Packages: Built-in**, find **UIElements** and ensure it is enabled. Do the same for **UI**.
5. If the editor still uses stale package metadata, close it, delete `Library/PackageCache` and `Library/ScriptAssemblies` (not `Assets`, `Packages`, or `ProjectSettings`), then reopen the project.

Do not install a separate `com.unity.ui` package for these errors. UIElements is a built-in editor module. A GitHub manifest change does not change a running local Unity Editor until the project is updated and packages are reloaded.

## Asymmetric hierarchy and device configuration

The seeded generator now builds two core routers, distribution L3 switches and access L2 switches. Device positions and redundant links are deliberately asymmetric. Select a device on the map to edit its VLAN ID, MTU, administrative status and routing-mode setting in the inspector.

Addressing conventions in this gameplay prototype:
- Management addresses use the RFC 1918 private 10/8 range.
- Simulated point-to-point link labels use /31 format from RFC 3021, but **all current links are abstract capacity edges**; VLAN-tagged L2 access links are not yet modeled as individual routed /31 interfaces.
- CIDR labels do not imply a complete RFC 4632 routing implementation.
- `OspfSimulated` is a configuration flag only; OSPF neighbors, areas, LSDB, SPF and protocol messages from RFC 2328 are **not implemented**.
- Switch VLAN and MTU settings are validated and stored, but VLAN forwarding and per-interface MTU enforcement are pending. Do not treat the current simulator as RFC-conformance-tested.

Upcoming: typed physical interfaces, VLAN-tagged trunks and access ports, IPv4 subnet validation, L3 adjacency rules, routing tables, and scenario tests.

## Network data plane and path resilience

Added `NetworkDataPlane.cs` with network interface, VLAN, subnet, route, neighbor and MAC switching entities. Existing topology links now create two interface records and one simulated /31 subnet each. These are data models, **not** a packet-level RFC implementation. Neighbor and switching tables are currently empty placeholders; control-plane STP, LACP and OSPF are not implemented.

Traffic allocation now attempts a bandwidth-eligible primary route and a disjoint alternate. When the second route can carry half the source demand, a simple 50/50 split is used. Every simulation tick reroutes from scratch, so failures can cause selection of another available path. This is **simplified multipath traffic allocation**, not standards-compliant ECMP hashing or OSPF convergence. The device inspector shows per-source primary and alternate allocated Mbps; primary is yellow and alternate purple on the map.

Unity runtime notes: `NetworkCameraController.ResetView()` now tolerates early calls. The dynamic `PanelSettings` is assigned a `ThemeStyleSheet`; Unity 6000.6 may still require a serialized panel-settings/theme asset for reliable packaged-player rendering. Editor and Web compilation have not been run.

## Terminal dispatch visual update

The interface uses an original dark command-console aesthetic influenced by the legibility and workstation feel of classic 2000s network simulation games such as Uplink. It does not reuse Uplink assets or reproduce its interface. Added a top operations status bar, squared edge-lit panels, turquoise text/buttons, a coordinate grid behind the topology, and a color legend. Unity's procedural UI Toolkit screen remains runtime-generated. The editor/Web build has not yet been visually validated.

Unity `.gitignore` excludes regenerated Library, Temp, Obj, Logs, build outputs, UserSettings and IDE files. Keep `Assets/*.meta`, `Packages` and `ProjectSettings` committed.

## Campaign and tactical device visualization

The map now draws original procedural device glyphs: router directional arrows, L3 switch crossed port grid and L2 switch port grid, in addition to status and layer colors. All glyphs are Unity LineRenderers; no external sprites or copyrighted Uplink artwork.

A six-objective story campaign tracks availability, new links, repairs, upgrades, survival after a simulated outage and device configuration. Four achievements and two consumable inventory items are displayed in the operations HUD. Mission credit rewards are applied to the simulation. Campaign state currently resets when starting a new game; persistence and saved profiles remain future work. Missions and achievements are a lightweight first playable narrative, not a complete authored branching campaign.

Unverified: this change has not been compiled, visually inspected or tested in the Unity Editor or Web build.

## Operations console UI upgrade

The runtime console has two progress gauges (availability and level objective), a live failure banner, a selected equipment/link utilization gauge, and an overlay shown on game over. Pause/resume uses **P** or the in-game control; simulation ticks stop while paused. **1 / 2 / 3** switch between Network, Missions, and Inventory screens. These are foundation pieces for a more game-ready HUD, not a complete production-ready game. The Unity 6 runtime compilation, responsive layout and visual behavior must still be validated in Editor, standalone and Web builds.

## Sequential game UX and persistence update (Unity 6)

1. Adaptive HUD: width-sensitive left/right operations panels and smaller status-header typography.
2. Dispatcher dialogue: mission briefing modal, opened from Missions and on first start.
3. Settings: fourth HUD tab with persisted volume, mute and low-graphics 30 FPS mode. Shortcuts 1–3 still select Network/Missions/Inventory.
4. Save/load: the existing PlayerPrefs JSON campaign save now has a companion network snapshot (node configuration, failures, repairs, link topology/capacity, economy, score and level). A snapshot is restored on startup. "Start a new shift" discards both saves and starts fresh; simulation-generated route tables are rebuilt after loading.
5. Tutorial: first-time guided steps for selecting a device, opening missions and building a link; completion is tracked with PlayerPrefs.
6. Balance: early funds increased to 180, initial upkeep softened, and early levels have a less severe game-over threshold.

Limits: save versioning/migration is not implemented; RNG state, packet-flow animation phases, current voice clip and precise in-progress routing flow allocations are not saved. A fresh deterministic routing pass computes the post-load display/availability. Dialogues are briefing overlays rather than branching conversations. Responsive layout requires validation at multiple resolutions, and graphics mode is a simple frame-rate/vsync preference rather than full quality presets. Unity Editor compilation and player builds were not run.

## Engineering-grade line construction (game abstraction)

Select a source and destination device in build mode. The device inspector now offers **Copper**, **Fiber** and **Radio** media and selectable speeds (10/50/100/150/500/1000 Mbps). The game checks available endpoint ports, link length, requested media/speed and credits before accepting a job. A detailed quote combines base work, per-distance materials, transceiver/radio hardware, and capacity. Build time depends on distance and medium; a new channel is **not routed or animated** until commissioning completes. An in-progress link is orange on the map. Medium-dependent repair prices and times apply after incidents. The configuration is included in the existing network snapshot.

These are deliberately normalized game units, not real-world copper/fiber distance engineering specifications. Port inventories are modeled as counts rather than full pluggable/PHY compatibility; RF line-of-sight, attenuation, splices, licensing, permits and actual materials inventory are future work. Current UI speed choices may be rejected for an incompatible medium; select a supported combination. Compilation and Unity Editor playtesting still required.

## ISP operator career and procedural expansion

A new game now starts as a small ISP: one core gateway, one aggregation switch, three access sectors and 12 simulated customers. **Network → Open new district** purchases a new procedural access, distribution or (later) core node and a connecting link, preserving existing topology. The camera reframes the expanded graph. **Marketing campaign** costs 20 credits and increases subscribers and access demand, subject to the access node's capacity. Revenue gains a simplified per-customer contribution, adjusted by availability. Operator status advances from local through regional and national to transnational based on subscribers; it is a game progression tier, not actual intercontinental geography, ASN or transit contracts.

Select a device to diagnose power, admin state, adjacent failed links and routes. **NOC / Scan entire network** lists the first six outages, congestion warnings and construction jobs. Existing node/link repairs remain player controlled, with their own costs and timers. Expansions and customer growth are captured by the network snapshot.

Current limitations: abstract geometry in game units (not a geographic world map), no real subscriber address/contract entities or churn, no field-service dispatch automations, no realistic financial ledger or international peering simulation. Unity Editor compile and visual tests are still required.

## World market / competitors / interconnection

The **РЫНОК** HUD tab introduces a fictional city economy and ISP expansion:
- **Cities and territory:** open coverage in domestic cities and a foreign city. International expansion requires an active International Carrier contract.
- **Tariff contracts:** three starter tariffs (50/100/500 Mbps) and city-level customer batches; acquisition consumes budget and the access layer must have spare capacity. Subscribers add simplified billing income.
- **Competitors:** MetroNet, FiberUnion and BorderLink can be acquired via a legal market transaction. Price, operator reputation and migration capacity gate acquisition. This is **not hacking, hostile network takeover or real-world intrusion**; acquired assets and subscribers join the player business.
- **Transit/peering:** simulated carrier/IX agreements carry autonomous system numbers and recurring fees. The game uses private ASN 64512 and fictitious counterpart ASNs. This is a commercial BGP-inspired abstraction; there is **no real BGP FSM, RIB, route advertisements, AS-path selection or actual Internet transit** yet.
- **International links:** foreign coverage and international carrier contracts are tracked as campaign progression; geographic fiber paths and cross-border capacity engineering are not implemented.

World market is saved separately as `NetworkCollapse.World.v1` (Unity PlayerPrefs JSON), restored alongside the physical network, and deleted when creating a new shift. Advancing the game level preserves constructed and expanded infrastructure instead of recreating the starting graph.

Limitations: initial geography and rivals are authored data rather than procedural geopolitical world generation; acquisition is all-or-nothing with no negotiations; no regulatory approval, antitrust, recurring competitive AI, individual customer records, actual BGP protocol, route policy, or executable attack mechanics. Validate in Unity Editor before use.

## Autonomous ISP world simulation (strategic layer)

- A deterministic procedural world seed generates 12 playable cities across four fictitious countries, with map coordinates and population. The **Market** screen includes a clickable positional city overview; this is an abstract strategic map, not a geographical basemap.
- Competitors have independent ASNs and lists of physical site coordinates. Every fifth economic tick they recruit new subscribers, potentially grow their facilities and can take market share from an operator with poor availability.
- An acquisition negotiation proposes 120% of the base valuation. Acceptance uses seller reputation and player reputation; failed proposals increase the seller price. After accepted terms, the purchase uses the quoted multiplier. This is a simplified legal M&A mechanic, not an unauthorized takeover.
- The BGP-inspired route display presents destination prefixes, fictional AS paths, reachability and local preference based on active partner links. It is a **simulation of routing policy metadata**, not a real BGP protocol implementation, packet propagation or RFC-compliant convergence.
- New market state (map coordinates, competitor sites, proposals and simulation ticks) is serialized by the existing world JSON save. Older saves without these fields may contain empty generated-site lists and require a new shift to generate the complete world.
- Economic ticks use the existing two-second simulation cadence. This is deliberately accelerated strategy-game time.

**Not yet implemented:** geographic polygon boundaries, rendering independent rival equipment on the main network map, bidding rounds/escrow and regulation, multi-route BGP best-path calculation, routing across simulated international capacity constraints, realistic customer contract billing and churn. Unity Editor compilation and UI playtest remain outstanding.

## Integrated M&A topology and game windows

Purchasing a rival now provisions its saved site coordinates as new owned distribution/access devices on the **same physical network graph**, with backhaul links to an existing distribution point. Some acquired assets start failed and require engineering repairs, while the portfolio incurs recurring acquired-site maintenance each simulation tick. Acquired sites, liabilities, link failures and subscriber totals are stored in the existing network snapshot. This is a deterministic game abstraction: map coordinates are transformed into the local engineering view, not geographically accurate distances. Existing worlds without rival NetworkSites should start a new campaign for full acquisition behavior.

The Network, Missions, Inventory, Market and Settings navigation now works differently: **Network** remains in the left dock; **Missions**, **Inventory**, **Market** and **Settings** open separate centered scrollable windows over the physical map with explicit close controls. The inspector remains contextual. Escape opens the main menu, and the world view is blocked from receiving clicks under modal windows.

Known limitations: imported networks currently have a simple hub/backhaul structure, not the rival's full pre-acquisition routing table. Network ownership and maintenance are reflected through physical equipment and financial upkeep, rather than separately identified subsidiaries/maintenance contracts. Unity Editor compilation and visual verification are still needed.

## Subsidiaries and acquired-network traceability

Each integrated competitor's physical devices now retain the original operator name and fictional ASN. The world save keeps a subsidiary registry (city, original ASN, number of sites and transferred subscribers, recurring maintenance obligation, inherited-fault estimate). The Market game window shows that portfolio, and the node inspector identifies acquired assets. Acquired-network maintenance continues to be charged by the physical network simulation. Escape closes secondary game windows before invoking the pause menu.

The former rival's actual BGP RIB, historical monitoring events, and per-asset service contracts are not yet imported. Build validation in Unity Editor remains outstanding.

## Acquisition integration v2

Rival operators now have pre-acquisition site-to-site links (endpoints, capacity, and fault state), local ASN routes, inherited upstream transit agreements, and technician counts. Acquisition imports those original links into the physical network with new local IDs, keeps failed links failed, establishes one upstream interconnection, and records the inherited routes/contracts in a subsidiary. Subsidiary routing entries appear in the BGP-inspired RIB, with legacy AS paths; transit contracts charge ongoing fees in the world economy. The Market window lists each subsidiary and its status.
