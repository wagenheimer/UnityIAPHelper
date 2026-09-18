# IAP Setup Checklist

Duas camadas de verificação para o fluxo de compras de um projeto que usa `com.wagenheimer.iaphelper`:

1. **Automática** — `Tools > Wagenheimer > IAP Helper > Verify Setup...` no Editor (ou em batch mode, veja
   [Instruções para Agentes de IA](#instruções-para-agentes-de-ia-claude-code-etc)). Cobre tudo que dá para
   inferir de prefabs, cenas e código-fonte.
2. **Manual** — itens que só existem dentro das consoles das lojas (Google Play Console, App Store Connect).
   Nenhuma ferramenta local consegue verificar isso; siga a lista abaixo.

Use este checklist inteiro **antes de publicar** uma build com IAP, e sempre que investigar um relato do tipo
"comprei e nada aconteceu" / "ainda aparecem anúncios depois de comprar" / "travado num nível/gate que devia
ter sido liberado".

---

## 1. Verificação automática (roda no Editor)

Execute `Tools > Wagenheimer > IAP Helper > Verify Setup...`. A ferramenta varre o projeto e reporta:

- [ ] `com.unity.purchasing` instalado, versão ≥ 5.4.3
- [ ] Pelo menos um `IAPHelper` presente em prefab/cena (e não mais de um, a menos que intencional)
- [ ] `IAPHelper.products` não está vazio e nenhum `id` está em branco ou duplicado
- [ ] Todo `BaseIAPForm` (formulário de compra) tem `productId` preenchido
- [ ] **Todo `productId` usado em um form existe no catálogo de algum `IAPHelper`** — a causa mais comum do
      bug "comprou e nada muda": o form tenta comprar/checar um id que `IAPHelper.products` desconhece, então
      `HasPurchased()`/`GetProduct()` nunca encontram o produto
- [ ] `HasPurchasedFallback` está sendo atribuído em algum lugar do código (integra o save local do jogo)
- [ ] `OnEntitlementGranted` está sendo escutado em algum lugar do código (é o evento permanente que concede
      o conteúdo comprado — veja seção 3)
- [ ] O pacote instalado é `>= 1.1.0` (inclui a concessão/confirmação de compra persistente; versões
      anteriores dependiam de um listener temporário que expira em 60s ou ao fechar a tela de compra)

Falhas (❌) devem ser corrigidas antes de publicar. Avisos (⚠️) merecem uma segunda olhada, mas podem ser
intencionais dependendo do projeto.

---

## 2. Verificação manual nas lojas

Nada disso é automatizável a partir do projeto Unity — só existe dentro do painel da loja.

### Google Play Console

- [ ] O produto está cadastrado em **Monetizar > Produtos > Produtos no app** (ou **Assinaturas**, se for o
      caso) com o **mesmo ID**, caractere por caractere (case-sensitive), do `id` em `IAPHelper.products`
- [ ] O produto está com status **Ativo** (produtos "Inativos" não aparecem para compra nem retornam preço)
- [ ] O app foi enviado para pelo menos uma faixa de teste (interno/fechado/aberto/produção) com o **mesmo
      `applicationId`** e **assinado com a mesma chave** configurada no produto
- [ ] A conta Google usada para testar está cadastrada como **testador de licença** (Configurações > Testes de
      licença) — sem isso, compras de teste podem falhar silenciosamente ou cobrar de verdade
- [ ] Depois de uma compra de teste, o pedido aparece em **Monetizar > Pedidos** com status **Concluído** (não
      "Pendente" — um pedido preso em "Pendente" por muito tempo é exatamente o sintoma que este pacote agora
      se auto-cura, mas confirmar aqui fecha o ciclo)
- [ ] Se o projeto usa Amazon Appstore, repita os itens acima no painel da Amazon com o `amazonId` configurado

### App Store Connect (iOS/macOS)

- [ ] O produto está em **Recursos do App > Compras no app**, com o **mesmo Product ID** configurado em
      `appleId` (ou `id`, se `appleId` estiver vazio)
- [ ] O produto está com status **Pronto para envio** (não "Em desenvolvimento" / faltando metadados)
- [ ] Existe um **Contrato de Compartilhamento de Receita (Paid Apps Agreement)** ativo na conta — sem isso,
      NENHUMA compra funciona, mesmo em sandbox
- [ ] Testado com uma conta de **Sandbox Tester** dedicada (não a Apple ID pessoal do dispositivo)
- [ ] `autoRestorePurchases` está `false` nessa plataforma e existe um botão visível "Restaurar Compras" (
      exigência das App Store Review Guidelines — restauração automática silenciosa é motivo de rejeição)

### Ambos

- [ ] O `productId` usado no código é **idêntico** ao SKU/Product ID cadastrado na loja (copie e cole, não
      digite de novo — divergência de maiúsculas/hífen/espaço é o erro mais comum)
- [ ] Testado o ciclo completo **compra → fechar o app → reabrir** para confirmar que a restauração automática
      (Android) ou o botão de restaurar (iOS) realmente libera o conteúdo numa sessão nova

---

## 3. Como a concessão de conteúdo deve estar conectada (a partir da v1.1.0)

```csharp
// No boot do jogo, uma única vez:
IAPHelper.Instance.OnEntitlementGranted += productId =>
{
    if (productId == "unlockfullgame")
        UnlockFullGame(); // idempotente: seta o save e salva, pode ser chamado mais de uma vez
};

IAPHelper.HasPurchasedFallback = id => SaveData != null && SaveData.UnlockedGame && id == "unlockfullgame";
```

`OnEntitlementGranted` é a **única fonte de verdade** para "o jogador possui este produto" — dispara tanto
para uma compra concluída ao vivo quanto para uma restauração detectada no boot ou ao retomar o foco do app,
e não depende de nenhum formulário de UI continuar aberto/existir. Não implemente a liberação de conteúdo
apenas dentro do `GrantPurchasedContent()` de um `BaseIAPForm` específico — esse método deve tratar só do
lado de UI (fechar o diálogo, tocar uma animação); o `OnEntitlementGranted` global garante que o jogo é
liberado mesmo que o jogador nunca tenha visto aquela tela nesta sessão.

---

## Instruções para Agentes de IA (Claude Code, etc.)

Se você é um agente de codificação auditando este projeto (sem acesso à UI do Editor), reproduza a verificação
automática por grep/leitura de arquivos, e rode a verificação real em batch mode se tiver acesso ao binário do
Unity Editor.

### 4.1 Rodar a auditoria real via Unity batch mode (preferível)

```bash
"<caminho do Unity Editor>" -batchmode -quit -projectPath "<caminho do projeto>" -logFile - \
  -executeMethod Wagenheimer.IAPHelper.Editor.IAPHelperAudit.RunHeadlessAndLog
```

Isso executa exatamente a mesma lógica da janela `Verify Setup...`, imprime um relatório Markdown no log, e
sai com código de saída `1` se houver alguma falha crítica (útil para gates de CI).

### 4.2 Se não houver acesso ao Unity Editor: reproduza os checks por leitura estática

1. **Versão do pacote**: leia `Packages/manifest.json` e `Packages/packages-lock.json`, procure
   `com.wagenheimer.iaphelper`. Se a versão for `< 1.1.0`, marque como falha — essa build usa o fluxo antigo em
   que a concessão da compra depende de um listener temporário com timeout de 60s (veja `CHANGELOG.md` deste
   pacote para o texto exato do bug corrigido).

2. **`com.unity.purchasing` instalado e versão**: mesma leitura de manifest, procurando `com.unity.purchasing`;
   versão mínima `5.4.3`.

3. **Catálogo de produtos**: grep por `class ProductConfig` não é necessário — procure, dentro de
   `Assets/**/*.cs` e em arquivos `.prefab`/`.unity` (são YAML texto), pelas listas serializadas de
   `IAPHelper.products` (no YAML de prefab/cena aparece como blocos `products:` sob o componente `IAPHelper`,
   com entradas `id: <valor>`).

4. **`productId` de cada form de compra**: grep em arquivos `.prefab`/`.unity` por `productId: <valor>` (é um
   campo público serializado de `BaseIAPForm`). Compare o conjunto de valores encontrados aqui com o conjunto
   de `id` encontrado no passo 3 — **qualquer `productId` de um form que não apareça na lista de products do
   IAPHelper é uma falha crítica** (causa direta do sintoma "comprei e nada mudou").

   Exemplo de comando útil:
   ```bash
   grep -rn "productId:" Assets/ | sort -u
   ```

5. **`HasPurchasedFallback`**: `grep -rn "HasPurchasedFallback\s*=" Assets/` — deve haver pelo menos uma
   atribuição fora do próprio pacote.

6. **`OnEntitlementGranted`**: `grep -rn "OnEntitlementGranted" Assets/` — deve haver pelo menos uma assinatura
   (`+=`) fora do próprio pacote. Se só existir `OnPurchasesFetched +=` e não `OnEntitlementGranted +=`, sinalize
   como aviso (funciona, mas só cobre restauração no boot, não compra ao vivo além do que o próprio pacote já
   resolve internamente).

7. **Consistência entre IDs de loja**: isto **não é verificável estaticamente** — apenas alerte o usuário
   humano para confirmar manualmente a seção 2 deste documento (Google Play Console / App Store Connect), já
   que nenhum agente sem credenciais de loja consegue validar isso.

### 4.3 Ao investigar um bug relatado como "comprei e nada aconteceu" / "ainda tem anúncios depois de comprar"

Siga esta ordem de investigação (é a mesma que localizou o bug original corrigido na v1.1.0 deste pacote):

1. Encontre o(s) gate(s) no código do jogo que controlam anúncios/progressão (busque pelo campo de save que
   representa "jogo desbloqueado", ex: `UnlockedGame`) e confirme que ele só depende desse flag.
2. Encontre onde esse flag é setado como `true` (deve ser dentro de um `GrantPurchasedContent()` de algum
   `BaseIAPForm`, ou dentro de um listener de `OnEntitlementGranted`/`OnPurchasesFetched`).
3. Confirme (passo 4.2.4 acima) que o `productId` usado nesse caminho bate com o catálogo do `IAPHelper`.
4. Confirme a versão do pacote (passo 4.2.1). Se for anterior a 1.1.0, a causa mais provável é a compra ter
   confirmado depois do timeout/fechamento da UI de compra — a correção é atualizar o pacote, não alterar a
   lógica do jogo.
5. Se tudo acima estiver correto, o problema está fora do alcance de uma auditoria estática: peça para o
   usuário verificar a seção 2 (Google Play Console / App Store Connect) — em particular se o pedido aparece
   como "Concluído" ou ficou preso em "Pendente".
