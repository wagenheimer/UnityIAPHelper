# Unity IAP Helper

[![UPM](https://img.shields.io/badge/UPM-com.wagenheimer.iaphelper-green.svg)](https://github.com/wagenheimer/UnityIAPHelper)

Production-ready Unity In-App Purchasing (v5+) helper for Unity 2021.3+ / Unity 6. Designed specifically for the Unity IAP v5 mandatory two-step purchase flow (`Pending -> Confirm`).

## Installation

In Unity, open **Window > Package Manager**, click **+ > Add package from git URL...** and enter:

```
https://github.com/wagenheimer/UnityIAPHelper.git
```

Or add directly to your `Packages/manifest.json`:

```json
"com.wagenheimer.iaphelper": "https://github.com/wagenheimer/UnityIAPHelper.git"
```

## Features

- **Unity IAP v5 Native**: Built for `com.unity.purchasing 5.4.3+` with the StoreController v5 lifecycle.
- **Two-Step Purchase Flow**: Mandatory pending confirmation prevents lost purchases and fulfills modern store requirements.
- **Persistent Purchase Fulfillment**: Granting content and confirming orders is handled centrally by `IAPHelper` itself (not tied to a purchase-button UI's lifetime), plus an automatic re-fetch on app resume — so a purchase that confirms after the buy dialog closed (or after the app was minimized during the store checkout) is never lost. See `OnEntitlementGranted`.
- **Auto-Update Checker**: Built-in editor notification when newer releases are published on GitHub.
- **Built-in Setup Auditor**: `Tools > Wagenheimer > IAP Helper > Verify Setup...` scans your project for the most common IAP misconfigurations (mismatched product IDs, empty catalogs, missing save-system wiring, outdated package version) — also runnable headless for CI/AI agents. See `IAP-CHECKLIST.md`.
- **Decoupled Architecture**: Easily integrate with any game save system via `IAPHelper.HasPurchasedFallback`.
- **Cross-Platform Purchase Restoration**: Automatic background restoration on Android/Amazon and explicit compliance with Apple App Store guidelines on iOS/macOS.

---

## Purchase Restoration (Android vs iOS / macOS)

### How this used to work with `IAPListener` (Unity IAP v3/v4):
In the old Unity IAP, the `IAPListener` component listened to the store's initialization (`IStoreListener.OnInitialized`). Google Play sent the receipts and Unity IAP automatically called `ProcessPurchase` on boot for every existing Non-Consumable product, firing `onPurchaseComplete` and unlocking the game on reinstall.

### How it works in Unity IAP v5 with `IAPHelper`:
In Unity IAP v5, querying previous purchases is done via `FetchPurchases()`, which fires the `OnPurchasesFetched(Orders orders)` event.

| Platform | Recommended Behavior | Setting | Reason |
| :--- | :--- | :--- | :--- |
| **Android / Amazon** | **Automatic on boot** | `autoRestorePurchases = true` | The query via Google Play / Amazon is **100% silent** and requires no password. If the player reinstalls the game or switches devices, non-consumable purchases (e.g. "Unlock Full Game") are recovered immediately on launch. |
| **iOS / macOS (Apple)** | **Manual via button** | `autoRestorePurchases = false` | Apple's **App Store Review Guidelines** forbid triggering a restore that could prompt for Apple ID credentials without explicit user consent. On iOS/macOS, use a "Restore Purchases" button that calls `IAPHelper.RestorePurchases()`. |

### Recommended Setup at Game Boot:

```csharp
if (!TryGetComponent<IAPHelper>(out IAPHelper))
{
    IAPHelper = gameObject.AddComponent<IAPHelper>();
}

// 1. Smart platform-based auto-restore:
//    - Android/Amazon: true (silent and automatic on reinstall)
//    - iOS/macOS: false (manual via UI button)
IAPHelper.autoRestorePurchases = IAPHelper.RecommendedAutoRestoreForCurrentPlatform;

// 2. Single source of truth for "product granted": fires for both a live purchase and a
//    restore (boot or app-resume re-fetch), even if the purchase screen has already closed.
IAPHelper.OnEntitlementGranted += HandleEntitlementGranted;

// 3. Fallback for checking whether the game is already unlocked in the local save
IAPHelper.HasPurchasedFallback = id => SaveData != null && SaveData.UnlockedGame && id == "unlockfullgame";
```

### Example `HandleEntitlementGranted` Callback:

```csharp
private void HandleEntitlementGranted(string productId)
{
    if (productId == "unlockfullgame")
        UnlockFullGame(); // idempotent: sets the save flag and saves, safe to call more than once
}
```

---

## License

MIT (c) Cezar Wagenheimer
