using System;

namespace Wagenheimer.IAPHelper
{
    /// <summary>
    /// "Free up to N, then unlock" rule for freemium games: levels, chapters, stages or any 1-based
    /// index that is playable for free up to <see cref="FreeLimit"/> and stays behind a paywall until a
    /// one-time purchase (non-consumable) is owned.
    /// <para>
    /// The gate holds no state of its own. Ownership comes from <see cref="IAPHelper.HasPurchased"/> (which
    /// already includes <see cref="IAPHelper.HasPurchasedFallback"/>, the game save) or from an optional
    /// <c>isUnlocked</c> callback, so it works offline and before the store answers. The class is plain C#
    /// and does not need a scene object, which keeps it easy to unit test.
    /// </para>
    /// </summary>
    public sealed class FreemiumGate
    {
        private readonly Func<bool> _isUnlocked;

        /// <param name="productId">Non-consumable that removes the limit.</param>
        /// <param name="freeLimit">Highest index playable for free. Zero or less means no limit.</param>
        /// <param name="isUnlocked">
        /// Optional extra ownership source (for example the game save, or "full game" builds that never
        /// sell the product). The gate is open when this returns true OR the store reports the product.
        /// </param>
        public FreemiumGate(string productId, int freeLimit, Func<bool> isUnlocked = null)
        {
            ProductId = productId;
            FreeLimit = freeLimit;
            _isUnlocked = isUnlocked;
        }

        public string ProductId { get; }

        /// <summary>Highest index playable for free. Zero or less means the build has no limit.</summary>
        public int FreeLimit { get; }

        /// <summary>True when the build restricts content at all.</summary>
        public bool HasLimit => FreeLimit > 0;

        /// <summary>True when the one-time unlock is owned (store, restore, game save or custom source).</summary>
        public bool IsUnlocked
        {
            get
            {
                if (_isUnlocked != null && _isUnlocked())
                    return true;

                var helper = IAPHelper.Instance;
                return helper != null && !string.IsNullOrEmpty(ProductId) && helper.HasPurchased(ProductId);
            }
        }

        /// <summary>True when <paramref name="index"/> (1-based) is behind the paywall.</summary>
        public bool IsPaywalled(int index) => HasLimit && index > FreeLimit && !IsUnlocked;

        /// <summary>How many free items are still available after reaching <paramref name="reachedIndex"/>.</summary>
        public int RemainingFree(int reachedIndex) =>
            !HasLimit || IsUnlocked ? int.MaxValue : Math.Max(0, FreeLimit - reachedIndex);
    }
}
