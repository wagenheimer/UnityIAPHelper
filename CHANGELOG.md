# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-06

### Added
- Initial UPM package release for Unity IAP v5 (`com.unity.purchasing 5.4.3+`).
- Two-step purchase confirmation architecture (`PendingOrder` -> grant content -> `ConfirmPurchase`).
- Safe asynchronous initialization with timeout protection (`EnsureInitializedAsync`).
- Entitlement checking and restore transaction support across iOS and Android.
- In-Editor automated update checking and one-click package updater window.
- Decoupled `HasPurchasedFallback` hook for seamless game save integration.
