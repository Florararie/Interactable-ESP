using RoR2;
using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;

namespace InteractableESP
{
    // Plugin metadata
    public static class PluginInfo
    {
        public const string PluginAuthor = "Floramene";
        public const string PluginName = "InteractableESP";
        public const string PluginVersion = "1.1.0";
        public const string PluginGUID = PluginAuthor + "." + PluginName;
    }

    [BepInPlugin(PluginInfo.PluginGUID, PluginInfo.PluginName, PluginInfo.PluginVersion)]
    public class ESP : BaseUnityPlugin
    {
        public static ESP Instance { get; private set; }
        private static ManualLogSource _log;

        private readonly Dictionary<string, ConfigEntry<Color>> _colorConfigs = new();
        private readonly Dictionary<string, ConfigEntry<bool>> _colorEnabledConfigs = new();
        private Dictionary<string, Color> _tokenColorMap;
        private ConfigEntry<KeyCode> _highlightToggleKey, _guiToggleKey;
        private ConfigEntry<bool> _drawLabels, _drawBoundingBox, _useAdvancedLabel, _highlightInteractables, _highContrastText;
        private ConfigEntry<int> _labelFontSize;
        private bool _highlightEnabled = true;

        // Properties for GUI
        public bool DrawLabels { get => _drawLabels.Value; set => _drawLabels.Value = value; }
        public bool DrawBoundingBox { get => _drawBoundingBox.Value; set => _drawBoundingBox.Value = value; }
        public bool UseAdvancedLabel { get => _useAdvancedLabel.Value; set => _useAdvancedLabel.Value = value; }
        public bool HighlightInteractablesEnabled { get => _highlightInteractables.Value; set => _highlightInteractables.Value = value; }
        public bool HighContrastText { get => _highContrastText.Value; set => _highContrastText.Value = value; }
        public int LabelFontSize { get => _labelFontSize.Value; set => _labelFontSize.Value = value; }
        public KeyCode GUIToggleKey { get => _guiToggleKey.Value; set => _guiToggleKey.Value = value; }
        public KeyCode HighlightToggleKey { get => _highlightToggleKey.Value; set => _highlightToggleKey.Value = value; }

        private void Awake()
        {
            Instance = this;
            _log = Logger;
            _log.LogInfo("Interactable ESP plugin loaded.");

            InitializeConfig();
            Stage.onStageStartGlobal += _ => ScheduleHighlight();
            gameObject.AddComponent<ConfigGUI>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_highlightToggleKey.Value))
            {
                _highlightEnabled = !_highlightEnabled;
                _log.LogInfo($"Interactable ESP toggled {(_highlightEnabled ? "ON" : "OFF")}");
                ToggleAllHighlights(_highlightEnabled);
            }
        }

        private void ToggleAllHighlights(bool enable)
        {
            if (!enable)
            {
                foreach (var h in GameObject.FindObjectsOfType<Highlight>()) h.isOn = false;
                foreach (var l in GameObject.FindObjectsOfType<InteractableLabel>()) Destroy(l);
            }
            else
            {
                HighlightAllInteractables();
            }
        }

        private void ScheduleHighlight()
        {
            CancelInvoke(nameof(HighlightAllInteractables));
            Invoke(nameof(HighlightAllInteractables), 2f);
        }

        private void InitializeConfig()
        {
            _highlightToggleKey = Config.Bind("General", "ToggleHighlightKey", KeyCode.F3, "Key to toggle interactable highlights / labels on/off");
            _guiToggleKey = Config.Bind("General", "ToggleGUIKey", KeyCode.F4, "Key to open/close the ESP settings GUI");
            _highlightInteractables = Config.Bind("General", "HighlightInteractables", true, "Whether to highlight interactable items");
            _drawLabels = Config.Bind("General", "DrawLabels", false, "Whether to draw labels on interactables");
            _drawBoundingBox = Config.Bind("General", "DrawBoundingBox", false, "Whether to draw a bounding box around interactables");
            _useAdvancedLabel = Config.Bind("General", "UseAdvancedLabel", false, "Whether to show advanced label info (cost, item, etc.)");
            _highContrastText = Config.Bind("General", "HighContrastText", false, "Whether to use high contrast text for labels");
            _labelFontSize = Config.Bind("General", "LabelFontSize", 14, new ConfigDescription("Font size for the label text", new AcceptableValueRange<int>(8, 48)));

            _drawLabels.SettingChanged += (sender, args) => UpdateAllLabelsAndBoxes();
            _drawBoundingBox.SettingChanged += (sender, args) => UpdateAllLabelsAndBoxes();

            // Color configs and enabled states
            AddColorConfig("Default", "FFFFFFFF", "Default highlight color");
            AddColorConfig("Chest", "FFEA04FF", "Chest highlight color");
            AddColorConfig("Barrel", "FF5804FF", "Barrel highlight color");
            AddColorConfig("Shop", "19FF04FF", "Shop terminal highlight color");
            AddColorConfig("Printer", "FF04D9FF", "3D printer highlight color");
            AddColorConfig("Lunar", "01FFFFFF", "Lunar Chest / Newt Statue highlight color");
            AddColorConfig("Drone", "FF0400FF", "Drone / Turret highlight color");
            AddColorConfig("Teleporter", "8E38FFFF", "Teleporter highlight color");

            RefreshTokenColorMap();
        }

        private void AddColorConfig(string key, string hex, string desc)
        {
            _colorConfigs[key] = Config.Bind("Colors", $"{key}Color", HexToColor(hex), desc);
            _colorEnabledConfigs[key] = Config.Bind("Colors", $"{key}Enabled", true, $"Enable ESP for {key} interactables");
        }

        private static Color HexToColor(string hex) =>
            ColorUtility.TryParseHtmlString($"#{hex}", out var color) ? color : Color.white;

        public Dictionary<string, ConfigEntry<Color>> GetColorConfigs() => _colorConfigs;
        public Dictionary<string, ConfigEntry<bool>> GetColorEnabledConfigs() => _colorEnabledConfigs;

        public void HighlightAllInteractables()
        {
            if (!_highlightEnabled) return;
            HighlightInteractables<PurchaseInteraction>(i => i.displayNameToken);
            HighlightInteractables<BarrelInteraction>(_ => "BARREL_NAME");
            HighlightInteractables<ScrapperController>(_ => "SCRAPPER_NAME");
            HighlightInteractables<MultiShopController>(_ => "TRIPLE_SHOP");
            HighlightInteractables<PressurePlateController>(_ => "SECRET_BUTTON");
            HighlightInteractables<TeleporterInteraction>(_ => "TELEPORTER_NAME");
            HighlightInteractables<GenericPickupController>(i => i.GetDisplayName());
            HighlightInteractables<GeodeController>(i => i.GetDisplayName());
        }

        private void HighlightInteractables<T>(System.Func<T, string> tokenSelector) where T : Component
        {
            foreach (var interactable in GameObject.FindObjectsOfType<T>())
                HighlightInteractable(interactable.gameObject, tokenSelector(interactable));
        }

        private void HighlightInteractable(GameObject interactable, string token)
        {
            if (!interactable) return;

            string colorGroup = GetColorGroupForToken(token);
            if (colorGroup != null && !_colorEnabledConfigs[colorGroup].Value)
                return;

            var color = _tokenColorMap.TryGetValue(token, out var c) ? c : _colorConfigs["Default"].Value;

            if (HighlightInteractablesEnabled)
            {
                var highlight = interactable.GetComponent<Highlight>() ?? interactable.AddComponent<Highlight>();
                highlight.enabled = highlight.isOn = true;

                if (!highlight.targetRenderer)
                {
                    highlight.targetRenderer = GetRendererForInteractable(interactable, token);
                    if (!highlight.targetRenderer)
                    {
                        _log.LogWarning($"No renderer for {interactable.name}");
                        return;
                    }
                }

                highlight.strength = 1f;
                highlight.highlightColor = Highlight.HighlightColor.custom;
                highlight.CustomColor = color;
            }

            var cleaner = interactable.GetComponent<HighlightCleaner>() ?? interactable.AddComponent<HighlightCleaner>();
            cleaner.target = interactable;

            if (token == "MULTISHOP_TERMINAL_NAME") return;
            bool needLabel = _drawLabels.Value || _drawBoundingBox.Value;
            UpdateLabelForInteractable(interactable, token, needLabel, color);
        }

        public void UpdateAllLabelsAndBoxes()
        {
            foreach (var interactable in GetAllInteractables())
            {
                string token = Utils.GetTokenForGameObject(interactable);
                if (token == "`")
                    continue;

                var color = _tokenColorMap != null && token != null && _tokenColorMap.TryGetValue(token, out var c) ? c : _colorConfigs["Default"].Value;
                bool needLabel = _drawLabels.Value || _drawBoundingBox.Value;
                UpdateLabelForInteractable(interactable, token, needLabel, color);
            }
        }

        private void UpdateLabelForInteractable(GameObject interactable, string token, bool needLabel, Color color)
        {
            if (token == "MULTISHOP_TERMINAL_NAME") return;
            var marker = interactable.transform.Find("ESPMarker") ?? CreateMarker(interactable.transform);
            var label = marker.GetComponent<InteractableLabel>();

            if (needLabel)
            {
                if (label == null)
                    label = marker.gameObject.AddComponent<InteractableLabel>();
                string friendlyName = InteractableDisplayNames.Map.TryGetValue(token, out var name) ? name : token;
                label.Label = friendlyName;
                label.Color = color;
                label.BoundingBoxSize = new Vector2(40, 40);
                label.Interactable = interactable;
                label.Logger = _log;
                label.DrawLabelsConfig = _drawLabels;
                label.DrawBoundingBoxConfig = _drawBoundingBox;
                label.UseAdvancedLabelConfig = _useAdvancedLabel;
                label.HighContrastConfig = _highContrastText;
                label.LabelFontSizeConfig = _labelFontSize;
            }
            else if (label != null)
            {
                Destroy(label);
            }
        }

        private static Renderer GetRendererForInteractable(GameObject interactable, string token)
        {
            if (token == "BARREL_NAME")
            {
                var modelTransform = interactable.GetComponent<ModelLocator>()?.modelTransform;
                return modelTransform ? modelTransform.GetComponentInChildren<Renderer>(true) : null;
            }
            return interactable.GetComponentInChildren<Renderer>();
        }

        private static Transform CreateMarker(Transform parent)
        {
            var markerObj = new GameObject("ESPMarker");
            markerObj.transform.SetParent(parent, false);
            markerObj.transform.localPosition = Vector3.up * 1.0f;
            return markerObj.transform;
        }

        private string GetColorGroupForToken(string token)
        {
            foreach (var pair in _colorConfigs)
            {
                if (_tokenColorMap.TryGetValue(token, out var mappedColor) && mappedColor == pair.Value.Value)
                    return pair.Key;
            }
            return null;
        }

        public void RefreshTokenColorMap()
        {
            _tokenColorMap = new Dictionary<string, Color>
            {
                {"CHEST1_NAME", _colorConfigs["Chest"].Value},
                {"CHEST2_NAME", _colorConfigs["Chest"].Value},
                {"LOCKBOX_NAME", _colorConfigs["Chest"].Value},
                {"CASINOCHEST_NAME", _colorConfigs["Chest"].Value},
                {"GOLDCHEST_NAME", _colorConfigs["Chest"].Value},
                {"CHEST1_STEALTHED_NAME", _colorConfigs["Chest"].Value},
                {"LUNAR_CHEST_NAME", _colorConfigs["Lunar"].Value},
                {"BARREL_NAME", _colorConfigs["Barrel"].Value},
                {"EQUIPMENTBARREL_NAME", _colorConfigs["Chest"].Value},
                {"CATEGORYCHEST_HEALING_NAME", _colorConfigs["Chest"].Value},
                {"CATEGORYCHEST_DAMAGE_NAME", _colorConfigs["Chest"].Value},
                {"CATEGORYCHEST_UTILITY_NAME", _colorConfigs["Chest"].Value},
                {"CATEGORYCHEST2_HEALING_NAME", _colorConfigs["Chest"].Value},
                {"CATEGORYCHEST2_DAMAGE_NAME", _colorConfigs["Chest"].Value},
                {"CATEGORYCHEST2_UTILITY_NAME", _colorConfigs["Chest"].Value},
                {"MULTISHOP_TERMINAL_NAME", _colorConfigs["Shop"].Value},
                {"TRIPLE_SHOP", _colorConfigs["Shop"].Value},
                {"DUPLICATOR_NAME", _colorConfigs["Printer"].Value},
                {"DUPLICATOR_WILD_NAME", _colorConfigs["Printer"].Value},
                {"DUPLICATOR_MILITARY_NAME", _colorConfigs["Printer"].Value},
                {"BAZAAR_CAULDRON_NAME", _colorConfigs["Printer"].Value},
                {"SCRAPPER_NAME", _colorConfigs["Printer"].Value},
                {"RADIOTOWER_NAME", _colorConfigs["Drone"].Value},
                {"TURRET1_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"DRONE_GUNNER_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"DRONE_HEALING_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"EMERGENCYDRONE_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"DRONE_MISSILE_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"EQUIPMENTDRONE_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"FLAMEDRONE_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"DRONE_MEGA_INTERACTABLE_NAME", _colorConfigs["Drone"].Value},
                {"NEWT_STATUE_NAME", _colorConfigs["Lunar"].Value},
                {"SECRET_BUTTON", _colorConfigs["Lunar"].Value},
                {"MOON_BATTERY_MASS_NAME", _colorConfigs["Lunar"].Value},
                {"MOON_BATTERY_DESIGN_NAME", _colorConfigs["Lunar"].Value},
                {"MOON_BATTERY_SOUL_NAME", _colorConfigs["Lunar"].Value},
                {"MOON_BATTERY_BLOOD_NAME", _colorConfigs["Lunar"].Value},
                {"LUNAR_TERMINAL_NAME", _colorConfigs["Lunar"].Value},
                {"LUNAR_REROLL_NAME", _colorConfigs["Lunar"].Value},
                {"BAZAAR_SEER_NAME", _colorConfigs["Lunar"].Value},
                {"TELEPORTER_NAME", _colorConfigs["Teleporter"].Value}
            };
        }

        private IEnumerable<GameObject> GetAllInteractables()
        {
            foreach (var i in GameObject.FindObjectsOfType<PurchaseInteraction>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<BarrelInteraction>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<ScrapperController>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<MultiShopController>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<PressurePlateController>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<TeleporterInteraction>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<GenericPickupController>()) yield return i.gameObject;
            foreach (var i in GameObject.FindObjectsOfType<GeodeController>()) yield return i.gameObject;
        }

        public void RemoveHighlightsAndLabelsForColorGroup(string colorGroup)
        {
            foreach (var highlight in GameObject.FindObjectsOfType<Highlight>())
            {
                var go = highlight.gameObject;
                string token = Utils.GetTokenForGameObject(go);
                if (token == null) continue;
                if (GetColorGroupForToken(token) == colorGroup)
                {
                    highlight.isOn = false;
                    Destroy(highlight);
                }
            }

            foreach (var label in GameObject.FindObjectsOfType<InteractableLabel>())
            {
                var go = label.Interactable;
                if (go == null) continue;
                string token = Utils.GetTokenForGameObject(go);
                if (token == null) continue;
                if (GetColorGroupForToken(token) == colorGroup)
                {
                    Destroy(label);
                }
            }
        }
    }
}