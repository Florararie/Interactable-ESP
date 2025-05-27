using RoR2;
using UnityEngine;

namespace InteractableESP
{
    public static class Utils
    {
        public static string GetTokenForGameObject(GameObject go)
        {
            var purchase = go.GetComponent<PurchaseInteraction>();
            if (purchase != null) return purchase.displayNameToken;
            if (go.GetComponent<BarrelInteraction>() != null) return "BARREL_NAME";
            if (go.GetComponent<ScrapperController>() != null) return "SCRAPPER_NAME";
            if (go.GetComponent<MultiShopController>() != null) return "TRIPLE_SHOP";
            if (go.GetComponent<PressurePlateController>() != null) return "SECRET_BUTTON";
            if (go.GetComponent<TeleporterInteraction>() != null) return "TELEPORTER_NAME";
            return null;
        }
    }
}