using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesTabStrip
    {
        private readonly VisualElement _container;
        private readonly VisualElement _root;
        private readonly IFavouritesHost _host;

        public FavouritesTabStrip(VisualElement container, VisualElement root, IFavouritesHost host)
        {
            _container = container;
            _root = root;
            _host = host;
        }

        public void Rebuild()
        {
            _container.Clear();
            for (int i = 0; i < _host.Data.Tabs.Count; i++)
                _container.Add(CreateTabElement(i));
        }

        public void AddTab()
        {
            var newTab = new FavouritesTab
            {
                Name  = $"Tab {_host.Data.Tabs.Count + 1}",
                Color = Random.ColorHSV(0f, 1f, 0.4f, 0.8f, 0.7f, 1f),
            };
            _host.Data.Tabs.Add(newTab);
            _host.UndoTracker.RegisterTab(newTab);
            _host.Data.ActiveTabIndex = _host.Data.Tabs.Count - 1;
            _host.SaveActive();
            Rebuild();
            _host.RefreshList();
        }

        // ── Private ──────────────────────────────────────────

        private VisualElement CreateTabElement(int index)
        {
            var tab = _host.Data.Tabs[index];

            var element = new VisualElement();
            element.AddToClassList("fav-tab");
            element.style.backgroundColor = new StyleColor(new Color(tab.Color.r, tab.Color.g, tab.Color.b, 0.55f));

            if (index == _host.Data.ActiveTabIndex)
                element.AddToClassList("fav-tab--active");

            var label = new Label(tab.Name);
            label.AddToClassList("fav-tab-label");
            element.Add(label);

            RegisterTabDragHandlers(element);
            return element;
        }

        private int GetTabIndex(VisualElement element) => _container.IndexOf(element);

        private void RegisterTabDragHandlers(VisualElement element)
        {
            Vector2 downPos    = default;
            Vector2 grabOffset = default;
            bool    armed      = false;
            bool    dragging   = false;
            int     pointerId  = -1;
            int     startIndex = -1;
            int     targetIndex = -1;
            VisualElement ghost = null;

            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    downPos    = evt.position;
                    grabOffset = (Vector2)evt.position - new Vector2(element.worldBound.xMin, element.worldBound.yMin);
                    armed      = true;
                    dragging   = false;
                    pointerId  = evt.pointerId;
                    startIndex = GetTabIndex(element);
                    targetIndex = startIndex;
                }
                else if (evt.button == 1)
                {
                    ShowTabContextMenu(GetTabIndex(element));
                    evt.StopPropagation();
                }
                else if (evt.button == 2)
                {
                    DeleteTab(GetTabIndex(element));
                    evt.StopPropagation();
                }
            });

            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!armed) return;

                if (!dragging && Vector2.Distance(evt.position, downPos) > 4f)
                {
                    dragging = true;
                    element.CapturePointer(pointerId);
                    element.AddToClassList("fav-tab--dragging");
                    ghost = CreateTabGhost(element);
                    _root.Add(ghost);
                }

                if (dragging)
                {
                    PositionGhost(ghost, evt.position, grabOffset, element.worldBound.yMin);
                    UpdateDragTargetIndex(element, ref targetIndex, evt.position.x);
                }
            });

            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (dragging)
                {
                    if (element.HasPointerCapture(pointerId)) element.ReleasePointer(pointerId);
                    if (ghost != null) ghost.RemoveFromHierarchy();
                    dragging = false;
                    CommitTabMove(startIndex, targetIndex);
                }
                else if (armed && evt.button == 0)
                {
                    SetActiveTab(GetTabIndex(element));
                }
                armed = false;
            });
        }

        private VisualElement CreateTabGhost(VisualElement source)
        {
            var ghost = new VisualElement();
            ghost.AddToClassList("fav-tab");
            ghost.AddToClassList("fav-tab--ghost");
            ghost.style.backgroundColor = source.style.backgroundColor;
            ghost.style.position = Position.Absolute;
            ghost.style.width  = source.resolvedStyle.width;
            ghost.style.height = source.resolvedStyle.height;

            var label = new Label((source.Q<Label>(className: "fav-tab-label")).text);
            label.AddToClassList("fav-tab-label");
            ghost.Add(label);
            return ghost;
        }

        private void PositionGhost(VisualElement ghost, Vector2 worldMouse, Vector2 grabOffset, float worldTabTop)
        {
            Vector2 local    = _root.WorldToLocal(worldMouse - grabOffset);
            Vector2 rowLocal = _root.WorldToLocal(new Vector2(0f, worldTabTop));
            ghost.style.left = local.x;
            ghost.style.top  = rowLocal.y;
        }

        private void UpdateDragTargetIndex(VisualElement dragged, ref int targetIndex, float worldMouseX)
        {
            int newIndex = 0;
            for (int i = 0; i < _container.childCount; i++)
            {
                var sibling = _container[i];
                if (sibling == dragged) continue;
                float siblingCenter = sibling.worldBound.xMin + sibling.worldBound.width * 0.5f;
                if (worldMouseX > siblingCenter) newIndex++;
            }
            targetIndex = newIndex;
        }

        private void CommitTabMove(int from, int to)
        {
            if (from < 0 || to < 0 || from == to) { Rebuild(); return; }

            var tabData = _host.Data.Tabs[from];
            _host.Data.Tabs.RemoveAt(from);
            _host.Data.Tabs.Insert(to, tabData);

            if      (_host.Data.ActiveTabIndex == from)                                        _host.Data.ActiveTabIndex = to;
            else if (from < _host.Data.ActiveTabIndex && to >= _host.Data.ActiveTabIndex)     _host.Data.ActiveTabIndex--;
            else if (from > _host.Data.ActiveTabIndex && to <= _host.Data.ActiveTabIndex)     _host.Data.ActiveTabIndex++;

            _host.SaveActive();
            Rebuild();
            _host.RefreshList();
        }

        private void SetActiveTab(int index)
        {
            if (index < 0 || index >= _host.Data.Tabs.Count) return;
            _host.Data.ActiveTabIndex = index;
            _host.SaveActive();
            Rebuild();
            _host.RefreshList();
        }

        private void ShowTabContextMenu(int index)
        {
            if (index < 0) return;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Configure…"), false, () => ConfigureTab(index));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete…"), false, () => DeleteTab(index));
            menu.ShowAsContext();
        }

        private void ConfigureTab(int index)
        {
            var tab = _host.Data.Tabs[index];
            FavouritesTabConfigPrompt.Show(tab.Name, tab.Color, tab.UseThumbnails, (newName, newColor, newUseThumbnails) =>
            {
                if (!string.IsNullOrWhiteSpace(newName)) tab.Name = newName.Trim();
                tab.Color = newColor;
                tab.UseThumbnails = newUseThumbnails;
                _host.SaveActive();
                Rebuild();
                _host.RefreshList();
            });
        }

        private void DeleteTab(int index)
        {
            if (_host.Data.Tabs.Count <= 1)
            {
                EditorUtility.DisplayDialog("Can't delete", "At least one tab must remain.", "OK");
                return;
            }

            var tab     = _host.Data.Tabs[index];
            var entries = _host.UndoTracker.GetEntries(tab);
            if (entries.Count > 0)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Delete tab",
                    $"Delete tab \"{tab.Name}\" and all {entries.Count} references? This cannot be undone.",
                    "Delete", "Cancel");
                if (!confirm) return;
            }

            _host.UndoTracker.ForgetTab(tab);
            _host.Data.Tabs.RemoveAt(index);
            _host.Data.ActiveTabIndex = Mathf.Clamp(_host.Data.ActiveTabIndex, 0, _host.Data.Tabs.Count - 1);
            _host.SaveActive();
            Rebuild();
            _host.RefreshList();
        }
    }
}
