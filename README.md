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

## Restauração de Compras (Android vs iOS / macOS)

### Como funcionava antigamente com `IAPListener` (Unity IAP v3/v4):
No Unity IAP antigo, o componente `IAPListener` escutava a inicialização da loja (`IStoreListener.OnInitialized`). O Google Play enviava os recibos e o Unity IAP chamava `ProcessPurchase` automaticamente no boot para todos os produtos Não-Consumíveis existentes, disparando `onPurchaseComplete` e liberando o jogo ao reinstalar.

### Como funciona no Unity IAP v5 com `IAPHelper`:
No Unity IAP v5, a consulta de compras anteriores é feita via `FetchPurchases()`, que dispara o evento `OnPurchasesFetched(Orders orders)`.

| Plataforma | Comportamento Recomendado | Configuração | Motivo |
| :--- | :--- | :--- | :--- |
| **Android / Amazon** | **Automático no boot** | `autoRestorePurchases = true` | A consulta via Google Play / Amazon é **100% silenciosa** e não exige senha. Se o jogador reinstalar o jogo ou trocar de aparelho, as compras não-consumíveis (ex: "Desbloquear Jogo Completo") são recuperadas imediatamente na abertura. |
| **iOS / macOS (Apple)** | **Manual via Botão** | `autoRestorePurchases = false` | As **App Store Review Guidelines** da Apple proíbem acionar restauração que possa solicitar credenciais de Apple ID sem consentimento explícito do usuário. No iOS/macOS deve-se usar um botão "Restaurar Compras" que chama `IAPHelper.RestorePurchases()`. |

### Configuração Recomendada no Boot do Jogo:

```csharp
if (!TryGetComponent<IAPHelper>(out IAPHelper))
{
    IAPHelper = gameObject.AddComponent<IAPHelper>();
}

// 1. Configura auto-restore inteligente por plataforma:
//    - Android/Amazon: true (silencioso e automático ao reinstalar)
//    - iOS/macOS: false (manual via botão UI)
IAPHelper.autoRestorePurchases = IAPHelper.RecommendedAutoRestoreForCurrentPlatform;

// 2. Callback disparado quando as compras da loja forem buscadas
IAPHelper.OnPurchasesFetched += HandlePurchasesFetched;

// 3. Fallback para checar se o jogo já está desbloqueado no Save local
IAPHelper.HasPurchasedFallback = id => SaveData != null && SaveData.UnlockedGame && id == "unlockfullgame";
```

### Exemplo de Callback `HandlePurchasesFetched`:

```csharp
private void HandlePurchasesFetched(Orders orders)
{
    // Verifica se o usuário já possui o produto 'unlockfullgame' confirmado na loja
    if ((IAPHelper != null && IAPHelper.HasPurchased("unlockfullgame")) || 
        (orders != null && orders.ConfirmedOrders.Count > 0))
    {
        UnlockFullGame();
    }
}
```

---

## License

MIT (c) Cezar Wagenheimer
