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

- **Unity IAP v5 Native**: Built for `com.unity.purchasing 5.4.3+` with StoreController v5 lifecycle.
- **Two-Step Purchase Flow**: Mandatory pending confirmation prevents lost purchases and fulfills platform requirements.
- **Auto-Update Checker**: Built-in editor notification when newer releases are published on GitHub.
- **Decoupled Architecture**: Easily integrate with any game save system via `IAPHelper.HasPurchasedFallback`.
- **Restore Transactions**: One-click restore handling for Apple App Store (RestoreTransactions) and Google Play (FetchPurchases).

## License

MIT (c) Cezar Wagenheimer
