# IAP Setup Checklist

Two layers of verification for a project's purchase flow that uses `com.wagenheimer.iaphelper`:

1. **Automated** — `Tools > Wagenheimer > IAP Helper > Verify Setup...` in the Editor (or in batch mode, see
   [Instructions for AI Agents](#instructions-for-ai-agents-claude-code-etc)). Covers everything that can be
   inferred from prefabs, scenes, and source code.
2. **Manual** — items that only exist inside the store consoles (Google Play Console, App Store Connect). No
   local tool can verify these; follow the list below.

Use this entire checklist **before publishing** any build with IAP, and whenever investigating a report like
"I bought it and nothing happened" / "still see ads after buying" / "stuck on a level/gate that should have
been unlocked."

---

## 1. Automated verification (runs in the Editor)

Run `Tools > Wagenheimer > IAP Helper > Verify Setup...`. The tool scans the project and reports:

- [ ] `com.unity.purchasing` installed, version ≥ 5.4.3
- [ ] At least one `IAPHelper` present, either as a prefab/scene component or instantiated at runtime via
      `AddComponent<IAPHelper>()` (and not more than one serialized instance, unless intentional)
- [ ] `IAPHelper.products` is not empty and no `id` is blank or duplicated
- [ ] Every `BaseIAPForm` (purchase form) has `productId` filled in
- [ ] **Every `productId` used by a form exists in some `IAPHelper`'s catalog** — the most common cause of
      the "bought it and nothing changed" bug: the form tries to purchase/check an id that
      `IAPHelper.products` doesn't know about, so `HasPurchased()`/`GetProduct()` never find the product
- [ ] `HasPurchasedFallback` is assigned somewhere in code (integrates with the game's local save data)
- [ ] `OnEntitlementGranted` is being listened to somewhere in code (the permanent event that grants the
      purchased content — see section 3)
- [ ] The installed package is `>= 1.1.0` (includes persistent purchase grant/confirmation; earlier
      versions relied on a temporary listener that expires after 60s or when the purchase screen closes)

Failures (❌) must be fixed before publishing. Warnings (⚠️) deserve a second look but may be intentional
depending on the project.

---

## 2. Manual verification in the stores

None of this is automatable from the Unity project — it only exists inside the store's dashboard.

### Google Play Console

- [ ] The product is registered under **Monetize > Products > In-app products** (or **Subscriptions**, if
      applicable) with the **exact same ID**, character for character (case-sensitive), as the `id` in
      `IAPHelper.products`
- [ ] The product's status is **Active** ("Inactive" products don't show up for purchase and return no price)
- [ ] The app has been uploaded to at least one test track (internal/closed/open/production) with the
      **same `applicationId`** and **signed with the same key** configured for the product
- [ ] The Google account used for testing is registered as a **license tester** (Settings > License testing)
      — without this, test purchases may silently fail or charge real money
- [ ] After a test purchase, the order shows up under **Monetize > Orders** with status **Completed** (not
      "Pending" — an order stuck in "Pending" for a long time is exactly the symptom this package now
      self-heals, but confirming it here closes the loop)
- [ ] If the project targets Amazon Appstore, repeat the above in the Amazon dashboard with the `amazonId`
      configured

### App Store Connect (iOS/macOS)

- [ ] The product is under **App > In-App Purchases**, with the **same Product ID** configured in `appleId`
      (or `id`, if `appleId` is empty)
- [ ] The product's status is **Ready to Submit** (not "Developer Action Needed" / missing metadata)
- [ ] There is an active **Paid Apps Agreement** on the account — without it, NO purchases work, even in
      sandbox
- [ ] Tested with a dedicated **Sandbox Tester** account (not the device's personal Apple ID)
- [ ] `autoRestorePurchases` is `false` on this platform and there is a visible "Restore Purchases" button
      (App Store Review Guidelines requirement — silent automatic restoration is grounds for rejection)

### Both platforms

- [ ] The `productId` used in code is **identical** to the SKU/Product ID registered in the store (copy-paste,
      don't retype — a mismatched case/hyphen/space is the most common mistake)
- [ ] Tested the full **buy → close the app → reopen** cycle to confirm that automatic restoration (Android)
      or the restore button (iOS) actually unlocks the content in a fresh session

---

## 3. How content granting should be wired (from v1.1.0 onward)

```csharp
// At game boot, once:
IAPHelper.Instance.OnEntitlementGranted += productId =>
{
    if (productId == "unlockfullgame")
        UnlockFullGame(); // idempotent: sets the save flag and saves, safe to call more than once
};

IAPHelper.HasPurchasedFallback = id => SaveData != null && SaveData.UnlockedGame && id == "unlockfullgame";
```

`OnEntitlementGranted` is the **single source of truth** for "the player owns this product" — it fires both
for a purchase completed live and for a restore detected at boot or on app resume, and it doesn't depend on
any UI form still being open/alive. Don't implement content unlocking only inside a specific `BaseIAPForm`'s
`GrantPurchasedContent()` — that method should only handle the UI side (closing the dialog, playing an
animation); the global `OnEntitlementGranted` guarantees the game unlocks even if the player never saw that
screen this session.

---

## Instructions for AI Agents (Claude Code, etc.)

If you're a coding agent auditing this project (without access to the Editor UI), reproduce the automated
check by grepping/reading files, and run the real check in batch mode if you have access to the Unity Editor
binary.

### 4.1 Run the real audit via Unity batch mode (preferred)

```bash
"<Unity Editor path>" -batchmode -quit -projectPath "<project path>" -logFile - \
  -executeMethod Wagenheimer.IAPHelper.Editor.IAPHelperAudit.RunHeadlessAndLog
```

This runs the exact same logic as the `Verify Setup...` window, prints a Markdown report to the log, and
exits with code `1` if any critical failure is found (useful for CI gates).

### 4.2 If there's no access to the Unity Editor: reproduce the checks via static reading

1. **Package version**: read `Packages/manifest.json` and `Packages/packages-lock.json`, look for
   `com.wagenheimer.iaphelper`. If the version is `< 1.1.0`, flag it as a failure — that build uses the old
   flow where granting a purchase depends on a temporary listener with a 60s timeout (see this package's
   `CHANGELOG.md` for the exact bug description).

2. **`com.unity.purchasing` installed and its version**: same manifest reading, looking for
   `com.unity.purchasing`; minimum version `5.4.3`.

3. **Product catalog**: `IAPHelper` is either placed on a prefab/scene (serialized `products` list, visible
   as YAML `products:` blocks with `id: <value>` entries under the `IAPHelper` component in `.prefab`/`.unity`
   files) or instantiated at runtime via `gameObject.AddComponent<IAPHelper>()` — in which case its catalog
   is the compiled default (`unlockfullgame`, `NonConsumable`) unless the game's source explicitly assigns
   `.products = ...` somewhere. Grep for both patterns:
   ```bash
   grep -rn "AddComponent<IAPHelper>\|AddComponent(typeof(IAPHelper))" Assets/
   grep -rn "\.products\s*=" Assets/
   ```

4. **Each purchase form's `productId`**: grep `.prefab`/`.unity` files (they're YAML text) for
   `productId: <value>` (a public serialized field on `BaseIAPForm`). Compare the set of values found here
   against the set of `id`s found in step 3 — **any form `productId` that doesn't appear in the IAPHelper's
   product list is a critical failure** (direct cause of the "bought it and nothing changed" symptom).

   Useful command:
   ```bash
   grep -rn "productId:" Assets/ | sort -u
   ```

5. **`HasPurchasedFallback`**: `grep -rn "HasPurchasedFallback\s*=" Assets/` — there should be at least one
   assignment outside the package itself.

6. **`OnEntitlementGranted`**: `grep -rn "OnEntitlementGranted" Assets/` — there should be at least one
   subscription (`+=`) outside the package itself. If only `OnPurchasesFetched +=` exists and not
   `OnEntitlementGranted +=`, flag it as a warning (it works, but only covers restore on boot, not a live
   purchase beyond what the package already resolves internally).

7. **Store-side ID consistency**: this is **not statically verifiable** — just alert the human user to
   manually confirm section 2 of this document (Google Play Console / App Store Connect), since no agent
   without store credentials can validate that.

### 4.3 Investigating a "I bought it and nothing happened" / "still see ads after buying" report

Follow this investigation order (it's the same one that found the original bug fixed in this package's
v1.1.0):

1. Find the gate(s) in the game's code that control ads/progression (search for the save field that
   represents "game unlocked", e.g. `UnlockedGame`) and confirm it only depends on that flag.
2. Find where that flag gets set to `true` (should be inside some `BaseIAPForm`'s `GrantPurchasedContent()`,
   or inside a listener for `OnEntitlementGranted`/`OnPurchasesFetched`).
3. Confirm (step 4.2.4 above) that the `productId` used along that path matches the `IAPHelper`'s catalog.
4. Confirm the package version (step 4.2.1). If it's older than 1.1.0, the most likely cause is the purchase
   confirming after the purchase UI's timeout/closure — the fix is updating the package, not changing the
   game's logic.
5. If everything above checks out, the problem is outside the reach of a static audit: ask the user to check
   section 2 (Google Play Console / App Store Connect) — specifically whether the order shows as "Completed"
   or got stuck as "Pending".
