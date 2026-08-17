using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    public enum FavouritesContainerKind
    {
        Unknown = 0,
        Asset   = 1, // The object itself is an imported asset (prefab file, texture, ScriptableObject, etc.)
        Scene   = 2, // Object lives inside a scene; container is that scene asset.
        Prefab  = 3, // Object is a sub-object of a prefab file (e.g. nested child).
    }

    [Serializable]
    public class FavouritesItemRef
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _containerGuid;
        [SerializeField] private int _containerKind;

        public string Id { get => _id; set => _id = value; }
        public string DisplayName { get => _displayName; set => _displayName = value; }
        public string ContainerGuid { get => _containerGuid; set => _containerGuid = value; }
        public FavouritesContainerKind ContainerKind
        {
            get => (FavouritesContainerKind)_containerKind;
            set => _containerKind = (int)value;
        }
    }

    [Serializable]
    public class FavouritesItemEntry
    {
        [SerializeField] private Object _object;
        [SerializeField] private string _gid;
        [SerializeField] private string _displayName;
        [SerializeField] private string _containerGuid;
        [SerializeField] private int _containerKind;

        public Object Object { get => _object; set => _object = value; }
        public string Gid { get => _gid; set => _gid = value; }
        public string DisplayName { get => _displayName; set => _displayName = value; }
        public string ContainerGuid { get => _containerGuid; set => _containerGuid = value; }
        public FavouritesContainerKind ContainerKind
        {
            get => (FavouritesContainerKind)_containerKind;
            set => _containerKind = (int)value;
        }

        public bool IsResolved => _object != null;
        public bool HasContainer => ContainerKind != FavouritesContainerKind.Unknown
                                    && !string.IsNullOrEmpty(_containerGuid);
    }

    public static class FavouritesEntryFactory
    {
        public static FavouritesItemEntry FromObject(Object obj)
        {
            var entry = new FavouritesItemEntry { Object = obj };
            if (obj == null) return entry;

            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            entry.Gid = gid.ToString();
            entry.DisplayName = obj.name;

            string containerGuid = gid.assetGUID.ToString();
            entry.ContainerGuid = containerGuid;
            switch (gid.identifierType)
            {
                case 1:
                    entry.ContainerKind = FavouritesContainerKind.Asset;
                    break;
                case 2:
                    // identifierType 2 covers both regular scene objects and objects inside an open
                    // prefab stage (the stage lives in a preview scene, but its assetGUID points at
                    // the prefab asset). Disambiguate via the container's main-asset type.
                    entry.ContainerKind = IsPrefabContainer(containerGuid)
                        ? FavouritesContainerKind.Prefab
                        : FavouritesContainerKind.Scene;
                    break;
                case 3:
                    entry.ContainerKind = FavouritesContainerKind.Prefab;
                    break;
                default:
                    entry.ContainerKind = FavouritesContainerKind.Unknown;
                    entry.ContainerGuid = null;
                    break;
            }
            return entry;
        }

        private static bool IsPrefabContainer(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return false;
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            return mainType == typeof(GameObject);
        }

        public static string GetContainerPath(FavouritesItemEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ContainerGuid)) return null;
            return AssetDatabase.GUIDToAssetPath(entry.ContainerGuid);
        }
    }

    [Serializable]
    public class FavouritesTab
    {
        [SerializeField] private string _name = "New Tab";
        [SerializeField] private Color _color = new(0.3f, 0.6f, 0.95f, 1f);
        [SerializeField] private bool _useThumbnails;
        [SerializeField] private List<FavouritesItemRef> _itemRefs = new();

        public string Name { get => _name; set => _name = value; }
        public Color Color { get => _color; set => _color = value; }
        public bool UseThumbnails { get => _useThumbnails; set => _useThumbnails = value; }
        public List<FavouritesItemRef> ItemRefs { get => _itemRefs; set => _itemRefs = value; }
    }

    [Serializable]
    public class FavouritesState
    {
        [SerializeField] private List<FavouritesTab> _tabs = new();
        [SerializeField] private int _activeTabIndex;

        public List<FavouritesTab> Tabs { get => _tabs; set => _tabs = value; }
        public int ActiveTabIndex { get => _activeTabIndex; set => _activeTabIndex = value; }
    }

    [Serializable]
    internal class FavouritesIndex
    {
        [SerializeField] private string _active = "default";

        public string Active { get => _active; set => _active = value; }
    }

    public static class FavouritesStore
    {
        private const string RootFolder = "UserSettings/Favourites";
        private const string IndexFile = "_index.json";

        private static FavouritesIndex _indexCache;

        public static async Task<FavouritesState> LoadActive()
        {
            EnsureRoot();

            var index = await LoadIndex();
            var state = await ReadState(index.Active);
            if (state == null)
            {
                state = new FavouritesState();
                state.Tabs.Add(new FavouritesTab { Name = "Main" });
                await WriteState(index.Active, state);
            }
            return state;
        }

        public static async Task SaveActive(FavouritesState state)
        {
            var index = GetIndex();
            await WriteState(index.Active, state);
        }

        public static List<string> ListStates()
        {
            EnsureRoot();
            var list = new List<string>();
            foreach (var path in Directory.GetFiles(RootFolder, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("_")) continue;
                list.Add(name);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static string GetActiveStateName() => GetIndex().Active;

        public static async Task SwitchTo(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var index = GetIndex();
            index.Active = name;
            await WriteIndex(index);
        }

        public static async Task CreateState(string name)
        {
            EnsureRoot();
            name = SanitizeName(name);
            if (string.IsNullOrEmpty(name)) return;
            string path = StatePath(name);
            if (File.Exists(path)) return;
            var state = new FavouritesState();
            state.Tabs.Add(new FavouritesTab { Name = "Main" });
            await WriteState(name, state);
        }

        public static async Task RenameState(string oldName, string newName)
        {
            newName = SanitizeName(newName);
            if (string.IsNullOrEmpty(newName) || oldName == newName) return;
            string oldPath = StatePath(oldName);
            string newPath = StatePath(newName);
            if (!File.Exists(oldPath) || File.Exists(newPath)) return;
            File.Move(oldPath, newPath);

            var index = GetIndex();
            if (index.Active == oldName)
            {
                index.Active = newName;
                await WriteIndex(index);
            }
        }

        public static async Task DeleteState(string name)
        {
            string path = StatePath(name);
            if (File.Exists(path)) File.Delete(path);

            var index = GetIndex();
            if (index.Active == name)
            {
                var remaining = ListStates();
                index.Active = remaining.Count > 0 ? remaining[0] : "default";
                await WriteIndex(index);
                if (!File.Exists(StatePath(index.Active)))
                {
                    var fresh = new FavouritesState();
                    fresh.Tabs.Add(new FavouritesTab { Name = "Main" });
                    await WriteState(index.Active, fresh);
                }
            }
        }

        public static async Task Import(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath)) return;
            EnsureRoot();
            string name = SanitizeName(Path.GetFileNameWithoutExtension(sourceFilePath));
            string targetPath = StatePath(name);
            int suffix = 1;
            while (File.Exists(targetPath))
            {
                targetPath = StatePath($"{name} ({suffix++})");
            }
            File.Copy(sourceFilePath, targetPath);
            await SwitchTo(Path.GetFileNameWithoutExtension(targetPath));
        }

        public static void Export(string targetFilePath)
        {
            string active = GetIndex().Active;
            string src = StatePath(active);
            if (!File.Exists(src)) return;
            File.Copy(src, targetFilePath, true);
        }

        // ── Internal ─────────────────────────────────────

        private static void EnsureRoot()
        {
            if (!Directory.Exists(RootFolder)) Directory.CreateDirectory(RootFolder);
        }

        private static string StatePath(string name) => Path.Combine(RootFolder, name + ".json");

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        private static FavouritesIndex GetIndex()
        {
            if (_indexCache == null)
            {
                throw new InvalidOperationException(
                    "FavouritesStore.GetIndex() called before LoadActive(). Load the store on window open first.");
            }
            return _indexCache;
        }

        private static async Task<FavouritesIndex> LoadIndex()
        {
            EnsureRoot();
            string path = Path.Combine(RootFolder, IndexFile);
            if (!File.Exists(path))
            {
                _indexCache = new FavouritesIndex();
                await WriteIndex(_indexCache);
                return _indexCache;
            }
            try
            {
                _indexCache = await Task.Run(() =>
                {
                    string json = File.ReadAllText(path);
                    return JsonUtility.FromJson<FavouritesIndex>(json) ?? new FavouritesIndex();
                });
            }
            catch
            {
                _indexCache = new FavouritesIndex();
            }
            return _indexCache;
        }

        private static Task WriteIndex(FavouritesIndex index)
        {
            EnsureRoot();
            _indexCache = index;
            string path = Path.Combine(RootFolder, IndexFile);
            return Task.Run(() => File.WriteAllText(path, JsonUtility.ToJson(index, true)));
        }



        private static Task<FavouritesState> ReadState(string name)
        {
            string path = StatePath(name);
            if (!File.Exists(path)) return Task.FromResult<FavouritesState>(null);
            return Task.Run(() =>
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonUtility.FromJson<FavouritesState>(json);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static Task WriteState(string name, FavouritesState state)
        {
            EnsureRoot();
            string path = StatePath(name);
            return Task.Run(() => File.WriteAllText(path, JsonUtility.ToJson(state, true)));
        }

    }
}
