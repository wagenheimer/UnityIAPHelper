# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-09-17

### Fixed
- `IAPHelperAudit` could throw **"It is not allowed to open a scene in a read-only package"** when a scene
  bundled inside an installed package (under `Packages/...` or resolved into
  `Library/PackageCache/...`) was picked up by `AssetDatabase.FindAssets("t:Scene")`. Scene and prefab
  scanning is now restricted to paths under the consuming project's own `Assets/` folder.
- `IAPHelperAudit` reported a false **"No IAPHelper component found"** (and, as a result, false "productId
  doesn't exist in any catalog" failures for every purchase form) whenever `IAPHelper` was instantiated
  purely via `gameObject.AddComponent<IAPHelper>()` in code rather than placed as a serialized component on a
  prefab/scene — a fully supported and common pattern. The audit now also detects that pattern via a source
  scan and, when found, reads the component's default product catalog from a temporary scratch instance
  instead of failing.
- Localized the entire package (Runtime, Editor, README, CHANGELOG, `IAP-CHECKLIST.md`) to English; it
  previously mixed English API/doc-comments with Portuguese runtime log messages and Editor tooling text.

## [1.1.0] - 2026-09-17

### Fixed
- **Purchases could get stuck as "Pending" forever.** Granting content and confirming the order
  (`ConfirmPurchase`) depended on a local listener created inside `PurchaseAsync`, which unsubscribed once the
  purchase resolved or after a 60s timeout. If the store's confirmation arrived later than that — app
  minimized during native checkout, pending/deferred payment, slow 2FA — nothing in the project would call
  `ConfirmPurchase`/grant the content again, even in future sessions. Typical symptom: "I bought it and
  nothing happened" / content/ad gates never lift even though the payment confirmed.

### Added
- `IAPHelper.OnEntitlementGranted` (`Action<string>`): a permanent event, fired once per product, that
  replaces the need to grant content from a temporary UI listener. Covers live purchases, restore on boot,
  and re-fetch on app resume.
- Granting and confirming pending purchases is now done centrally in `IAPHelper`
  (`HandlePurchasePending` / `HandlePurchasesFetched`), regardless of whether any `BaseIAPForm` is open on
  screen.
- Automatic purchase re-fetch (`FetchPurchases`) on app resume (`OnApplicationPause` / `OnApplicationFocus`),
  with a small debounce window — recovers orders that confirmed while the app was minimized in the store's
  native checkout.
- `IAPHelperAudit` / **Tools > Wagenheimer > IAP Helper > Verify Setup...** window: automated project audit
  (product catalog, each form's `productId` cross-referenced against the catalog,
  `HasPurchasedFallback`/`OnEntitlementGranted` wiring, `com.unity.purchasing` version, this package's
  version). Also runs headless via
  `-executeMethod Wagenheimer.IAPHelper.Editor.IAPHelperAudit.RunHeadlessAndLog` for CI or AI agents without
  access to the Editor UI.
- `IAP-CHECKLIST.md`: a full checklist (automated + manual, including the items that only exist in the
  Google Play / App Store consoles) and a dedicated investigation guide for AI agents.

### Changed
- `PurchaseAsync` no longer grants content directly from the `OnPurchasePending` event; it now listens to
  `OnEntitlementGranted` only to sync UI feedback. The public API did not change —
  `BaseIAPForm` and existing game code keep working without modification.

## [1.0.0] - 2026-09-06

### Added
- Initial UPM package release for Unity IAP v5 (`com.unity.purchasing 5.4.3+`).
- Two-step purchase confirmation architecture (`PendingOrder` -> grant content -> `ConfirmPurchase`).
- Safe asynchronous initialization with timeout protection (`EnsureInitializedAsync`).
- Entitlement checking and restore transaction support across iOS and Android.
- In-Editor automated update checking and one-click package updater window.
- Decoupled `HasPurchasedFallback` hook for seamless game save integration.
