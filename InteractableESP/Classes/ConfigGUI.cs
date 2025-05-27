using RoR2;
using UnityEngine;
using BepInEx.Configuration;

namespace InteractableESP
{
    public class ConfigGUI : MonoBehaviour
    {
        private bool showWindow, waitingForHighlightKey, waitingForGUIKey, showColorPicker, colorPickerHexChangedByUser;
        private Rect windowRect = new(100, 100, 420, 600), colorPickerRect = new(400, 200, 260, 340);
        private string colorPickerKey, colorPickerHex = "";
        private Color colorPickerValue;
        private static Texture2D _previewTex;

        void Update()
        {
            if (Input.GetKeyDown(ESP.Instance.GUIToggleKey))
                showWindow = !showWindow;
        }

        void OnGUI()
        {
            if (!showWindow) return;
            windowRect = GUI.Window(123456, windowRect, DrawWindow, "Interactable ESP Settings");
            if (showColorPicker && colorPickerKey != null)
                colorPickerRect = GUI.Window(654321, colorPickerRect, DrawColorPickerWindow, $"Pick Color: {colorPickerKey}");
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("<b>General</b>", GetHeaderStyle());
            DrawToggleButton("Draw Labels", () => ESP.Instance.DrawLabels, v => ESP.Instance.DrawLabels = v);
            DrawToggleButton("Draw Bounding Box", () => ESP.Instance.DrawBoundingBox, v => ESP.Instance.DrawBoundingBox = v);
            DrawToggleButton("Use Advanced Label", () => ESP.Instance.UseAdvancedLabel, v => ESP.Instance.UseAdvancedLabel = v);
            DrawToggleButton("High Contrast Text", () => ESP.Instance.HighContrastText, v => ESP.Instance.HighContrastText = v);
            DrawToggleButton("Highlight Interactables", () => ESP.Instance.HighlightInteractablesEnabled, v =>
            {
                ESP.Instance.HighlightInteractablesEnabled = v;
                if (!v)
                    foreach (var h in GameObject.FindObjectsOfType<Highlight>()) h.isOn = false;
                else
                    ESP.Instance.HighlightAllInteractables();
            });

            GUILayout.Space(10);
            GUILayout.Label("Label Font Size: " + ESP.Instance.LabelFontSize);
            int newFontSize = (int)GUILayout.HorizontalSlider(ESP.Instance.LabelFontSize, 8, 48);
            if (newFontSize != ESP.Instance.LabelFontSize)
                ESP.Instance.LabelFontSize = newFontSize;

            GUILayout.Space(10);
            GUILayout.Label("<b>Key Bindings</b>", GetHeaderStyle());
            DrawKeyBinding("Toggle Highlight Key", () => ESP.Instance.HighlightToggleKey, k => ESP.Instance.HighlightToggleKey = k, ref waitingForHighlightKey);
            DrawKeyBinding("Toggle GUI Key", () => ESP.Instance.GUIToggleKey, k => ESP.Instance.GUIToggleKey = k, ref waitingForGUIKey);

            GUILayout.Space(10);
            GUILayout.Label("<b>Colors</b>", GetHeaderStyle());
            var colorConfigs = ESP.Instance.GetColorConfigs();
            var colorEnabledConfigs = ESP.Instance.GetColorEnabledConfigs();
            foreach (var kvp in colorConfigs)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(kvp.Key, GUILayout.Width(100));
                bool enabled = colorEnabledConfigs[kvp.Key].Value;
                string enabledLabel = enabled ? "Enabled" : "Disabled";
                bool newEnabled = GUILayout.Toggle(enabled, enabledLabel, GUILayout.Width(80));
                if (newEnabled != enabled)
                {
                    colorEnabledConfigs[kvp.Key].Value = newEnabled;
                    if (!newEnabled)
                        ESP.Instance.RemoveHighlightsAndLabelsForColorGroup(kvp.Key);
                    else
                        ESP.Instance.HighlightAllInteractables();
                }
                Color prev = GUI.color;
                GUI.color = kvp.Value.Value;
                if (GUILayout.Button("", GUILayout.Width(32), GUILayout.Height(18)))
                {
                    showColorPicker = true;
                    colorPickerKey = kvp.Key;
                    colorPickerValue = kvp.Value.Value;
                    colorPickerHex = ColorToHex(colorPickerValue);
                    colorPickerHexChangedByUser = false;
                }
                GUI.color = prev;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("Press " + ESP.Instance.GUIToggleKey + " to close this window.");
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void DrawColorPickerWindow(int id)
        {
            GUILayout.BeginVertical();
            DrawColorSlider("Red", ref colorPickerValue.r);
            DrawColorSlider("Green", ref colorPickerValue.g);
            DrawColorSlider("Blue", ref colorPickerValue.b);
            DrawColorSlider("Alpha", ref colorPickerValue.a);

            GUILayout.Space(10);
            GUILayout.Label("Hex (RRGGBBAA):");
            GUI.SetNextControlName("HexInputField");
            string newHex = GUILayout.TextField(colorPickerHex, 8, GUILayout.Width(100));
            bool hexChanged = newHex != colorPickerHex;
            colorPickerHex = newHex.ToUpper();

            if (hexChanged)
            {
                colorPickerHexChangedByUser = true;
                if (TryParseHexColor(colorPickerHex, out var parsed))
                    colorPickerValue = parsed;
            }
            else if (Event.current.type == EventType.Repaint && !colorPickerHexChangedByUser)
                colorPickerHex = ColorToHex(colorPickerValue);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                colorPickerHexChangedByUser = false;
                GUI.FocusControl(null);
            }
            if (Event.current.type == EventType.MouseDown && GUI.GetNameOfFocusedControl() != "HexInputField")
                colorPickerHexChangedByUser = false;

            GUILayout.Space(5);
            GUILayout.Label("Preview:");
            Rect previewRect = GUILayoutUtility.GetRect(60, 24);
            if (_previewTex == null)
            {
                _previewTex = new Texture2D(1, 1);
                _previewTex.SetPixel(0, 0, Color.white);
                _previewTex.Apply();
            }
            Color prevColor = GUI.color;
            GUI.color = colorPickerValue;
            GUI.DrawTexture(previewRect, _previewTex);
            GUI.color = prevColor;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply"))
            {
                var colorConfigs = ESP.Instance.GetColorConfigs();
                if (colorConfigs.ContainsKey(colorPickerKey))
                    colorConfigs[colorPickerKey].Value = colorPickerValue;

                ESP.Instance.RefreshTokenColorMap();
                ESP.Instance.HighlightAllInteractables();

                showColorPicker = false;
                colorPickerKey = null;
                colorPickerHexChangedByUser = false;
            }
            if (GUILayout.Button("Cancel"))
            {
                showColorPicker = false;
                colorPickerKey = null;
                colorPickerHexChangedByUser = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawToggleButton(string label, System.Func<bool> getter, System.Action<bool> setter)
        {
            if (GUILayout.Button($"{label}: {(getter() ? "ON" : "OFF")}"))
                setter(!getter());
        }

        private void DrawKeyBinding(string label, System.Func<KeyCode> getter, System.Action<KeyCode> setter, ref bool waiting)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {getter()}", GUILayout.Width(220));
            if (!waiting)
            {
                if (GUILayout.Button("Change", GUILayout.Width(80)))
                    waiting = true;
            }
            else
            {
                GUILayout.Label("Press any key...", GUILayout.Width(120));
                if (Event.current.isKey && Event.current.type == EventType.KeyDown)
                {
                    setter(Event.current.keyCode);
                    waiting = false;
                    Event.current.Use();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawColorSlider(string label, ref float value)
        {
            GUILayout.Label(label);
            float prev = value;
            value = GUILayout.HorizontalSlider(value, 0, 1);
            if (!colorPickerHexChangedByUser && value != prev)
                colorPickerHex = ColorToHex(colorPickerValue);
        }

        private string ColorToHex(Color c) =>
            $"{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}{(int)(c.a * 255):X2}";

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.white;
            return hex.Length == 8 && ColorUtility.TryParseHtmlString("#" + hex, out color);
        }

        private GUIStyle GetHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold };
            return style;
        }
    }
}