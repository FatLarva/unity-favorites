using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesGearMenu
    {
        private readonly Func<Task> _reloadActive;

        public FavouritesGearMenu(Func<Task> reloadActive)
        {
            _reloadActive = reloadActive;
        }

        public void Populate(GenericMenu menu)
        {
            var states = FavouritesStore.ListStates();
            string active = FavouritesStore.GetActiveStateName();

            if (states.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Switch State/(none)"));
            }
            else
            {
                foreach (var stateName in states)
                {
                    string captured = stateName;
                    menu.AddItem(new GUIContent("Switch State/" + stateName), stateName == active,
                        () => SwitchState(captured));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("New State…"),          false, NewStatePrompt);
            menu.AddItem(new GUIContent("Rename Current…"),     false, RenameStatePrompt);

            if (states.Count <= 1)
            {
                menu.AddDisabledItem(new GUIContent("Delete State/(no other states)"));
            }
            else
            {
                foreach (var stateName in states)
                {
                    if (stateName == active)
                    {
                        menu.AddDisabledItem(new GUIContent("Delete State/" + stateName + " (current)"));
                    }
                    else
                    {
                        string captured = stateName;
                        menu.AddItem(new GUIContent("Delete State/" + stateName + "…"), false,
                            () => DeleteStatePrompt(captured));
                    }
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Import…"),           false, ImportState);
            menu.AddItem(new GUIContent("Export Current…"),   false, ExportState);
        }

        // ── Private ──────────────────────────────────────────

        private async void SwitchState(string stateName)
        {
            try
            {
                await FavouritesStore.SwitchTo(stateName);
                await _reloadActive();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void NewStatePrompt()
        {
            FavouritesTextPrompt.Show("New state", "new-state", async n =>
            {
                if (string.IsNullOrWhiteSpace(n)) return;
                try
                {
                    await FavouritesStore.CreateState(n);
                    await FavouritesStore.SwitchTo(n.Trim());
                    await _reloadActive();
                }
                catch (Exception e) { Debug.LogException(e); }
            });
        }

        private void RenameStatePrompt()
        {
            string current = FavouritesStore.GetActiveStateName();
            FavouritesTextPrompt.Show("Rename state", current, async n =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(n)) return;
                    await FavouritesStore.RenameState(current, n.Trim());
                    await _reloadActive();
                }
                catch (Exception e) { Debug.LogException(e); }
            });
        }

        private async void DeleteStatePrompt(string stateName)
        {
            try
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Delete state",
                    $"Delete Favourites state \"{stateName}\"? This cannot be undone.",
                    "Delete", "Cancel");
                if (!ok) return;
                await FavouritesStore.DeleteState(stateName);
                await _reloadActive();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private async void ImportState()
        {
            try
            {
                string path = EditorUtility.OpenFilePanel("Import Favourites state", "", "json");
                if (string.IsNullOrEmpty(path)) return;
                await FavouritesStore.Import(path);
                await _reloadActive();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void ExportState()
        {
            string current = FavouritesStore.GetActiveStateName();
            string path = EditorUtility.SaveFilePanel("Export Favourites state", "", current + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { FavouritesStore.Export(path); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
