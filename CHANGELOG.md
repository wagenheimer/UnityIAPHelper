# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-09-17

### Fixed
- **Compras podiam ficar presas como "Pending" para sempre.** A concessão de conteúdo e a confirmação da
  ordem (`ConfirmPurchase`) dependiam de um listener local criado dentro de `PurchaseAsync`, que se
  desinscrevia após a compra resolver ou depois de um timeout de 60s. Se a confirmação da loja chegasse
  depois disso — app minimizado durante o checkout nativo, pagamento pendente/diferido, 2FA demorado — nada
  no projeto voltava a chamar `ConfirmPurchase`/conceder o conteúdo, mesmo em sessões futuras. Sintoma típico:
  "comprei e não aconteceu nada" / gates de conteúdo/anúncios nunca liberam mesmo após o pagamento confirmar.

### Added
- `IAPHelper.OnEntitlementGranted` (`Action<string>`): evento permanente, disparado uma vez por produto,
  que substitui a necessidade de conceder conteúdo a partir de um listener temporário de UI. Cobre compra ao
  vivo, restauração no boot e reconsulta ao retomar o foco do app.
- Concessão e confirmação de compras pendentes agora são feitas centralmente em `IAPHelper`
  (`HandlePurchasePending` / `HandlePurchasesFetched`), independentemente de qualquer `BaseIAPForm` estar
  aberto na tela.
- Reconsulta automática de compras (`FetchPurchases`) ao retomar o foco do app (`OnApplicationPause` /
  `OnApplicationFocus`), com uma pequena janela de debounce — recupera pedidos que confirmaram enquanto o
  app estava minimizado no checkout nativo da loja.
- `IAPHelperAudit` / janela **Tools > Wagenheimer > IAP Helper > Verify Setup...**: auditoria automática do
  projeto (catálogo de produtos, `productId` de cada form comparado ao catálogo, wiring de
  `HasPurchasedFallback`/`OnEntitlementGranted`, versão do `com.unity.purchasing`, versão deste pacote).
  Também roda headless via `-executeMethod Wagenheimer.IAPHelper.Editor.IAPHelperAudit.RunHeadlessAndLog`
  para CI ou agentes de IA sem acesso à UI do Editor.
- `IAP-CHECKLIST.md`: checklist completo (automatizável + manual, incluindo os itens que só existem nas
  consoles do Google Play / App Store) e um roteiro de investigação dedicado a agentes de IA.

### Changed
- `PurchaseAsync` não concede mais conteúdo diretamente a partir do evento `OnPurchasePending`; passou a
  escutar `OnEntitlementGranted` apenas para sincronizar feedback de UI. A API pública não mudou —
  `BaseIAPForm` e código de jogo existente continuam funcionando sem alteração.

## [1.0.0] - 2026-09-06

### Added
- Initial UPM package release for Unity IAP v5 (`com.unity.purchasing 5.4.3+`).
- Two-step purchase confirmation architecture (`PendingOrder` -> grant content -> `ConfirmPurchase`).
- Safe asynchronous initialization with timeout protection (`EnsureInitializedAsync`).
- Entitlement checking and restore transaction support across iOS and Android.
- In-Editor automated update checking and one-click package updater window.
- Decoupled `HasPurchasedFallback` hook for seamless game save integration.
