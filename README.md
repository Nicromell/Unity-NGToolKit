# NgToolBox - Unity Editor Tool

A Unity Editor window with productivity tools for scene and asset management. Open it via **NG Tools -> Tool Box** in the menu bar.

---

## Table of Contents

- [Utilities](#utilities)
  - [Apply Material](#apply-material)
  - [Round Transform Values](#round-transform-values)
  - [Select Matching Objects](#select-matching-objects)
- [Batch Renamer](#batch-renamer)
- [Prefab Batch Edit](#prefab-batch-edit)

---

## Utilities

### Apply Material

Assigns a material to a specific slot on all selected GameObjects at once.

**How to use:**
1. Select one or more GameObjects in the scene
2. Pick a material from the **Material** field
3. Choose the target slot from the **Material Slot** dropdown, it auto-populates from the current selection, showing `[index]  MaterialName` for each shared slot
4. Click **Apply Material to Selection**

**Notes:**
- The slot dropdown only shows slots that exist on **all** selected objects (the common minimum)
- If selected objects have different slot counts, a warning is displayed and objects with fewer slots than the chosen index are skipped
- The operation is fully undoable

---

### Round Transform Values

Snaps Position, Rotation, and/or Scale values to the nearest multiple of a defined step, useful for cleaning up transforms after manual placement.

**How to use:**
1. Select one or more GameObjects in the scene
2. Toggle which axes to round: **Position**, **Rotation**, **Scale**
3. Set the **Round Step** (e.g. `0.25` snaps to the nearest quarter unit)
4. Click **Round Transforms**

**Notes:**
- Round Step must be greater than `0`
- Rotation uses `localEulerAngles`. Objects near 90° or 180° boundaries may flip due to gimbal lock, use with caution on rotated objects
- The operation is fully undoable

---

### Select Matching Objects

Expands the current selection to all scene objects that share the same prefab asset or mesh as the active object.

**How to use:**
1. Select any GameObject in the scene
2. Optionally enable **Match prefab root only** (see below)
3. Click **Select Same Prefab / Mesh**

**Match modes:**

| Mode | Behaviour |
|------|-----------|
| Off (default) | Searches the entire scene for every instance of the same prefab or mesh |
| Match prefab root only | Only looks among siblings under the same parent (or scene root objects if there is no parent) |

**Fallback behaviour:** If the selected object is not a prefab instance, the tool falls back to mesh comparison using the object's `MeshFilter`. If it has no mesh either, nothing is selected and a warning is logged.

---

## Batch Renamer

Renames multiple GameObjects (Inspector selection) or Project assets (Project window selection) in one operation.

**Targets:**

| Target | What gets renamed |
|--------|-------------------|
| Inspector Selection | Selected GameObjects in the Hierarchy |
| Project Folder Selection | Selected assets in the Project window (folders are skipped) |

**Options:**

| Field | Description |
|-------|-------------|
| Prefix | Text prepended to every name |
| Suffix | Text appended to every name |
| Find | Substring to find in the original name |
| Replace | What to replace the found substring with |
| Add Index | Appends `_N` to each name, incrementing per object |
| Start at | The starting number for the index |

A **live preview** shows how `PrefabName` would look with the current settings before you apply.

**Find field - special syntax:**

Appending `^` to the Find string turns it into a **truncation anchor**. Everything from the first occurrence of the token onward is replaced, not just the token itself.

| Find | Input | Result |
|------|-------|--------|
| `_01` | `SM_Prop_Scroll_01_Leg` | `SM_Prop_Scroll__Leg` (normal replace) |
| `_01^` | `SM_Prop_Table_01_Leg` | `SM_Prop_Table` (everything from `_01` onward is cut) |
| `_01^` | `SM_Prop_No_Match` | unchanged |

**Notes:**
- Project renaming uses `AssetDatabase.StartAssetEditing` / `StopAssetEditing` to batch all operations into a single refresh pass — renaming large selections is fast
- Inspector renaming is fully undoable. Project renaming goes through Unity's asset pipeline and cannot be undone with Ctrl+Z

---

## Prefab Batch Edit

Applies bulk modifications to every prefab inside a chosen Project folder without opening them one by one.

**How to use:**
1. Drag a folder from the Project window into the **Prefab Folder** field
2. Toggle **Include Sub-folders** to control whether nested folders are included
3. Enable any combination of operations below
4. Click **Apply to Prefabs**

**Available operations:**

### Set Static

Applies a set of `StaticEditorFlags` to the prefab root and optionally all of its children.

| Option | Description |
|--------|-------------|
| Static Flags | Bitmask of flags to set (Batching Static, Contribute GI, Occludee/Occluder Static, Reflection Probe Static, etc.) |
| Apply to Children | When enabled, the flags are set on every child Transform inside the prefab, not just the root |

Default flags: `BatchingStatic`, `ContributeGI`, `OccludeeStatic`, `OccluderStatic`, `ReflectionProbeStatic`.

### Reset Transform

Resets the local Position, Rotation, and/or Scale of the **prefab root** to their default values.

| Toggle | Resets to |
|--------|-----------|
| Position | `Vector3.zero` |
| Rotation | `Quaternion.identity` |
| Scale | `Vector3.one` |

> Only the root transform is affected, child transforms are left untouched.

**Notes:**
- Prefabs are loaded with `PrefabUtility.LoadPrefabContents`, edited in memory, then saved back with `PrefabUtility.SaveAsPrefabAsset`, no scene instantiation occurs
- At least one operation must be enabled for the **Apply to Prefabs** button to become active
- **Changes are saved to disk immediately and cannot be undone with Ctrl+Z, make sure to version-control your prefabs before running !**
