# Modular Vehicle Builder — Setup Guide

A step-by-step guide to wire up the modular vehicle builder so a player-built
soapbox can be built, painted, saved/loaded, and **driven by the existing
`VehicleController`** at the press of "Test".

All builder code lives under `Assets/01_Scripts/Builder/` (namespaces
`Soapbox.Builder.*`). The existing driving code under `Assets/01_Scripts/Car/`
is unchanged except for three small, backward-compatible additions (see
§11).

---

## 0. Concepts at a glance

- **PartData / PartCategory** (ScriptableObjects) — author every part's values; no hardcoding.
- **PartInstance** — runtime identity on each part prefab; also holds paint.
- **AttachmentPoint / PartAttachments** — sockets and snapping.
- **VehicleRoot** — the single object every part is parented under; the socket source.
- **PlacementController** — ghost preview, snapping, validity, commit.
- **SelectionController / PaintController** — select, highlight, paint.
- **CommandHistory + commands** — undo/redo (place, delete, move, rotate, duplicate).
- **VehicleStatsTracker / VehicleValidator** — live stats + drivability checks.
- **VehicleAssembler** — turns the build into something `VehicleController` can drive.
- **BuilderController** — the coordinator the UI/input talks to.
- **PartCatalog** — the list of all parts (browser + load lookup).

---

## 1. Let Unity compile

1. Open the project in Unity 6 (6000.5.x).
2. Wait for the scripts to compile and for the **Input Actions** asset to
   regenerate its C# wrapper (it now contains a new **Builder** action map).
3. Confirm there are no compile errors in the Console.

> The new scripts have no `.meta` files until Unity imports them — that's normal;
> Unity creates them on first import.

---

## 2. Project layers

Create two layers (Edit ▸ Project Settings ▸ Tags and Layers):

- **Ground** — the build platform / floor.
- **Parts** — all placeable parts.

These let placement and selection raycast precisely.

---

## 3. Materials

Create three materials (Project ▸ Create ▸ Material). Using URP/Lit (or your
toon shader) is fine.

| Material | Purpose | Suggested look |
|---|---|---|
| `M_PreviewValid` | Ghost when placement is valid | translucent green |
| `M_PreviewInvalid` | Ghost when placement is invalid | translucent red |
| `M_Highlight` | Selected-part highlight | bright/emissive tint |

---

## 4. Part categories (ScriptableObjects)

Create one asset per category: **Create ▸ Soapbox ▸ Builder ▸ Part Category**.
For each, set a unique **Id**, a **Display Name**, and the **Role**:

| Category | Role |
|---|---|
| Chassis | Chassis |
| Wheel | Wheel |
| Seat | Seat |
| Structure | None |
| Decoration | None |
| Engine | None |
| Prop | None |

> Role is what the validator/stats use to count chassis/wheels/seats. New
> categories with Role = None can be added any time, no code changes.

---

## 5. Part prefabs

Every buildable object is a prefab whose **root** has:

- A **mesh + collider** (collider on the **Parts** layer).
- **`PartInstance`** (assign its `PartData` — created in §6).
- **`PartAttachments`** (just add it; it caches the sockets below).
- One or more **AttachmentPoint** children (empty GameObjects):
  - Position/rotate each socket where parts should connect.
  - The **blue forward gizmo** must point *outward* (the direction a mating part comes from).
  - Set **Compatible Categories** (leave empty = accepts any).

### 5a. Wheel prefab (special)

A wheel prefab must carry the pre-wired driving sub-hierarchy so the assembler
never has to build springs:

```
Wheel (root)         ← PartInstance, PartAttachments, WheelRoleProvider
├── AttachmentPoint  ← socket that connects to the chassis (forward = toward chassis)
├── SuspensionAnchor ← Suspension   (_wheelTransform → WheelPivot)
└── WheelPivot       ← Wheel        (_tireVisual → TireVisual)
    └── TireVisual   ← the rolling tire mesh
```

- On **WheelRoleProvider**: assign the **Wheel** component (on WheelPivot), and
  tick **Is Steering** / **Is Brake** as desired (e.g. front wheels steering, all braking).
- Make sure **`Suspension._restDistance`** roughly matches **`Wheel._groundCheckDistance`**.
- Orient `WheelPivot` so its **forward = vehicle forward** and **right = axle/lateral**.
- Do **not** add a Rigidbody to any part — the builder enforces one Rigidbody on the root.

> The `Wheel`/`Suspension` components stay **disabled during building** (the
> builder handles this) and are enabled automatically at Test.

Repeat for chassis, seat, structure, decoration, etc. (those don't need the
wheel sub-hierarchy or `WheelRoleProvider`).

---

## 6. Part data (ScriptableObjects)

For each prefab create **Create ▸ Soapbox ▸ Builder ▸ Part Data** and fill in:

- **Id** (unique, stable — used in save files), **Display Name**, **Description**
- **Category** (from §4)
- **Cost**, **Weight** (kg), **Size** (approx, metres)
- **Prefab** (the §5 prefab) and **Thumbnail** (optional sprite)

Then assign this `PartData` to the prefab's **PartInstance**.

---

## 7. Part catalog

Create **Create ▸ Soapbox ▸ Builder ▸ Part Catalog** and drag **every**
`PartData` into its list. This drives the browser and load lookups.

---

## 8. Scene objects

Build this hierarchy in your builder scene:

```
BuilderCamera        ← Camera + BuilderCamera
VehicleRoot          ← VehicleRoot + VehicleStatsTracker + PartFactory + VehicleAssembler
BuildManager         ← PlacementController + SelectionController + PaintController + BuilderController
BuildPlatform        ← mesh + collider on the "Ground" layer
EventSystem          ← (UI) Input System UI Input Module
Canvas               ← (UI) browser, stats, toolbar
```

You can split components onto different objects; just wire the references.

### 8a. VehicleRoot object
- **VehicleRoot**: optional `Vehicle Name`.
- **VehicleStatsTracker**: (auto-uses the VehicleRoot on the same object).
- **PartFactory**: set **Vehicle** → this VehicleRoot.
- **VehicleAssembler**: set Min Wheels = 4, Require Chassis/Seat as desired.

### 8b. BuilderCamera object
- **BuilderCamera**: set **Selection** → the SelectionController; assign the
  input refs (§9): Look, Zoom, Orbit, Pan, Focus.

### 8c. BuildManager object
- **PlacementController**: Build Camera → BuilderCamera's Camera; **Vehicle** →
  VehicleRoot; Valid/Invalid Material (§3); Ground Mask = **Ground**; Obstacle
  Mask = **Parts**; Rotation Step (15/30/45/90); input refs Point/Place/Cancel/Rotate Left/Rotate Right.
- **SelectionController**: Camera → BuilderCamera's Camera; Placement →
  PlacementController; Highlight Material (§3); Selectable Mask = **Parts**;
  input refs Select + Point.
- **PaintController**: Selection → SelectionController; pick a default colour.
- **BuilderController**: assign Vehicle, Factory, Placement, Selection, Paint,
  Assembler, Stats (the StatsTracker), Catalog; input refs Delete/Duplicate/Undo/Redo;
  **Build Mode Behaviours** → drag PlacementController, SelectionController,
  PaintController, BuilderCamera (these get disabled on Test).

---

## 9. Input action references

The Input Actions asset (`Assets/03_Assets/Inputs/InputActions.inputactions`)
now has a **Builder** map with these actions. Assign each field above by dragging
the matching action (its `InputActionReference` sub-asset) from the asset:

| Action | Default binding | Used by |
|---|---|---|
| Point | Mouse position | Placement, Selection |
| Place | Left mouse | Placement |
| Cancel | Right mouse / Esc | Placement |
| Select | Left mouse | Selection |
| Rotate Left / Right | Q / E | Placement preview |
| Delete | Delete | BuilderController |
| Duplicate | G | BuilderController |
| Undo / Redo | Z / Y | BuilderController |
| Focus | F | Camera |
| Orbit | Middle mouse (hold) | Camera |
| Pan | Left Shift (hold + Orbit) | Camera |
| Look | Mouse delta | Camera |
| Zoom | Scroll wheel | Camera |

> Place and Select share Left-Mouse on purpose: selection is ignored while a
> ghost is being placed.

---

## 10. UI (optional but recommended)

On a Canvas:

- **Part browser**: a Scroll View; add **PartBrowserUI** to its content's parent
  with Catalog, BuilderController, the **Content** transform, a **PartButton**
  prefab, and (optional) a search `InputField`. Make a small `PartButton` prefab
  (a `Button` + child `Text`/`Image`) and assign it.
- **Category tabs**: buttons that call `PartBrowserUI.SetCategoryFilter(category)`.
- **Stats panel**: a `Text` + **StatsPanelUI** (Tracker → VehicleStatsTracker).
- **Toolbar**: buttons + **BuilderHUD** (assign Save/Load/Delete/Duplicate/Paint/
  Test/Undo/Redo buttons, a name `InputField`, and a status `Text`).

---

## 11. The three controller-stack changes (FYI)

Per the "adapt the builder to the controller" rule, only minimal, backward-
compatible additions were made:

1. **`VehicleController.Initialize(steering, brake)`** + a null-guard in `Awake`
   — lets the builder assign wheels at runtime; existing inspector-configured
   vehicles are unaffected.
2. **`Wheel`** now resolves its `Rigidbody` lazily (was cached in `Awake`) — so a
   wheel enabled *after* the builder adds the root Rigidbody still finds it.
3. **`Suspension`** — same lazy `Rigidbody` change.

Existing scenes using these scripts keep working exactly as before.

---

## 12. Build & test flow

1. Enter Play mode in the builder scene.
2. Click a part in the browser → a ghost follows the cursor.
   - It snaps to compatible sockets, turns **green** (valid) / **red** (invalid).
   - **Q/E** rotate; **Left-click** places; **Right-click/Esc** cancels.
3. Place a **chassis** (free placement on the platform), then snap on **4 wheels**
   and a **seat**, plus any structure/decoration.
4. Select parts (left-click when not placing) to **Delete (Del)**, **Duplicate (G)**,
   rotate, or **Paint**. **Undo (Z)** / **Redo (Y)** anytime.
5. Watch the **stats panel** update (weight, cost, size, CoM, wheels, seats).
6. **Save** with a name (writes `…/Vehicles/<name>.json` under the persistent
   data path). **Load** restores parts, transforms, connections and colours.
7. Press **Test**:
   - The vehicle is validated (≥1 chassis, ≥4 wheels, ≥1 seat, no floating parts).
   - A Rigidbody is added/configured on the root (mass = total weight, computed
     centre of mass), wheels are enabled, and `VehicleController.Initialize` is called.
   - Build-mode behaviours are disabled and you drive with the existing controls
     (A/D steer, Space brake; W debug-accel in editor) — the soapbox rolls under gravity.

> Tip for the test camera: the builder camera is disabled on Test. If you want a
> chase cam, add the existing `CameraRig`/`SplineFollowCamera` and point it at the
> VehicleRoot, or remove BuilderCamera from "Build Mode Behaviours" to keep orbiting.

---

## 13. Known limitations / next steps

- Free (ground) placement is allowed whenever there are no free sockets in the
  build (true for an empty vehicle; also technically true if every socket is
  occupied). Tighten if needed.
- Moving a connected part is a free transform; logical connections are kept but
  geometry isn't re-snapped.
- For hundreds of parts, replace the linear socket scan in `VehicleRoot`/
  `AttachmentSolver` with a spatial hash.
- Selection highlight swaps materials; if your shader needs a different highlight
  approach, adjust `SelectionController`.
