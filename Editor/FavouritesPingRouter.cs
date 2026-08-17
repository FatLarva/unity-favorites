using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesPingRouter
    {
        private readonly Action _refreshList;

        public FavouritesPingRouter(Action refreshList)
        {
            _refreshList = refreshList;
        }

        public void Activate(FavouritesItemEntry entry, bool pingOnly)
        {
            if (entry == null) return;

            if (entry.Object != null)
            {
                if (ShouldPromptExitPrefabStage(entry.Object))
                {
                    bool exit = EditorUtility.DisplayDialog(
                        "Exit prefab mode?",
                        $"'{entry.Object.name}' lives outside the prefab you are editing.\n\nExit prefab mode to reveal it?",
                        "Exit", "Cancel");
                    if (!exit) return;
                    StageUtility.GoToMainStage();
                }

                if (pingOnly) PingObject(entry.Object);
                else OpenItem(entry.Object);
                return;
            }

            if (!entry.HasContainer) return;

            string containerPath = FavouritesEntryFactory.GetContainerPath(entry);
            if (string.IsNullOrEmpty(containerPath))
            {
                EditorUtility.DisplayDialog("Container missing",
                    $"Could not locate the container asset for '{entry.DisplayName}'. It may have been moved or deleted.",
                    "OK");
                return;
            }

            switch (entry.ContainerKind)
            {
                case FavouritesContainerKind.Scene:  PromptSceneLoad(entry, containerPath, pingOnly);  break;
                case FavouritesContainerKind.Prefab: PromptPrefabOpen(entry, containerPath, pingOnly); break;
            }
        }

        // ── Private ──────────────────────────────────────────

        private void PromptSceneLoad(FavouritesItemEntry entry, string scenePath, bool pingOnly)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Scene not loaded",
                $"'{entry.DisplayName}' lives in:\n{scenePath}\n\nHow would you like to load it?",
                "Change scene", "Cancel", "Load additive");

            if (choice == 1) return;

            try
            {
                if (choice == 0)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                else
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
            }
            catch (Exception e) { Debug.LogException(e); return; }

            ReResolveAndActivate(entry, pingOnly);
        }

        private void PromptPrefabOpen(FavouritesItemEntry entry, string prefabPath, bool pingOnly)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Prefab not open",
                $"'{entry.DisplayName}' is inside:\n{prefabPath}\n\nOpen the prefab to ping it?",
                "Open", "Cancel");
            if (!ok) return;

            var asset = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
            if (asset == null) return;
            AssetDatabase.OpenAsset(asset);

            ReResolveAndActivate(entry, pingOnly);
        }

        private void ReResolveAndActivate(FavouritesItemEntry entry, bool pingOnly)
        {
            if (!GlobalObjectId.TryParse(entry.Gid, out var gid)) return;

            Object resolved = null;
            try { resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid); }
            catch { resolved = null; }

            if (resolved == null) return;

            entry.Object = resolved;
            _refreshList();

            if (pingOnly) PingObject(resolved);
            else OpenItem(resolved);
        }

        private static bool ShouldPromptExitPrefabStage(Object obj)
        {
            if (obj == null || EditorUtility.IsPersistent(obj)) return false;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return false;
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go != null && go.scene == stage.scene) return false;
            return true;
        }

        private static void PingObject(Object obj)
        {
            if (obj == null) return;
            Selection.activeObject = obj;

            if (AssetDatabase.Contains(obj))
            {
                EditorUtility.FocusProjectWindow();
            }
            else
            {
                var hierarchyType = Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor");
                if (hierarchyType != null)
                {
                    var existing = Resources.FindObjectsOfTypeAll(hierarchyType);
                    if (existing is { Length: > 0 }) ((EditorWindow)existing[0]).Focus();
                }
            }

            EditorGUIUtility.PingObject(obj);
        }

        private static void OpenItem(Object obj)
        {
            if (obj == null) return;
            if (AssetDatabase.Contains(obj)) { AssetDatabase.OpenAsset(obj); return; }
            Selection.activeObject = obj;
            SceneView.FrameLastActiveSceneView();
        }
    }
}
