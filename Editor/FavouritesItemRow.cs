using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesItemRow
    {
        private readonly IFavouritesHost _host;

        public FavouritesItemRow(IFavouritesHost host)
        {
            _host = host;
        }

        public VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("fav-item");

            var pingButton = new Button();
            pingButton.AddToClassList("fav-item-ping");
            var pingIcon = EditorGUIUtility.IconContent("d_Search Icon").image;
            if (pingIcon != null) pingButton.style.backgroundImage = new StyleBackground((Texture2D)pingIcon);
            row.Add(pingButton);

            var body = new VisualElement();
            body.AddToClassList("fav-item-body");
            body.AddManipulator(new BodyDragManipulator(_host));
            row.Add(body);

            var iconSlot = new VisualElement();
            iconSlot.AddToClassList("fav-item-icon");
            var iconBg = new Image { scaleMode = ScaleMode.ScaleToFit };
            iconBg.AddToClassList("fav-item-icon-bg");
            iconBg.pickingMode = PickingMode.Ignore;
            iconSlot.Add(iconBg);
            var iconFgShadow = new Image { scaleMode = ScaleMode.ScaleToFit, tintColor = new Color(0f, 0f, 0f, 0.9f) };
            iconFgShadow.AddToClassList("fav-item-icon-fg-shadow");
            iconFgShadow.pickingMode = PickingMode.Ignore;
            iconSlot.Add(iconFgShadow);
            var iconFg = new Image { scaleMode = ScaleMode.ScaleToFit };
            iconFg.AddToClassList("fav-item-icon-fg");
            iconFg.pickingMode = PickingMode.Ignore;
            iconSlot.Add(iconFg);
            body.Add(iconSlot);

            var labelRow = new VisualElement();
            labelRow.AddToClassList("fav-item-label-row");
            body.Add(labelRow);

            var deleteButton = new Button();
            deleteButton.AddToClassList("fav-item-delete");
            deleteButton.tooltip = "Remove from Favourites";
            var trashIcon = EditorGUIUtility.IconContent("d_TreeEditor.Trash").image;
            if (trashIcon != null) deleteButton.style.backgroundImage = new StyleBackground((Texture2D)trashIcon);
            row.Add(deleteButton);

            pingButton.clickable = new Clickable(() =>
            {
                if (row.userData is FavouritesItemEntry entry)
                    _host.ActivateEntry(entry, pingOnly: true);
            });

            row.RegisterCallback<PointerDownEvent, VisualElement>(static (evt, rowArg) =>
            {
                if (evt.button != 1) return;
                if (rowArg.userData is not FavouritesItemEntry entry || entry.Object == null) return;
                Selection.activeObject = entry.Object;
                var popupRect = new Rect(evt.position.x, evt.position.y, 0, 0);
                string menuPath = AssetDatabase.Contains(entry.Object) ? "Assets/" : "GameObject/";
                EditorUtility.DisplayPopupMenu(popupRect, menuPath, null);
                evt.StopPropagation();
            }, row);

            return row;
        }

        public void BindItem(VisualElement element, int index)
        {
            var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            var entries = _host.UndoTracker.GetEntries(tab);
            FavouritesItemEntry entry = index < entries.Count ? entries[index] : null;

            element.userData = entry;

            var iconSlot    = element.Q<VisualElement>(className: "fav-item-icon");
            var iconBg      = element.Q<Image>(className: "fav-item-icon-bg");
            var iconFgShadow = element.Q<Image>(className: "fav-item-icon-fg-shadow");
            var iconFg      = element.Q<Image>(className: "fav-item-icon-fg");
            var labelRow    = element.Q<VisualElement>(className: "fav-item-label-row");
            var pingButton  = element.Q<Button>(className: "fav-item-ping");
            var deleteButton = element.Q<Button>(className: "fav-item-delete");

            pingButton.tooltip = PingTooltipFor(entry);

            bool useThumbnails = tab.UseThumbnails;
            element.EnableInClassList("fav-item--thumb", useThumbnails);

            labelRow.Clear();
            iconBg.image = null;
            iconFg.image = null;
            iconFgShadow.image = null;
            iconSlot.EnableInClassList("fav-item-icon--composite", false);
            iconSlot.style.display = DisplayStyle.Flex;

            if (entry == null || (!entry.IsResolved && !entry.HasContainer))
            {
                string text = entry != null && !string.IsNullOrEmpty(entry.DisplayName)
                    ? entry.DisplayName + " (missing)" : "<missing>";
                var missingLabel = MakeNameLabel(text, "Reference lost — the original object no longer exists.");
                missingLabel.style.color = new StyleColor(new Color(1f, 0.4f, 0.4f));
                labelRow.Add(missingLabel);
                element.EnableInClassList("fav-item--unresolved", false);
            }
            else if (!entry.IsResolved)
            {
                iconSlot.style.display = DisplayStyle.None;
                string containerPath = FavouritesEntryFactory.GetContainerPath(entry);
                string containerName = ContainerShortName(containerPath);
                string rawName       = string.IsNullOrEmpty(entry.DisplayName) ? "(unloaded)" : entry.DisplayName;
                string tooltip       = string.IsNullOrEmpty(containerPath)
                    ? $"{entry.ContainerKind} — container not found."
                    : $"{entry.ContainerKind}: {containerPath} (not loaded — click ping to load).";
                BuildCompositeLabelRow(labelRow, ContainerTypeIcon(entry.ContainerKind), containerName,
                    ContainerMiniIcon(entry.ContainerKind), rawName, tooltip);
                element.EnableInClassList("fav-item--unresolved", true);
            }
            else
            {
                var obj       = entry.Object;
                bool isAsset  = EditorUtility.IsPersistent(obj);
                bool composite = !isAsset && entry.HasContainer;

                Texture image = null;
                if (useThumbnails && isAsset)
                {
                    image = AssetPreview.GetAssetPreview(obj);
                    if (image == null) _host.ArmPendingPreview();
                }
                Texture objIcon = image != null ? image : AssetPreview.GetMiniThumbnail(obj);
                if (!isAsset && entry.HasContainer)
                    objIcon = EditorGUIUtility.IconContent("GameObject Icon").image;

                string fullPath = GetFullPath(obj);
                string baseName = IsDuplicateName(entries, index, obj.name) ? fullPath : obj.name;

                if (composite)
                {
                    iconSlot.style.display = DisplayStyle.None;
                    string containerPath = FavouritesEntryFactory.GetContainerPath(entry);
                    string containerName = ContainerShortName(containerPath);
                    string tooltip       = string.IsNullOrEmpty(containerPath)
                        ? fullPath
                        : $"{entry.ContainerKind}: {containerPath}\n{fullPath}";
                    BuildCompositeLabelRow(labelRow, ContainerTypeIcon(entry.ContainerKind), containerName,
                        objIcon, baseName, tooltip);
                }
                else
                {
                    iconFg.image = objIcon;
                    labelRow.Add(MakeNameLabel(baseName, fullPath));
                }
                element.EnableInClassList("fav-item--unresolved", false);
            }

            var captured      = entry;
            int capturedIndex = index;
            deleteButton.clickable = new Clickable(() =>
            {
                EditorApplication.delayCall += () => RemoveItem(captured, capturedIndex);
            });
        }

        // ── Item removal ──────────────────────────────────────

        private void RemoveItem(FavouritesItemEntry target, int fallbackIndex)
        {
            if (_host.Data.Tabs.Count == 0) return;
            var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            var entries = _host.UndoTracker.GetEntries(tab);

            int index = target != null ? entries.IndexOf(target) : -1;
            if (index < 0 && fallbackIndex >= 0 && fallbackIndex < entries.Count && entries[fallbackIndex] == target)
                index = fallbackIndex;
            if (index < 0) return;

            _host.UndoTracker.RecordUndo(tab, "Remove Favourite");
            entries.RemoveAt(index);
            _host.UndoTracker.AfterMutation(tab);
            _host.SaveActive();
            _host.RefreshList();
        }

        // ── Static helpers ────────────────────────────────────

        private static string PingTooltipFor(FavouritesItemEntry entry)
        {
            if (entry == null || (!entry.IsResolved && !entry.HasContainer)) return "Reference lost";
            if (!entry.IsResolved) return entry.ContainerKind == FavouritesContainerKind.Prefab
                ? "Open prefab and ping" : "Load scene and ping";
            return EditorUtility.IsPersistent(entry.Object) ? "Ping in Project" : "Ping in Hierarchy";
        }

        private static Label MakeNameLabel(string text, string tooltip)
        {
            var l = new Label(text);
            l.AddToClassList("fav-item-label");
            l.tooltip = tooltip;
            return l;
        }

        private static Image MakeInlineIcon(Texture tex)
        {
            var img = new Image { scaleMode = ScaleMode.ScaleToFit, image = tex };
            img.AddToClassList("fav-item-inline-icon");
            img.pickingMode = PickingMode.Ignore;
            return img;
        }

        private static void BuildCompositeLabelRow(VisualElement row, Texture containerIcon, string containerName,
            Texture objIcon, string objName, string tooltip)
        {
            row.tooltip = tooltip;
            row.Add(MakeInlineIcon(containerIcon));
            if (!string.IsNullOrEmpty(containerName))
            {
                var containerLabel = new Label(containerName + "/");
                containerLabel.AddToClassList("fav-item-label-container");
                containerLabel.pickingMode = PickingMode.Ignore;
                row.Add(containerLabel);
            }
            row.Add(MakeInlineIcon(objIcon));
            var nameLabel = MakeNameLabel(objName, null);
            nameLabel.pickingMode = PickingMode.Ignore;
            row.Add(nameLabel);
        }

        private static Texture ContainerMiniIcon(FavouritesContainerKind kind) => kind switch
        {
            FavouritesContainerKind.Scene  => EditorGUIUtility.IconContent("GameObject Icon").image,
            FavouritesContainerKind.Prefab => EditorGUIUtility.IconContent("GameObject Icon").image,
            _                              => EditorGUIUtility.IconContent("d_console.warnicon").image,
        };

        private static Texture ContainerTypeIcon(FavouritesContainerKind kind) => kind switch
        {
            FavouritesContainerKind.Scene  => EditorGUIUtility.IconContent("SceneAsset Icon").image,
            FavouritesContainerKind.Prefab => EditorGUIUtility.IconContent("Prefab Icon").image,
            _                              => null,
        };

        private static bool IsDuplicateName(List<FavouritesItemEntry> entries, int selfIndex, string name)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == selfIndex) continue;
                var e = entries[i];
                if (e?.Object != null && e.Object.name == name) return true;
            }
            return false;
        }

        private static string ContainerShortName(string containerPath) =>
            string.IsNullOrEmpty(containerPath) ? null : Path.GetFileNameWithoutExtension(containerPath);

        private static string GetFullPath(Object obj)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath)) return assetPath;

            GameObject go = obj as GameObject;
            if (go == null && obj is Component comp) go = comp.gameObject;

            if (go != null)
            {
                var stack = new Stack<string>();
                Transform t = go.transform;
                while (t != null) { stack.Push(t.name); t = t.parent; }
                return string.Join("/", stack);
            }

            return obj.name;
        }

        // ── BodyDragManipulator ───────────────────────────────

        private class BodyDragManipulator : Manipulator
        {
            private readonly IFavouritesHost _host;
            private Vector2 _downPos;
            private FavouritesItemEntry _primaryEntry;
            private int _downIndex;
            private int _pointerId;
            private bool _armed;
            private bool _preventedSelection;
            private double _lastClickTime;
            private int _lastClickIndex = -1;

            public BodyDragManipulator(IFavouritesHost host) => _host = host;

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0) return;

                var row = target.parent;
                if (row?.userData is not FavouritesItemEntry entry) return;

                var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
                var entries = _host.UndoTracker.GetEntries(tab);
                int index   = entries.IndexOf(entry);
                if (index < 0) return;

                _downPos  = evt.position;
                _primaryEntry = entry;
                _downIndex = index;
                _pointerId = evt.pointerId;
                _armed     = true;
                _preventedSelection = false;

                target.CapturePointer(_pointerId);

                bool additive = evt.ctrlKey || evt.commandKey;
                bool range    = evt.shiftKey;

                double now = EditorApplication.timeSinceStartup;
                if (_lastClickIndex == index && now - _lastClickTime < 0.3)
                {
                    _armed = false;
                    _lastClickIndex = -1;
                    target.ReleasePointer(_pointerId);
                    _host.ActivateEntry(entry, pingOnly: false);
                    evt.StopPropagation();
                    return;
                }
                _lastClickTime  = now;
                _lastClickIndex = index;

                if (!additive && !range && IsSelected(index))
                {
                    _preventedSelection = true;
                    evt.StopPropagation();
                }
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!_armed) return;
                if ((evt.pressedButtons & 1) == 0) { Disarm(); return; }
                if (Vector2.Distance(evt.position, _downPos) < 6f) return;

                _armed = false;
                target.ReleasePointer(_pointerId);

                if (_primaryEntry?.Object == null) return;

                var payload = CollectDragPayload(_primaryEntry, _host);
                if (payload.Length == 0) return;

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = payload;
                var paths = new List<string>();
                foreach (var o in payload)
                {
                    string p = AssetDatabase.GetAssetPath(o);
                    if (!string.IsNullOrEmpty(p)) paths.Add(p);
                }
                if (paths.Count > 0) DragAndDrop.paths = paths.ToArray();
                DragAndDrop.StartDrag(payload.Length > 1 ? $"{payload.Length} items" : _primaryEntry.Object.name);
                _host.StartItemDrag(payload);
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!_armed) { Disarm(); return; }
                if (_preventedSelection) _host.SetListSelection(_downIndex);
                Disarm();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt) => _armed = false;

            private void Disarm()
            {
                _armed = false;
                if (target.HasPointerCapture(_pointerId)) target.ReleasePointer(_pointerId);
            }

            private bool IsSelected(int index)
            {
                foreach (int i in _host.GetListSelectedIndices())
                    if (i == index) return true;
                return false;
            }
        }

        private static Object[] CollectDragPayload(FavouritesItemEntry primary, IFavouritesHost host)
        {
            var tab     = host.Data.Tabs[host.Data.ActiveTabIndex];
            var entries = host.UndoTracker.GetEntries(tab);
            var collected = new List<Object>();
            bool primaryInSelection = false;

            foreach (int i in host.GetListSelectedIndices())
            {
                if (i < 0 || i >= entries.Count) continue;
                var entry = entries[i];
                if (entry?.Object == null) continue;
                collected.Add(entry.Object);
                if (entry == primary) primaryInSelection = true;
            }

            if (!primaryInSelection)
            {
                collected.Clear();
                if (primary?.Object != null) collected.Add(primary.Object);
            }

            return collected.ToArray();
        }
    }
}
