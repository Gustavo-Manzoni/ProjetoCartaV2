# Inventário radial e interação por raycast

## Montagem do jogador

1. Adicione `InventoryController` ao jogador.
2. Adicione `PlayerRaycastInteractor` ao jogador. Informe a câmera, o `InventoryController` e, depois de montar a UI, o `RadialInventoryView`.
3. Ajuste distância e LayerMask. Todo objeto interagível precisa de Collider e deve estar em uma layer incluída nessa máscara. A interação usa Mouse 1 esquerdo.

## Suporte que segue a mão

Monte esta estrutura:

```
Player
├── Camera
│   └── HandTarget
└── StoredItems

EquipmentRig (fora da Camera)
└── ItemMount
```

- Coloque `EquipmentFollowRig` em `EquipmentRig`.
- Arraste `HandTarget` para `Follow Target` e `ItemMount` para `Item Mount`.
- No `InventoryController`, coloque `ItemMount` em `Equipment Mount` e `StoredItems` em `Stored Items Root`.
- Ajuste `Pickup Transition Duration` e `Pickup Transition Curve` para controlar o movimento do objeto coletado até a mão.
- Não coloque `EquipmentRig` como filho da câmera: ele deve seguir o `HandTarget` por Lerp para produzir o atraso suave.
- `Position Sharpness` e `Rotation Sharpness` maiores deixam o objeto mais preso à mão; menores aumentam o atraso.
- Os offsets finais de posição, rotação e escala continuam vindo do asset do item.

## Montagem da UI radial (uGUI)   

Use esta hierarquia dentro de um Canvas `Screen Space - Overlay` com `CanvasScaler` em `Scale With Screen Size`:

```
Canvas
└── RadialInventory (RectTransform centralizado, por exemplo 520 x 520)
    ├── Ring (mesmo tamanho; RadialInventoryGraphic)
    └── Icons (mesmo tamanho, transparente; somente RectTransform)
```

- Coloque `CanvasGroup` e `RadialInventoryView` em `RadialInventory`.
- No view, ligue o inventário, CanvasGroup, Ring e Icons.
- Ligue o `FirstPersonController` do jogador no campo correspondente. Enquanto a roda está aberta, o inventário pede travas ao `GameplayLocks` (movimento, interação, piscada e pausa; cursor livre) e as libera ao fechar. Outros sistemas bloqueiam a roda com `GameplayLock.Inventory` (ou `SetInputBlocked`).
- Em `Behaviours Disabled While Open`, coloque somente outros scripts que também precisem pausar. Não é necessário colocar o `FirstPersonController` nessa lista.
- O view cria e posiciona os ícones sozinho. O segmento 0 é sempre vazio e serve para desequipar.
- Cada item mostra seu `Display Name` no segmento. Posição, rotação e escala do ícone podem ser ajustadas individualmente no asset do item, em `Radial Inventory Presentation`.
- Segurar Tab abre e libera o cursor. Nenhum slot começa selecionado: é necessário mover o mouse intencionalmente. Soltar Tab sem mover não altera o item atual.
- Ao selecionar, o segmento, o ícone e o aro externo crescem e se afastam suavemente do centro.
- `Circle Resolution` controla a suavidade do círculo. O padrão `128` deixa a rosquinha redonda mesmo quando existe somente um segmento.

## Criar um item

1. No Project: `Create > Projeto Carta > Inventory > Item`.
2. Defina um `Id` único, nome, ícone, a apresentação do ícone na roda e os offsets locais usados quando o objeto real for equipado.
3. Para lógica específica no prefab, derive seu script de `EquippedItemBehaviour` e sobrescreva somente os métodos necessários. Para lógica reutilizável entre itens, crie um ScriptableObject derivado de `InventoryItemAction` e coloque-o na lista `Actions` do item.

O inventário não aceita dois itens com o mesmo `Id`. Não existe quantidade nem limite de slots.

## Criar interagíveis

- Ação genérica: adicione `SimpleRaycastInteractable` e configure seu `On Interact` no Inspector.
- Item coletável: adicione `InventoryPickup`, selecione o asset e informe `Item Object`. Se ficar vazio, o próprio GameObject do pickup será usado. `Pickup Thought` é a frase de Tomás depois de pegar.
- Algo que abre e fecha (baú, gaveta, tampa), com ou sem chave: `AnimatorOpenable` ou `TransformOpenable`. Pickups filhos só ficam disponíveis com ele aberto.
- Usar um item aqui: `RequiredEquippedItemInteractable`. Encaixar um item num ponto: `InventoryItemPlacement`.
- Interação própria: derive de `RaycastInteractable`, implemente `Interact(InteractionContext context)` e, se a ação puder falhar, sobrescreva `IsFocusable`/`CanInteract`/`GetBlockedFeedback` para Tomás explicar o motivo. Veja "Arquitetura e convenções" no `CLAUDE.md` da raiz.
- Os eventos de foco da classe base podem ligar outline, texto ou animações sem criar outro script.

Para um aviso na tela, crie um painel com `CanvasGroup` e texto TMP, adicione `InteractionPromptView` e ligue as três referências.

Ao coletar, o objeto de cena não é destruído nem clonado. Seus colliders e física são desligados, e o próprio objeto interpola da posição do mundo até os offsets da mão antes de permanecer equipado. Quando outro item é equipado, o anterior fica desativado dentro de `StoredItems`; portanto, apenas o item equipado permanece visível.

## API útil

- `inventory.TryAdd(item)` adiciona se ainda não existe.
- `inventory.TryCollectAndEquip(item, sceneObject)` coleta o objeto real e inicia sua transição até a mão.
- `inventory.Remove(item)` remove e desequipa caso necessário.
- `inventory.Consume(item)` remove, desequipa e destrói definitivamente o objeto do item.
- `inventory.Equip(item)` equipa; `inventory.Equip(null)` desequipa.
- `inventory.UseEquippedItem()` executa o uso do item atual.
- Eventos: `InventoryChanged`, `EquippedItemChanged` e `RadialInventoryView.VisibilityChanged`.
