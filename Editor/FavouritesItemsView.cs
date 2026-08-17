using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesItemsView
    {
        private const int SimpleRowHeight    = 22;
        private const int ThumbnailRowHeight = 48;

        private readonly ListView _listView;
        private readonly Label    _emptyHint;
        private readonly IFavouritesHost _host;

        private bool   _hasPendingPreview;
        private double _nextPreviewPoll;
        private bool   _pointerDown;

        public FavouritesItemsView(ListView listView, Label emptyHint, IFavouritesHost host,
            FavouritesItemRow row)
        {
            _listView  = listView;
            _emptyHint = emptyHint;
            _host      = host;

            _listView.selectionType    = SelectionType.Multiple;
            _listView.reorderable      = true;
            _listView.reorderMode      = ListViewReorderMode.Animated;
            _listView.fixedItemHeight  = SimpleRowHeight;
            _listView.showBorder       = false;
            _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            _listView.showAddRemoveFooter = false;

            _listView.makeItem = row.MakeItem;
            _listView.bindItem = row.BindItem;

            _listView.itemIndexChanged += OnItemIndexChanged;

            _listView.RegisterCallback<KeyDownEvent, FavouritesItemsView>(static (evt, self) =>
            {
                if (evt.keyCode != KeyCode.Delete) return;
                self.DeleteSelected();
                evt.StopPropagation();
            }, this);

            _listView.RegisterCallback<PointerDownEvent, FavouritesItemsView>(static (evt, self) =>
            {
                if (evt.button != 0) return;
                var ve = evt.target as VisualElement;
                while (ve != null && ve != self._listView)
                {
                    if (ve.ClassListContains("fav-item")) return;
                    ve = ve.parent;
                }
                self._listView.ClearSelection();
            }, this);
        }

        public void Refresh()
        {
            if (_host.Data.Tabs.Count == 0)
            {
                _listView.itemsSource = new List<FavouritesItemEntry>();
                _emptyHint.style.display = DisplayStyle.Flex;
                _listView.Rebuild();
                return;
            }

            var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            var entries = _host.UndoTracker.GetEntries(tab);
            _listView.fixedItemHeight = tab.UseThumbnails ? ThumbnailRowHeight : SimpleRowHeight;
            _listView.itemsSource     = entries;
            _listView.Rebuild();
            _emptyHint.style.display  = entries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ArmPendingPreview() => _hasPendingPreview = true;

        public void SetPointerDown(bool down) => _pointerDown = down;

        public void OnPointerReleased()
        {
            _pointerDown = false;
            if (_hasPendingPreview)
            {
                EditorApplication.delayCall += () =>
                {
                    _nextPreviewPoll = 0;
                    Poll();
                };
            }
        }

        public void Poll()
        {
            if (_host.Data.Tabs.Count == 0) return;
            var tab = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            if (!tab.UseThumbnails || !_hasPendingPreview) return;
            if (_pointerDown) return;
            if (EditorApplication.timeSinceStartup < _nextPreviewPoll) return;

            _nextPreviewPoll = EditorApplication.timeSinceStartup + 0.1;
            bool workerBusy = AssetPreview.IsLoadingAssetPreviews();
            _hasPendingPreview = false;
            _listView.RefreshItems();
            if (workerBusy) _hasPendingPreview = true;
        }

        public void DeleteSelected()
        {
            var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            var entries = _host.UndoTracker.GetEntries(tab);
            var indices = new List<int>();
            foreach (var i in _listView.selectedIndices) indices.Add(i);
            if (indices.Count == 0) return;

            if (indices.Count > 1)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Remove from Favourites",
                    $"Remove {indices.Count} items from the \"{tab.Name}\" tab in Favourites?\n\nThis only removes them from this Favourites tab — the assets themselves are not deleted.",
                    "Remove", "Cancel");
                if (!confirm) return;
            }

            indices.Sort();
            _host.UndoTracker.RecordUndo(tab, indices.Count == 1 ? "Remove Favourite" : "Remove Favourites");
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int idx = indices[i];
                if (idx >= 0 && idx < entries.Count) entries.RemoveAt(idx);
            }
            _host.UndoTracker.AfterMutation(tab);
            _host.SaveActive();
            _listView.ClearSelection();
            _host.RefreshList();
        }

        // ── Private ──────────────────────────────────────────

        private void OnItemIndexChanged(int srcIndex, int dstIndex)
        {
            if (_host.Data.Tabs.Count == 0) return;
            var tab     = _host.Data.Tabs[_host.Data.ActiveTabIndex];
            var entries = _host.UndoTracker.GetEntries(tab);

            if (srcIndex < 0 || dstIndex < 0
                || srcIndex >= entries.Count || dstIndex >= entries.Count
                || srcIndex == dstIndex)
            {
                _host.SaveActive();
                return;
            }

            // ListView already moved srcIndex → dstIndex. Reverse to capture pre-state for undo, then re-apply.
            var moved = entries[dstIndex];
            entries.RemoveAt(dstIndex);
            entries.Insert(srcIndex, moved);
            _host.UndoTracker.RecordUndo(tab, "Reorder Favourite");
            entries.RemoveAt(srcIndex);
            entries.Insert(dstIndex, moved);
            _host.UndoTracker.AfterMutation(tab);
            _host.SaveActive();
        }
    }
}
