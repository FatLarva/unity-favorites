using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesUndoTracker : IDisposable
    {
        private readonly FavouritesState _state;
        private readonly Dictionary<FavouritesTab, FavouritesItemsList> _itemsByTab = new();
        private readonly Dictionary<FavouritesTab, List<int>> _snapshots = new();

        public event Action<FavouritesTab> UndoRedoChanged;

        public FavouritesUndoTracker(FavouritesState state)
        {
            _state = state;
        }

        public void Initialize()
        {
            foreach (var tab in _state.Tabs)
            {
                CreateSO(tab);
            }
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            DestroySOs();
        }

        public List<FavouritesItemEntry> GetEntries(FavouritesTab tab)
        {
            return GetOrCreateSO(tab).Entries;
        }

        public void RegisterTab(FavouritesTab tab)
        {
            if (!_itemsByTab.ContainsKey(tab))
            {
                CreateSO(tab);
            }
        }

        public void ForgetTab(FavouritesTab tab)
        {
            if (_itemsByTab.TryGetValue(tab, out var so))
            {
                if (so != null) Object.DestroyImmediate(so);
                _itemsByTab.Remove(tab);
                _snapshots.Remove(tab);
            }
        }

        public void RecordUndo(FavouritesTab tab, string description)
        {
            var so = GetOrCreateSO(tab);
            Undo.RegisterCompleteObjectUndo(so, description);
        }

        public void AfterMutation(FavouritesTab tab)
        {
            var so = GetOrCreateSO(tab);
            _snapshots[tab] = SnapshotIds(so.Entries);
        }

        /// <summary>
        /// Re-resolves any entries whose Object ref is null. Called after scene/prefab-stage events
        /// when previously-unloaded containers may now be loaded, or when Unity nulls out a live ref
        /// across a scene reload.
        /// </summary>
        public bool RescanUnresolved()
        {
            bool anyResolved = false;
            foreach (var kv in _itemsByTab)
            {
                var so = kv.Value;
                if (so == null) continue;
                foreach (var entry in so.Entries)
                {
                    if (entry == null || entry.Object != null) continue;
                    if (string.IsNullOrEmpty(entry.Gid)) continue;
                    if (!GlobalObjectId.TryParse(entry.Gid, out var gid)) continue;
                    var resolved = GlobalObjectIdentifierToObjectOrNull(gid);
                    if (resolved != null)
                    {
                        entry.Object = resolved;
                        anyResolved = true;
                    }
                }
            }
            return anyResolved;
        }

        public void WriteBackToTabs()
        {
            foreach (var tab in _state.Tabs)
            {
                if (!_itemsByTab.TryGetValue(tab, out var so) || so == null) continue;
                SerializeFrom(so, tab);
            }
        }

        // ── Internal ─────────────────────────────────────

        private FavouritesItemsList GetOrCreateSO(FavouritesTab tab)
        {
            if (!_itemsByTab.TryGetValue(tab, out var so) || so == null)
            {
                so = CreateSO(tab);
            }
            return so;
        }

        private FavouritesItemsList CreateSO(FavouritesTab tab)
        {
            var so = ScriptableObject.CreateInstance<FavouritesItemsList>();
            so.hideFlags = HideFlags.HideAndDontSave;
            so.name = "FavouritesItems_" + tab.Name;
            ResolveInto(tab, so);
            _itemsByTab[tab] = so;
            _snapshots[tab] = SnapshotIds(so.Entries);
            return so;
        }

        private void OnUndoRedoPerformed()
        {
            FavouritesTab changed = null;
            foreach (var tab in _state.Tabs)
            {
                if (!_itemsByTab.TryGetValue(tab, out var so) || so == null) continue;
                if (!_snapshots.TryGetValue(tab, out var snapshot)) continue;
                if (DiffersFromSnapshot(so.Entries, snapshot))
                {
                    _snapshots[tab] = SnapshotIds(so.Entries);
                    changed = tab;
                }
            }

            if (changed != null)
            {
                UndoRedoChanged?.Invoke(changed);
            }
        }

        private void DestroySOs()
        {
            foreach (var so in _itemsByTab.Values)
            {
                if (so != null) Object.DestroyImmediate(so);
            }
            _itemsByTab.Clear();
            _snapshots.Clear();
        }

        private static List<int> SnapshotIds(List<FavouritesItemEntry> entries)
        {
            var ids = new List<int>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int id = e?.Object != null ? e.Object.GetInstanceID() : 0;
                // Mix in gid hash so re-ordering of unresolved entries is still detected.
                int gidHash = string.IsNullOrEmpty(e?.Gid) ? 0 : e.Gid.GetHashCode();
                ids.Add(id ^ gidHash);
            }
            return ids;
        }

        private static bool DiffersFromSnapshot(List<FavouritesItemEntry> entries, List<int> snapshot)
        {
            if (entries.Count != snapshot.Count) return true;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int id = e?.Object != null ? e.Object.GetInstanceID() : 0;
                int gidHash = string.IsNullOrEmpty(e?.Gid) ? 0 : e.Gid.GetHashCode();
                if ((id ^ gidHash) != snapshot[i]) return true;
            }
            return false;
        }

        private static void ResolveInto(FavouritesTab tab, FavouritesItemsList list)
        {
            list.Entries.Clear();
            foreach (var itemRef in tab.ItemRefs)
            {
                var entry = new FavouritesItemEntry
                {
                    Gid = itemRef.Id,
                    DisplayName = itemRef.DisplayName,
                    ContainerGuid = itemRef.ContainerGuid,
                    ContainerKind = itemRef.ContainerKind,
                };

                if (!string.IsNullOrEmpty(itemRef.Id) && GlobalObjectId.TryParse(itemRef.Id, out var gid))
                {
                    entry.Object = GlobalObjectIdentifierToObjectOrNull(gid);
                }

                // If the entry resolved, re-derive metadata from the live object so prefab/scene
                // misclassifications from older saves get corrected automatically.
                if (entry.Object != null)
                {
                    var fresh = FavouritesEntryFactory.FromObject(entry.Object);
                    if (!string.IsNullOrEmpty(fresh.DisplayName)) entry.DisplayName = fresh.DisplayName;
                    if (!string.IsNullOrEmpty(fresh.ContainerGuid)) entry.ContainerGuid = fresh.ContainerGuid;
                    if (fresh.ContainerKind != FavouritesContainerKind.Unknown) entry.ContainerKind = fresh.ContainerKind;
                }

                list.Entries.Add(entry);
            }
        }

        private static Object GlobalObjectIdentifierToObjectOrNull(GlobalObjectId gid)
        {
            try
            {
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            }
            catch
            {
                return null;
            }
        }

        private static void SerializeFrom(FavouritesItemsList list, FavouritesTab tab)
        {
            tab.ItemRefs = new List<FavouritesItemRef>(list.Entries.Count);
            foreach (var entry in list.Entries)
            {
                if (entry == null) continue;

                string id = entry.Gid;
                string displayName = entry.DisplayName;
                string containerGuid = entry.ContainerGuid;
                FavouritesContainerKind kind = entry.ContainerKind;

                // If the object is live, refresh the metadata from its current GID/name so renames and
                // scene-moves propagate to the saved file.
                if (entry.Object != null)
                {
                    var fresh = FavouritesEntryFactory.FromObject(entry.Object);
                    id = fresh.Gid;
                    displayName = fresh.DisplayName;
                    containerGuid = fresh.ContainerGuid;
                    kind = fresh.ContainerKind;
                }

                if (string.IsNullOrEmpty(id)) continue;

                tab.ItemRefs.Add(new FavouritesItemRef
                {
                    Id = id,
                    DisplayName = displayName,
                    ContainerGuid = containerGuid,
                    ContainerKind = kind,
                });
            }
        }
    }
}
