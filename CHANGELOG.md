# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.10.0] - 2026-09-25

### Added
- Debug overlay zoom for phones: A-/A+ header buttons (0.75x-3x, saved in PlayerPrefs), a maximize button, and a mobile default zoom (`mobileDefaultScale`, now actually applied). Zoom uses a runtime clone of the PanelSettings.

## [1.9.1] - 2026-09-24

### Fixed
- Debug overlay did not render in player builds: no ThemeStyleSheet was available at runtime. The package now ships a PanelSettings + default runtime theme in Runtime/Resources/Wagenheimer (a project-level Resources/Wagenheimer/DebugPanelSettings still takes precedence).

## [1.9.0] - 2026-09-24

### Added
- **UI Toolkit In-Game Overlay (`IAPDebugOverlay`)**: Fully migrated the runtime debug HUD from legacy IMGUI (`OnGUI()`) to native UI Toolkit (`UIDocument`).
  - Sleek dark-slate design system with draggable floating window, minimize controls, and high-DPI scaling.
  - Interactive floating launcher badge (`🛒 IAP DBG`) with live store connection dot indicator.
  - Live store status badges: `STORE CONNECTED` / `DISCONNECTED`, `PRODUCTS LOADED`, and `AUTO-RESTORE`.
  - Comprehensive products catalog with ownership breakdown (Store owned, PlayerPrefs key, Fallback delegate) and live store prices.
  - Interactive simulation controls: `✓ Simulate Grant`, `✕ Revoke Local`, `↻ Revoke & Restore`, and real store purchase trigger `💳 Buy`.
  - Global QA actions: `⟳ Force Init`, `⇩ Restore All`, `🗑 Clear All Keys`, and copyable diagnostic state report.
  - Real-time event log with color-coded chips for purchases, grants, revokes, and connection callbacks.

## [1.8.1] - 2026-09-23

### Fixed
- Fixed UI Toolkit style compilation errors on element borders by applying `.iap-catalog-card` and `.iap-store-chip` USS classes.
- Fixed `ProductConfig.googlePlayId` property name reference in `IAPHelperCatalogView`.
- Fixed `AuditResult.Detail` and `FixHint` property name references in `IAPHelperAuditView`.

## [1.8.0] - 2026-09-23

### Added
- **UI Toolkit Design System**: Modernized interface with `IAPHelperCommon.uss` and `IAPHelperUIStyle.cs` featuring cards, severity badges (`pass`, `warn`, `fail`, `info`), product type pills (`Consumable`, `NonConsumable`, `Subscription`), and syntax-highlighted code boxes.
- **Modular Dashboard Architecture**: Converted `IAPHelperDashboardWindow` from legacy `OnGUI()` to UI Toolkit `CreateGUI()` hosting 4 dedicated modular views:
  - `IAPHelperAuditView`: Interactive setup audit runner with category grouping and severity filters.
  - `IAPHelperCatalogView`: Live catalog inspector displaying configured products, type badges, store SKU overrides, and entitlement listeners.
  - `IAPHelperChecklistView`: Persistent store release checklist for Google Play Console and Apple App Store Connect stored in `EditorPrefs`.
  - `IAPHelperDocsView`: Unity IAP v5 architecture guide, code snippets, and update checker.
- **UI Toolkit Custom Inspectors**:
  - `IAPHelperEditor`: Converted to native `CreateInspectorGUI()` with hero banner, product card lists, and platform auto-restore settings.
  - `IAPProductButtonEditor`: Converted to native `CreateInspectorGUI()` with catalog ID dropdown, binding status check badges, and 1-click Auto-Resolve.
- **UI Toolkit Audit Window**: Modernized `IAPHelperAuditWindow` with UI Toolkit `CreateGUI()`.

## [1.7.0] - 2026-09-20

### Added
- AI prompts for the setup audit: every Fail, Warning and actionable Info finding now carries a ready-to-paste task (`AuditResult.Prompt`) that states the reason, the evidence and the expected automatic fix, and asks the agent to explain the cause, apply the safest fix and re-run the audit to confirm.
- Dashboard: a per-item "Copy AI prompt" button and a "Copy AI prompt (N)" summary action that bundles every pending warning/error into one prompt. Same actions added to the legacy verification window.

### Fixed
- Dashboard: the version badge and the "Installed Version" line were hardcoded to 1.2.0; both now read the real installed package version.

## [1.6.0] - 2026-09-19

### Added
- Auto-installs `com.wagenheimer.packagehub` via git if missing, using a zero-dependency Editor bootstrap assembly (`PackageHubBootstrap`). Installing this package now pulls in PackageHub automatically, with no manual manifest edits or scoped registry required.

### Changed
- Reverted the `com.wagenheimer.packagehub` OpenUPM registry dependency added in 1.5.5: it required every consumer to configure a scoped registry manually, which defeats the "install one package, get everything" goal. The git-based auto-bootstrap replaces it.

## [1.5.5] - 2026-09-19

### Changed
- Re-added `com.wagenheimer.packagehub` as a proper semver dependency (`1.0.4`) now that it is published on the [OpenUPM registry](https://openupm.com/packages/com.wagenheimer.packagehub/). Consumers need the `com.wagenheimer` scope added to their `scopedRegistries`.

## [1.5.4] - 2026-09-18

### Fixed
- Removed `com.wagenheimer.packagehub` from `dependencies` in package.json: UPM does not support a git URL as a dependency version, which made this package fail to resolve/update in any consuming project. PackageHub must still be added directly to the consumer's manifest.json.

## [1.5.3] - 2026-09-18

### Changed
- Standardized menu item priorities under `Tools > Wagenheimer > IAP Helper` (base priority 120) for cohesive editor grouping and ordering.
- Updated `com.wagenheimer.packagehub` dependency to `v1.0.4`.

## [1.5.2] - 2026-09-18

### Changed
- **Centralized Update Management**: Replaced standalone update checker with dependency on `com.wagenheimer.packagehub` (`UnityPackageHub`). Updates, changelogs, and package management are now handled centrally through the unified Wagenheimer Package Hub.

## [1.5.1] - 2026-09-18

### Changed
- **Update Window Redesign**: Complete visual overhaul of `UpdateAvailableWindow` with modern slate header banner, pill badge, version diff card (`Installed: vX.Y.Z ➔ Latest: vA.B.C`), rich-text markdown release notes parser (`✦ Added`, `✔ Fixed`, `⚡ Changed`, styled bullets), and fixed layout scrolling.
- **Multi-Version Release Notes**: Enhanced `ExtractVersionNotes` in `UpdateChecker` to extract cumulative notes across intermediate versions and gracefully fallback to the latest changelog section instead of showing empty notes.

## [1.5.0] - 2026-09-18

### Added
- **IAPDebugOverlay: scale & maximize**: The panel is IMGUI, which doesn't respect device DPI — on phones it used to render tiny. Added `mobileDefaultScale`/`desktopDefaultScale` (auto-picked per platform), `A-`/`A+` buttons in the header to adjust it live (persisted via `PlayerPrefs`), and a `⛶` maximize toggle that expands the panel to fill the screen. The product list also grows to use the extra space when maximized.

## [1.4.0] - 2026-09-18

### Added
- **`ProductConfig.onEntitlementRevoked` (`UnityEvent`)**: Wire refund/cancellation handling per product directly in the Inspector, with zero code — mirrors `onEntitlementGranted`. Fired for both the real Apple StoreKit revocation path and `DebugRevokeEntitlement`/`DebugResetAndRestore`.
- **Inspector: live event wiring summary**: Each product's Granted/Revoked cards now list exactly which `object.method()` is bound (with a "Ping" button to select it), and show a green/red dot next to the product name — even while collapsed — so an unwired event is impossible to miss.
- **Inspector: product search**: A filter field appears once a catalog has more than 3 products.

### Changed
- **Grant/revoke bookkeeping centralized**: `IAPHelper` now funnels all revocation handling (dedup cache, PlayerPrefs fallback key clearing, event dispatch) through a single internal `RevokeEntitlement()`, shared by the real store path and the debug/QA tools.
- **Inspector & Debug Overlay redesign**: Colorful, card-based layout with a consistent accent-color palette across the custom Inspector and the in-game `IAPDebugOverlay` (F10).

### Fixed
- **Duplicate section headers in the custom Inspector**: `[Header(...)]` attributes on `ProductConfig`/`IAPHelper` fields were rendering alongside the custom-drawn section titles, showing every section name twice (e.g. "Store SKU Overrides" then "Store SKU Overrides (Optional)"). Removed the redundant attributes — every section now has exactly one title.

## [1.3.0] - 2026-09-18

### Added
- **Auto-attach `IAPDebugOverlay`**: New `enableDebugOverlay` field on `IAPHelper`. When enabled, the overlay is automatically attached in the Unity Editor and Development Builds — no scene setup or code required. Controlled via the **Debug & QA** section in the Inspector.

### Changed
- **Audit performance**: `IAPHelperAudit.FindAllComponents` no longer opens every project scene via `EditorSceneManager.OpenScene`. The audit now scans prefabs and any currently open scenes only, eliminating editor freezes on large projects. Full multi-scene scan is available as an explicit opt-in ("Re-run Audit" after opening the relevant scenes).
- **Dashboard opens instantly**: `IAPHelperDashboardWindow.OnEnable` no longer auto-runs the audit. The Setup Audit tab shows a prompt with a "Run Setup Audit" button, so the window opens without delay.
- **Menu cleanup**: Removed the duplicate `Window/Wagenheimer/IAP Helper Dashboard` menu item. Dashboard is now accessed exclusively via `Tools/Wagenheimer/IAP Helper/Dashboard`.
- **Inspector toolbar**: Replaced the manual "Add Debug Overlay" button with a read-only status label showing whether the overlay is currently active. Overlay attachment is automatic via the `enableDebugOverlay` field.
- **Audit fix hint**: "No IAPHelper found" fix hint now says to add the component to the persistent Main/Bootstrap prefab — no longer suggests `AddComponent` in code.
- **Product Catalog tab**: Updated the "no IAPHelper found" message to guide users toward using a Bootstrap prefab instead of a manager scene.

## [1.2.0] - 2026-09-17

### Added
- **Zero-Code Prefab UI Components**:
  - `IAPProductButton`: Drop-in component for UI prefabs that binds to buttons, localized price labels, title, description, loading indicators, and owned badges with configurable behavior when owned (disable, hide, custom badge).
  - `IAPRestoreButton`: Platform-compliant restore button that automatically appears on iOS/macOS (mandated by Apple App Store Review Guideline 3.1.1) and auto-hides on Android/Amazon.
  - `IAPDebugOverlay`: Universal in-game runtime debug menu for Development Builds and Editor, allowing real-time store inspection, simulated purchases, fallback clearing, and store testing for all products.
- **Multi-Product Engine & Inspector Rewards**:
  - `ProductConfig.onEntitlementGranted` (`UnityEvent`): Wire game unlocks and currency rewards per product directly in the Unity Inspector without writing C# boilerplate.
  - `ProductConfig.playerPrefsFallbackKey`: Optional automatic local save key for non-consumable products, checked seamlessly by `HasPurchased()`.
  - Fallback metadata fields (`titleFallback`, `descriptionFallback`, `priceFallback`) for offline display and Editor UI design.
  - `IAPHelper.autoConfigurePlatformRestore`: Automatic platform-aware auto-restore configuration.
- **Unified Editor Dashboard (`Window > Wagenheimer > IAP Helper Dashboard`)**:
  - **Setup Audit**: Project-wide static verification with severity filtering, fix hints, and Markdown export.
  - **Product Catalog**: Live visual table of configured products across platforms.
  - **Store Release Checklist**: Interactive pre-flight checklist for Google Play, App Store Connect, and Amazon with local `EditorPrefs` persistence.
  - **Docs & Updates**: Built-in GitHub update checker and documentation links.
- **Apple & Store Platform Extensions**:
  - `OnPromotionalPurchaseIntercepted`: Intercepts Apple App Store page promotional purchases and safely calls `ContinuePromotionalPurchases()` to prevent order stalls.
  - `OnEntitlementRevoked`: Listens to Apple StoreKit 2 refund/revocation events, clearing local ownership and fallback keys automatically.
  - `PresentAppleCodeRedemptionSheet()`: Helper to display the Apple promo/offer code redemption sheet.
  - Subscription verification: checks `subscriptionInfo.IsSubscribed() == Result.True` for active subscriptions.
- **Custom Modern Inspectors**:
  - `IAPHelperEditor`: Redesigned inspector with status diagnostics and visual product management.
  - `IAPProductButtonEditor`: Catalog dropdown selector, binding status badges, and 1-click component auto-resolve.

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
