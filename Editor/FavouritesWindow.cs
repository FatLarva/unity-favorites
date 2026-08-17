using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    public class FavouritesWindow : EditorWindow, IHasCustomMenu, IFavouritesHost
    {
        private const string UxmlPath = "Packages/com.fatlarva.favourites/Editor/FavouritesWindow.uxml";
        private const string UssPath  = "Packages/com.fatlarva.favourites/Editor/FavouritesWindow.uss";

        // ── State ──────────────────────────────────────────────
        private FavouritesState       _data        = new FavouritesState();
        private FavouritesUndoTracker _undoTracker;
        private ListView              _itemsList;

        // ── Subsystems ─────────────────────────────────────────
        private FavouritesGearMenu       _gearMenu;
        private FavouritesPingRouter     _pingRouter;
        private FavouritesTabStrip       _tabStrip;
        private FavouritesItemsView      _view;
        private FavouritesDropController _dropController;
        private FavouritesLifecycle      _lifecycle;

        // ── IFavouritesHost ───────────────────────────────────
        FavouritesState       IFavouritesHost.Data        => _data;
        FavouritesUndoTracker IFavouritesHost.UndoTracker => _undoTracker;

        void IFavouritesHost.SaveActive()                                               => SaveActive();
        void IFavouritesHost.RefreshList()                                              => _view?.Refresh();
        void IFavouritesHost.ActivateEntry(FavouritesItemEntry entry, bool pingOnly)    => _pingRouter.Activate(entry, pingOnly);
        void IFavouritesHost.ArmPendingPreview()                                        => _view?.ArmPendingPreview();
        void IFavouritesHost.StartItemDrag(Object[] payload)                            => _dropController.ShowOverlay(payload);
        void IFavouritesHost.SetListSelection(int index)                                => _itemsList.SetSelection(index);
        IEnumerable<int> IFavouritesHost.GetListSelectedIndices()                       => _itemsList.selectedIndices;

        // ── Lifecycle ──────────────────────────────────────────

        [MenuItem("Window/Favourites")]
        public static void Open()
        {
            var window = GetWindow<FavouritesWindow>("Favourites");
            window.minSize = new Vector2(180, 150);
            window.Show();
        }

        private async void OnEnable()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var uss  = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uxml == null || uss == null)
            {
                Debug.LogError("[Favourites] Missing UXML or USS asset. Expected at " + UxmlPath);
                return;
            }

            uxml.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(uss);

            var tabsContainer = rootVisualElement.Q<VisualElement>("tabsContainer");
            _itemsList        = rootVisualElement.Q<ListView>("itemsList");
            var emptyHint     = rootVisualElement.Q<Label>("emptyHint");
            var listContainer = rootVisualElement.Q<VisualElement>("listContainer");

            _gearMenu       = new FavouritesGearMenu(ReloadActive);
            _pingRouter     = new FavouritesPingRouter(RefreshList);
            _tabStrip       = new FavouritesTabStrip(tabsContainer, rootVisualElement, this);
            var row         = new FavouritesItemRow(this);
            _view           = new FavouritesItemsView(_itemsList, emptyHint, this, row);
            _dropController = new FavouritesDropController(listContainer, rootVisualElement, this);

            rootVisualElement.Q<Button>("addTabButton").clicked += _tabStrip.AddTab;
            _dropController.Register();
            _dropController.BuildOverlay();

            EditorApplication.update += OnEditorUpdate;
            _lifecycle = new FavouritesLifecycle(RescanEntries);

            rootVisualElement.RegisterCallback<PointerMoveEvent, FavouritesWindow>(
                static (evt, self) =>
                    FavouritesPromptPlacement.UpdateScreenMouse(self.position.position + (Vector2)evt.position),
                this, TrickleDown.TrickleDown);

            rootVisualElement.RegisterCallback<PointerDownEvent, FavouritesWindow>(
                static (_, self) => self._view.SetPointerDown(true),
                this, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent, FavouritesWindow>(
                static (_, self) => self._view.OnPointerReleased(),
                this, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerCaptureOutEvent, FavouritesWindow>(
                static (_, self) => self._view.OnPointerReleased(),
                this, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerLeaveEvent, FavouritesWindow>(
                static (_, self) => self._view.OnPointerReleased(),
                this, TrickleDown.TrickleDown);

            try
            {
                _data = await FavouritesStore.LoadActive();
                RecreateTracker();
                _tabStrip.Rebuild();
                _view.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _lifecycle?.Dispose();
            _lifecycle = null;
            DisposeTracker();
        }

        public void AddItemsToMenu(GenericMenu menu) => _gearMenu?.Populate(menu);

        // ── Private ───────────────────────────────────────────

        private async void SaveActive()
        {
            try
            {
                _undoTracker?.WriteBackToTabs();
                await FavouritesStore.SaveActive(_data);
            }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void RefreshList() => _view?.Refresh();

        private async Task ReloadActive()
        {
            _data = await FavouritesStore.LoadActive();
            RecreateTracker();
            _tabStrip.Rebuild();
            _view.Refresh();
        }

        private void RecreateTracker()
        {
            DisposeTracker();
            _undoTracker = new FavouritesUndoTracker(_data);
            _undoTracker.UndoRedoChanged += OnUndoRedoChanged;
            _undoTracker.Initialize();
        }

        private void DisposeTracker()
        {
            if (_undoTracker == null) return;
            _undoTracker.UndoRedoChanged -= OnUndoRedoChanged;
            _undoTracker.Dispose();
            _undoTracker = null;
        }

        private void OnUndoRedoChanged(FavouritesTab changedTab)
        {
            int idx = _data.Tabs.IndexOf(changedTab);
            if (idx >= 0 && idx != _data.ActiveTabIndex)
            {
                _data.ActiveTabIndex = idx;
                _tabStrip.Rebuild();
            }
            _view.Refresh();
            SaveActive();
        }

        private void RescanEntries()
        {
            if (_undoTracker == null) return;
            _undoTracker.RescanUnresolved();
            if (_view != null) _view.Refresh();
        }

        private void OnEditorUpdate()
        {
            _dropController?.Poll();
            _view?.Poll();
        }
    }
}
