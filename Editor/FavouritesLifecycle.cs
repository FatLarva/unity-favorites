using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ShoreRat.Editor.Favourites
{
    /// <summary>
    /// Owns scene/prefab-stage subscriptions and debounces rescan requests. A rescan
    /// must be deferred past the event itself — calling GlobalObjectIdentifierToObjectSlow
    /// inside the event trips an internal assertion because the AssetManager is mid-swap.
    /// </summary>
    internal sealed class FavouritesLifecycle : IDisposable
    {
        private readonly Action _onRescan;
        private bool _rescanQueued;
        private bool _alive = true;

        public FavouritesLifecycle(Action onRescan)
        {
            _onRescan = onRescan;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        public void Dispose()
        {
            _alive = false;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => Schedule();
        private void OnSceneClosed(Scene scene) => Schedule();
        private void OnPrefabStageOpened(PrefabStage stage) => Schedule();
        private void OnPrefabStageClosing(PrefabStage stage) => Schedule();

        private void Schedule()
        {
            if (_rescanQueued) return;
            _rescanQueued = true;
            EditorApplication.delayCall += Fire;
        }

        private void Fire()
        {
            _rescanQueued = false;
            if (!_alive) return;
            _onRescan?.Invoke();
        }
    }
}
