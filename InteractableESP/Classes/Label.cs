using RoR2;
using UnityEngine;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace InteractableESP
{
    public class InteractableLabel : MonoBehaviour
    {
        public string Label;
        public Color Color;
        public Vector2 BoundingBoxSize = new(60, 30);
        private Camera _camera;
        private static Texture2D _lineTexture;
        public GameObject Interactable { get; set; }
        public ManualLogSource Logger { get; set; }
        public ConfigEntry<bool> DrawLabelsConfig { get; set; }
        public ConfigEntry<bool> DrawBoundingBoxConfig { get; set; }
        public ConfigEntry<bool> UseAdvancedLabelConfig { get; set; }
        public ConfigEntry<bool> HighContrastConfig { get; set; }
        public ConfigEntry<int> LabelFontSizeConfig { get; set; }

        void Awake()
        {
            _camera = Camera.main;
            if (_lineTexture == null)
            {
                _lineTexture = new Texture2D(1, 1);
                _lineTexture.SetPixel(0, 0, Color.white);
                _lineTexture.Apply();
            }
        }

        void OnGUI()
        {
            if (!_camera) _camera = Camera.main;
            if (!_camera || !enabled || !gameObject.activeInHierarchy) return;

            Vector3 worldPos = transform.position;
            Vector3 screenPos = _camera.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f) return;
            screenPos.y = Screen.height - screenPos.y;

            Rect boxRect = new(screenPos.x - BoundingBoxSize.x / 2, screenPos.y - BoundingBoxSize.y / 2, BoundingBoxSize.x, BoundingBoxSize.y);

            // Draw bounding box if enabled
            if (DrawBoundingBoxConfig?.Value == true)
                DrawRectOutline(boxRect, Color, 2);

            // Only draw label if enabled
            if (DrawLabelsConfig != null && !DrawLabelsConfig.Value) return;

            string advancedInfo = GetAdvancedInfo();
            float distance = Vector3.Distance(_camera.transform.position, worldPos);
            string labelText = $"<color=#{ColorUtility.ToHtmlStringRGB(Color)}>{Label}</color>\n<color=#FFFFFF>{Mathf.RoundToInt(distance)}m</color>{advancedInfo}";

            GUIStyle style = new()
            {
                fontSize = LabelFontSizeConfig?.Value ?? 14,
                alignment = TextAnchor.UpperCenter,
                richText = true,
                normal = new GUIStyleState { textColor = Color.white }
            };
            Vector2 size = style.CalcSize(new GUIContent(labelText));
            Rect rect = new(boxRect.center.x - size.x / 2f, boxRect.yMin - size.y, size.x, size.y);

            if (HighContrastConfig?.Value == true)
            {
                var outlineStyle = new GUIStyle(style);
                outlineStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x - 1, rect.y, rect.width, rect.height), labelText, outlineStyle); // Left
                GUI.Label(new Rect(rect.x + 1, rect.y, rect.width, rect.height), labelText, outlineStyle); // Right
                GUI.Label(new Rect(rect.x, rect.y - 1, rect.width, rect.height), labelText, outlineStyle); // Up
                GUI.Label(new Rect(rect.x, rect.y + 1, rect.width, rect.height), labelText, outlineStyle); // Down
            }

            // Draw main label
            GUI.Label(rect, labelText, style);
        }

        private string GetAdvancedInfo()
        {
            if (UseAdvancedLabelConfig == null || !UseAdvancedLabelConfig.Value || Interactable == null) return "";
            var info = "";

            var purchase = Interactable.GetComponent<PurchaseInteraction>();
            if (purchase != null &&
                !purchase.displayNameToken.Contains("DUPLICATOR") &&
                !purchase.displayNameToken.Contains("SHRINE") &&
                !purchase.displayNameToken.Contains("BAZAAR_CAULDRON"))
                info += $" <color=#FFFFFF>- ${purchase.cost}</color>";

            // 3D Printer
            if (purchase != null && (purchase.displayNameToken.Contains("DUPLICATOR") || purchase.displayNameToken.Contains("BAZAAR_CAULDRON")))
            {
                var shopTerminal = Interactable.GetComponent<ShopTerminalBehavior>();
                info += FormatPickup(shopTerminal?.pickupIndex, prefix: "\n\n");
            }

            // Chest
            var chest = Interactable.GetComponent<ChestBehavior>();
            if (chest != null)
                info += FormatPickup(chest.dropPickup, prefix: "\n\n");

            // MultiShop
            var multiShop = Interactable.GetComponent<MultiShopController>();
            if (multiShop != null)
            {
                info += $" <color=#FFFFFF>- ${multiShop.cost}</color>\n";
                foreach (var terminal in multiShop.terminalGameObjects)
                {
                    var shopTerminal = terminal.GetComponent<ShopTerminalBehavior>();
                    info += FormatPickup(shopTerminal?.pickupIndex, prefix: "\n");
                }
            }
            return info;
        }

        private string FormatPickup(PickupIndex? pickupIndex, string prefix = "\n")
        {
            if (pickupIndex == null || pickupIndex == PickupIndex.none) return "";
            var pickupDef = PickupCatalog.GetPickupDef(pickupIndex.Value);
            if (pickupDef == null) return "";

            if (pickupDef.itemIndex != ItemIndex.None)
            {
                var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
                if (itemDef != null)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(GetTierColor(itemDef.tier));
                    string itemName = Language.GetString(itemDef.nameToken);
                    return $"{prefix}<color=#{hex}>{itemName}</color>";
                }
            }
            else if (pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                var equipDef = EquipmentCatalog.GetEquipmentDef(pickupDef.equipmentIndex);
                if (equipDef != null)
                {
                    Color equipColor = equipDef.isLunar ? Color.cyan : new(1f, 0.5f, 0f);
                    string hex = ColorUtility.ToHtmlStringRGB(equipColor);
                    string equipName = Language.GetString(equipDef.nameToken);
                    return $"{prefix}<color=#{hex}>{equipName}</color>";
                }
            }
            return "";
        }

        private void DrawRectOutline(Rect rect, Color color, int thickness)
        {
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), _lineTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), _lineTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), _lineTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), _lineTexture);
            GUI.color = prevColor;
        }

        private static Color GetTierColor(ItemTier tier) => (int)tier switch
        {
            0 => Color.white, // Tier1
            1 => Color.green, // Tier2
            2 => Color.red,   // Tier3
            4 => Color.yellow, // Boss
            3 => Color.cyan,  // Lunar
            6 => new(0.988f, 0.725f, 0.925f), // Void
            7 => new(0.776f, 0.47f, 0.705f),  // Pearl
            8 => new(0.584f, 0.043f, 0.458f), // Corrupt
            _ => Color.white
        };
    }
}