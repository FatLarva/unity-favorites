# Favourites

A Unity editor window for bookmarking anything you reach for often: project assets, scene objects, prefab children.

## Features

- **Tabs** with per-tab color, reorder by drag, rename, duplicate, delete.
- **Items** added via drag-drop from Project/Hierarchy. Reorderable; removable via trash button or Delete key.
- **Per-tab undo** (Ctrl/Cmd+Z) for item add / remove / reorder.
- **Thumbnail mode** per tab for larger asset previews.
- **Scene / prefab children** keep a stable reference via `GlobalObjectId` and survive scene reloads. When the container isn't loaded, the entry is faded and clicking the ping button prompts to load the scene (Change / Load Additive / Cancel) or open the prefab.
- **Composite row layout** for container'd entries: `[scene/prefab icon] Container / [GameObject icon] ObjectName`, with a single tooltip showing the full container path + in-hierarchy path.
- **Multiple named states** (Switch State / Create / Rename / Delete / Import / Export) via the gear menu.

## Usage

Open via `Window → Favourites`.

- Drag assets or scene objects onto the window to add them.
- Right-click a tab for its context menu (rename, color, thumbnails, delete, …).
- Middle-click a tab to delete it.
- Right-click the window for the gear-menu equivalents (or use the gear icon).

## Storage

State is persisted under `UserSettings/Favourites/<state>.json`. An `_index.json` tracks the active state. Files are plain JSON — safe to commit per-user or ignore entirely.

## License

MIT.
