using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesDropController
    {
        private readonly VisualElement _listContainer;
        private readonly VisualElement _root;
        private readonly IFavouritesHost _host;

        private VisualElement _overlay;
        private bool _overlayVisible;

        public Object[] CurrentDragPayload { get; private set; }

        public FavouritesDropController(VisualElement listContainer, VisualElement root, IFavouritesHost host)
        {
            _listContainer = listContainer;
            _root = root;
            _host = host;
        }

        public void Register()
        {
            _listContainer.RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            _listContainer.RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            _listContainer.RegisterCallback<DragEnterEvent, VisualElement>(
                static (_, c) => c.AddToClassList("fav-drop-target"),    _listContainer, TrickleDown.TrickleDown);
            _listContainer.RegisterCallback<DragLeaveEvent, VisualElement>(
                static (_, c) => c.RemoveFromClassList("fav-drop-target"), _listContainer, TrickleDown.TrickleDown);
            _listContainer.RegisterCallback<DragExitedEvent, VisualElement>(
                static (_, c) => c.RemoveFromClassList("fav-drop-target"), _listContainer, TrickleDown.TrickleDown);
        }

        public void BuildOverlay()
        {
            _overlay = new IMGUIContainer(OnOverlayIMGUI);
            _overlay.AddToClassList("fav-drag-cancel-overlay");
            _overlay.style.display = DisplayStyle.None;

            var icon = new Image();
            icon.AddToClassList("fav-drag-cancel-icon");
            icon.pickingMode = PickingMode.Ignore;
            var cancelIcon = EditorGUIUtility.IconContent("console.erroricon").image;
            if (cancelIcon != null) icon.image = cancelIcon;
            _overlay.Add(icon);

            var label = new Label("Cancel dragging");
            label.AddToClassList("fav-drag-cancel-label");
            label.pickingMode = PickingMode.Ignore;
            _overlay.Add(label);

            _root.Add(_overlay);
        }

        public void ShowOverlay(Object[] payload)
        {
            CurrentDragPayload = payload;
            if (_overlay == null) return;
            _overlayVisible = true;
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.BringToFront();
            _overlay.MarkDirtyRepaint();
        }

        public void HideOverlay()
        {
            CurrentDragPayload = null;
            if (_overlay == null) return;
            _overlayVisible = false;
            _overlay.style.display = DisplayStyle.None;
            DragAndDrop.visualMode = DragAndDropVisualMode.None;
        }

        public void Poll()
        {
            if (!_overlayVisible) return;

            bool stillOurs = CurrentDragPayload != null
                             && DragAndDrop.objectReferences != null
                             && DragAndDrop.objectReferences.Length == CurrentDragPayload.Length;
            if (stillOurs)
            {
                for (int i = 0; i < CurrentDragPayload.Length; i++)
                {
                    if (DragAndDrop.objectReferences[i] != CurrentDragPayload[i]) { stillOurs = false; break; }
                }
            }

            if (!stillOurs) HideOverlay();
        }

        // ── Private ──────────────────────────────────────────

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            _listContainer.RemoveFromClassList("fav-drop-target");
            DragAndDrop.AcceptDrag();

            if (_host.Data.Tabs.Count == 0) return;
            var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            var entries = _host.UndoTracker.GetEntries(tab);

            var existingGids = new HashSet<string>();
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.Gid)) existingGids.Add(e.Gid);

            var toAdd = new List<FavouritesItemEntry>();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj == null) continue;
                var candidate = FavouritesEntryFactory.FromObject(obj);
                if (string.IsNullOrEmpty(candidate.Gid)) continue;
                if (!existingGids.Add(candidate.Gid)) continue;
                toAdd.Add(candidate);
            }

            if (toAdd.Count > 0)
            {
                _host.UndoTracker.RecordUndo(tab, toAdd.Count == 1 ? "Add Favourite" : "Add Favourites");
                foreach (var candidate in toAdd) entries.Add(candidate);
                _host.UndoTracker.AfterMutation(tab);
            }

            _host.SaveActive();
            _host.RefreshList();
            evt.StopPropagation();
        }

        private void OnOverlayIMGUI()
        {
            if (!_overlayVisible) return;

            var e = Event.current;
            switch (e.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    e.Use();
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    HideOverlay();
                    e.Use();
                    break;
                case EventType.MouseDown when e.button == 0:
                    HideOverlay();
                    e.Use();
                    break;
            }
        }
    }
}
