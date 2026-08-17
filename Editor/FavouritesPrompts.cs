using System;
using UnityEditor;
using UnityEngine;

namespace ShoreRat.Editor.Favourites
{
    internal static class FavouritesPromptPlacement
    {
        private static Vector2? _lastScreenMouse;

        public static void UpdateScreenMouse(Vector2 screenMouse) => _lastScreenMouse = screenMouse;

        public static Rect NearCursor(float width, float height)
        {
            Vector2 screenMouse;
            if (_lastScreenMouse.HasValue)
            {
                screenMouse = _lastScreenMouse.Value;
            }
            else
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                screenMouse = new Vector2(main.x + main.width * 0.5f, main.y + main.height * 0.5f);
            }
            return new Rect(screenMouse.x - width * 0.5f, screenMouse.y - 10f, width, height);
        }
    }

    public class FavouritesTextPrompt : EditorWindow
    {
        private string _value;
        private Action<string> _onConfirm;

        public static void Show(string title, string initialValue, Action<string> onConfirm)
        {
            var window = CreateInstance<FavouritesTextPrompt>();
            window.titleContent = new GUIContent(title);
            window._value = initialValue;
            window._onConfirm = onConfirm;
            window.position = FavouritesPromptPlacement.NearCursor(260, 70);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            GUI.SetNextControlName("FavField");
            _value = EditorGUILayout.TextField("Name", _value);

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel")) Close();

                if (GUILayout.Button("OK") || (Event.current.isKey && Event.current.keyCode == KeyCode.Return))
                {
                    _onConfirm?.Invoke(_value);
                    Close();
                }
            }

            EditorGUI.FocusTextInControl("FavField");
        }
    }

    public class FavouritesTabConfigPrompt : EditorWindow
    {
        private string _name;
        private Color _color;
        private bool _useThumbnails;
        private Action<string, Color, bool> _onConfirm;

        public static void Show(string initialName, Color initialColor, bool initialUseThumbnails, Action<string, Color, bool> onConfirm)
        {
            var window = CreateInstance<FavouritesTabConfigPrompt>();
            window.titleContent = new GUIContent("Configure Tab");
            window._name = initialName;
            window._color = initialColor;
            window._useThumbnails = initialUseThumbnails;
            window._onConfirm = onConfirm;
            window.position = FavouritesPromptPlacement.NearCursor(280, 120);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            GUI.SetNextControlName("FavTabNameField");
            _name = EditorGUILayout.TextField("Name", _name);
            _color = EditorGUILayout.ColorField("Color", _color);
            _useThumbnails = EditorGUILayout.Toggle("Thumbnails", _useThumbnails);

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel")) Close();

                if (GUILayout.Button("OK") || (Event.current.isKey && Event.current.keyCode == KeyCode.Return))
                {
                    _onConfirm?.Invoke(_name, _color, _useThumbnails);
                    Close();
                }
            }

            EditorGUI.FocusTextInControl("FavTabNameField");
        }
    }
}
