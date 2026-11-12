using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Wagenheimer.IAPHelper
{
    /// <summary>
    /// Creates and initializes the shared <see cref="IAPHelper"/> from code, for games that keep their
    /// product ids in a constants class instead of wiring the component in a scene.
    /// <para>
    /// It prefers a serialized prefab under <c>Resources</c> (so Inspector wiring, store SKU overrides and
    /// <c>globalEvents</c> still win when the project has one) and falls back to a programmatic catalog of
    /// non-consumable products built from the given ids.
    /// </para>
    /// </summary>
    public static class IAPBootstrap
    {
        /// <summary>Default Resources path looked up for a serialized IAPHelper prefab.</summary>
        public const string DefaultPrefabResourcePath = "Monetization/IAPHelper";

        private static IAPHelper helper;

        /// <summary>
        /// Returns the existing helper or creates one, then initializes the store connection.
        /// Safe to call more than once. A helper that already exists is returned untouched.
        /// </summary>
        /// <param name="nonConsumableProductIds">
        /// Catalog used only when no prefab is found. Store SKUs equal the product id; set the per-store
        /// overrides on a prefab when an id differs.
        /// </param>
        /// <param name="prefabResourcePath">Resources path of an optional serialized helper prefab.</param>
        public static IAPHelper Ensure(
            IEnumerable<string> nonConsumableProductIds,
            string prefabResourcePath = DefaultPrefabResourcePath)
        {
            if (IAPHelper.Instance != null)
                return IAPHelper.Instance;

            if (helper != null)
                return helper;

            var prefab = string.IsNullOrEmpty(prefabResourcePath)
                ? null
                : Resources.Load<GameObject>(prefabResourcePath);

            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab);
                Object.DontDestroyOnLoad(instance);
                helper = instance.GetComponent<IAPHelper>();
            }

            if (helper == null)
            {
                var host = new GameObject("[IAPHelper]");
                Object.DontDestroyOnLoad(host);
                helper = host.AddComponent<IAPHelper>();

                // Awake already ran (singleton + platform restore). Configure the catalog and initialize
                // explicitly so Start() does not initialize a second time with the default catalog.
                helper.initializeOnStart = false;
                helper.autoConfigurePlatformRestore = true;
                helper.processPendingOnFetch = true;
                helper.products = BuildCatalog(nonConsumableProductIds);
            }

            helper.Initialize();
            return helper;
        }

        private static List<ProductConfig> BuildCatalog(IEnumerable<string> productIds)
        {
            var catalog = new List<ProductConfig>();
            if (productIds == null)
                return catalog;

            foreach (string productId in productIds)
            {
                if (string.IsNullOrEmpty(productId))
                    continue;

                catalog.Add(new ProductConfig
                {
                    id = productId,
                    type = ProductType.NonConsumable,
                    googlePlayId = "",
                    appleId = "",
                    amazonId = "",
                    titleFallback = "",
                    descriptionFallback = "",
                    priceFallback = "",
                    playerPrefsFallbackKey = ""
                });
            }

            return catalog;
        }
    }
}
