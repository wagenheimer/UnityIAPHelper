# Unity IAP Helper

[![UPM](https://img.shields.io/badge/UPM-com.wagenheimer.iaphelper-green.svg)](https://github.com/wagenheimer/UnityIAPHelper)
[![Version](https://img.shields.io/badge/version-1.2.0-blue.svg)](https://github.com/wagenheimer/UnityIAPHelper/releases)
[![Unity](https://img.shields.io/badge/unity-2021.3%2B-black.svg)](https://unity.com)
[![License](https://img.shields.io/badge/license-MIT-purple.svg)](LICENSE)

A production-ready monetization framework for Unity 2021.3+ and Unity 6, built around the modern **Unity IAP v5+ StoreController** (`com.unity.purchasing 5.4.3+`). 

Supports **multi-product catalogs**, **zero-code prefab UI components**, persistent purchase confirmation, a **runtime debug overlay**, and an integrated **Editor Dashboard & Verification Center**.

---

## Key Features

- 🛒 **Zero-Code Prefab Components**: Drop `IAPProductButton` and `IAPRestoreButton` onto UI prefabs. Automatic pricing labels, titles, loading states, and owned-state badging without writing C#.
- 📦 **Multi-Product Engine**: Native support for Consumables, Non-Consumables, and Subscriptions with store SKU overrides (Google Play, Apple App Store, Amazon Appstore).
- ⚡ **Inspector Reward Wiring**: Each product features an `onEntitlementGranted` UnityEvent in the Inspector. Hook up game methods (e.g. `Main.UnlockFullGame()`, `Player.AddCoins(100)`) with zero code.
- 🛡️ **Fail-Safe Two-Step Flow**: Full implementation of Unity IAP v5 mandatory two-step purchase flow (`Pending -> Confirm`). Eliminates lost purchases and orders stuck in "Pending".
- 🔄 **Smart Cross-Platform Restore**: 100% silent auto-restore on Android/Amazon on launch, combined with policy-compliant explicit UI restoration on iOS/macOS (Apple App Store Review Guideline 3.1.1).
- 🔍 **Unified UI Toolkit Dashboard**: `Tools > Wagenheimer > IAP Helper > Dashboard` provides interactive project setup auditing, live product catalog inspection, persistent pre-release store checklists (`EditorPrefs`), and documentation.
- 🎛️ **Native UI Toolkit Custom Inspectors**: `IAPHelperEditor` and `IAPProductButtonEditor` built 100% on UI Toolkit with data-binding, catalog dropdowns, binding status check badges, and 1-click Auto-Resolve.
- 🕹️ **Runtime Debug Overlay**: Built-in in-game panel (`IAPDebugOverlay`) for simulating purchases, revoking entitlements, and verifying store flows in Development Builds and Editor.

---

## Installation

### Via Unity Package Manager (Git URL)
1. In Unity, open **Window > Package Manager**.
2. Click **+ > Add package from git URL...** and enter:
   ```text
   https://github.com/wagenheimer/UnityIAPHelper.git
   ```

### Via `manifest.json`
Add the package directly to `Packages/manifest.json`:
```json
"com.wagenheimer.iaphelper": "https://github.com/wagenheimer/UnityIAPHelper.git"
```

---

## Architecture

```
Unity IAP v5 StoreController
         │
    [IAPHelper] (Singleton / Manager)
         ├── Multi-Product Catalog (Consumable / Non-Consumable / Subscriptions)
         ├── Store SKU Overrides (Google Play, Apple, Amazon)
         ├── Per-Product UnityEvents (Zero-Code Unlocks)
         └── Centralized Two-Step Confirmation & Auto-Restore
         │
         ├──► [IAPProductButton] (Prefab UI Card / Buy Button)
         │        ├── Price, Title, Description, Icon, Loading Indicator
         │        └── Auto-Disabling & Badging when Non-Consumable is Owned
         │
         ├──► [IAPRestoreButton] (Prefab Restore Button)
         │        └── Platform Visibility (Auto-shown on Apple, Auto-hidden on Android)
         │
         ├──► [IAPDebugOverlay] (Runtime In-Game Tester)
         │        └── Real-time inspection, Simulated Grants, and Revokes
         │
         └──► [IAP Helper Dashboard] (Editor Window)
                  ├── Tab 1: Setup & Code Audit
                  ├── Tab 2: Product Catalog & Store SKUs
                  ├── Tab 3: Interactive Store Pre-Flight Checklist
                  └── Tab 4: GitHub Update Checker & Docs
```

---

## Getting Started: Two Ways to Use

### Approach 1: Zero-Code Prefab Workflow (Recommended)

1. **Add `IAPHelper` to your initial scene** (e.g., `preloading` or `Main`):
   - Add a GameObject named `IAPManager` and attach the `IAPHelper` component.
   - In the Inspector, add your products (e.g. `unlockfullgame`, `coins_100`).
   - For each product, optionally configure `playerPrefsFallbackKey` (e.g. `game_unlocked`).
   - Drag your game manager's unlock method (e.g. `Main.UnlockFullGame`) directly into `onEntitlementGranted` on the product entry!

2. **Add `IAPProductButton` to your UI Prefabs**:
   - Attach `IAPProductButton` to any purchase button or shop card.
   - Pick the `productId` from the catalog dropdown in the Inspector.
   - Click **Auto-Resolve** to automatically hook up child `TextMeshProUGUI` labels (price, title) and buttons.
   - Set **Owned State Behavior** (e.g. `DisableButton`, `HideButton`, or `HideGameObject`).

3. **Add `IAPRestoreButton` to your Options or Shop Screen**:
   - Attach `IAPRestoreButton` to your "Restore Purchases" button.
   - Set **Visibility Mode** to `AppleOnly` (default). The button will automatically hide itself on Android and Amazon, and display on iOS/macOS.

---

### Approach 2: Programmatic C# Workflow

For code-driven architectures, `IAPHelper` provides clean, reliable async APIs:

```csharp
using UnityEngine;
using Wagenheimer.IAPHelper;

public class GameInitializer : MonoBehaviour
{
    private async void Start()
    {
        // 1. Hook up the single source of truth for all granted products (live purchase + restore)
        IAPHelper.Instance.OnEntitlementGranted += HandleEntitlementGranted;

        // 2. Hook up local save-game fallback check (for offline boot)
        IAPHelper.HasPurchasedFallback = productId =>
        {
            return productId == "unlockfullgame" && SaveSystem.IsGameUnlocked;
        };

        // 3. Ensure store connection is ready
        bool ready = await IAPHelper.Instance.EnsureInitializedAsync();
        if (ready)
        {
            Debug.Log("Store initialized!");
        }
    }

    private void HandleEntitlementGranted(string productId)
    {
        if (productId == "unlockfullgame")
        {
            SaveSystem.UnlockGame();
            SaveSystem.Save();
        }
        else if (productId == "coins_100")
        {
            Inventory.AddCoins(100);
        }
    }

    public async void OnBuyButtonClicked(string productId)
    {
        var result = await IAPHelper.Instance.PurchaseAsync(productId);
        if (result.IsSuccess)
        {
            Debug.Log("Purchase succeeded!");
        }
        else
        {
            Debug.LogError($"Purchase failed: {result.ErrorMessage}");
        }
    }
}
```

---

## In-Game Runtime Debug Panel (`IAPDebugOverlay`)

Testing IAP in sandbox or on physical test tracks can be tedious. `IAPHelper` includes an in-game debug overlay:

- **Activation**:
  - Add `IAPDebugOverlay` to your scene, or click **Add Debug Overlay** in the `IAPHelper` Inspector.
  - Press `F10` during gameplay or tap the floating **IAP DBG** button.
- **Capabilities**:
  - Inspect store connection state, products loaded, and current runtime platform.
  - Dynamic table of all catalog products with real-time price and ownership status.
  - **Simulate Grant**: Instantly triggers the product's `onEntitlementGranted` event and sets fallback keys.
  - **Revoke Fallback**: Clears local fallback keys to test non-consumable locking/unlocking.
  - **Buy (Store)**: Dispatches a real test transaction to the active store.
  - Automatically disabled in release builds unless explicitly enabled.

---

## Editor Dashboard & Verification Center

Open **Window > Wagenheimer > IAP Helper Dashboard** (or press `Run Audit` from any `IAPHelper` Inspector):

1. **Setup Audit**: Scans project prefabs, scenes, and C# files for misconfigurations:
   - Missing `com.unity.purchasing` package or outdated version.
   - Products referenced in UI forms or buttons that do not exist in the catalog.
   - Unhandled entitlement wiring or missing local fallbacks.
   - One-click **Export Markdown** for CI or agent reports.
2. **Product Catalog**: Live overview of all configured products, types, store SKUs, fallback prices, and persistence keys.
3. **Store Release Checklist**: Interactive pre-flight checklist for **Google Play Console**, **Apple App Store Connect**, and **Amazon Appstore**. Progress persists per project in `EditorPrefs`.
4. **Docs & Updates**: In-editor GitHub version check and quick links to guides.

---

## Cross-Platform Restoration Rules

| Platform | Recommended Behavior | Setting | Reason |
| :--- | :--- | :--- | :--- |
| **Android / Amazon** | **Automatic on boot** | `autoRestorePurchases = true` | The query via Google Play / Amazon is **100% silent** and requires no password. If the player reinstalls or switches devices, non-consumables are recovered immediately on launch. |
| **iOS / macOS (Apple)** | **Manual via button** | `autoRestorePurchases = false` | Apple's **App Store Review Guidelines (3.1.1)** require explicit user action for restoration to prevent unexpected Apple ID password prompts on launch. Use `IAPRestoreButton`. |

`IAPHelper.autoConfigurePlatformRestore` is enabled by default, setting this automatically for you.

---

## CI / Automated Batch Mode

You can run the project audit in CI or headless batch mode:

```bash
Unity -batchmode -quit -projectPath "<your-project-path>" -logFile - \
  -executeMethod Wagenheimer.IAPHelper.Editor.IAPHelperAudit.RunHeadlessAndLog
```

Exits with code `1` if any critical failures (`AuditSeverity.Fail`) are detected.

---

## License

MIT © [Cezar Wagenheimer](https://github.com/wagenheimer)
