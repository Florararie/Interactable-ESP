using RoR2;
using UnityEngine;

namespace InteractableESP
{
    public class HighlightCleaner : MonoBehaviour
    {
        public GameObject target;
        private float shrineUnavailableSince = -1f;
        private const float ShrineUnavailableDelay = 3f;

        void Update()
        {
            if (!target) { Destroy(this); return; }

            var purchase = target.GetComponent<PurchaseInteraction>();
            if (purchase != null &&(purchase.displayNameToken.Contains("DUPLICATOR") || purchase.displayNameToken.Contains("BAZAAR_CAULDRON")))
                return;

            var shrine = target.GetComponent<ShrineBehavior>();
            var multiShop = target.GetComponent<MultiShopController>();

            if (shrine != null)
            {
                if (purchase != null && !purchase.available)
                {
                    if (shrineUnavailableSince < 0f)
                        shrineUnavailableSince = Time.unscaledTime;
                    if (Time.unscaledTime - shrineUnavailableSince > ShrineUnavailableDelay)
                    {
                        Cleanup();
                        return;
                    }
                    return;
                }
                else
                {
                    shrineUnavailableSince = -1f;
                }
            }
            else if (multiShop != null && multiShop.available == false)
            {
                Cleanup();
                return;
            }
            else if ((purchase && !purchase.available) ||
                (target.GetComponent<ChestBehavior>()?.isChestOpened == true) ||
                (target.GetComponent<BarrelInteraction>()?.opened == true) ||
                (target.GetComponent<ShopTerminalBehavior>()?.hasBeenPurchased == true))
            {
                Cleanup();
            }
        }

        private void Cleanup()
        {
            if (target.TryGetComponent<Highlight>(out var highlight))
            {
                highlight.isOn = false;
                Destroy(highlight);
            }
            var marker = target.transform.Find("ESPMarker");
            if (marker != null) Destroy(marker.gameObject);
            Destroy(this);
        }
    }
}