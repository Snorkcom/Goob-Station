// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._CorvaxGoob.CardPdaPainter;

public abstract class SharedCardPdaPainterSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardPdaPainterComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CardPdaPainterComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, CardPdaPainterComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, CardPdaPainterComponent.TargetSlotId, component.TargetSlot);
    }

    private void OnComponentRemove(EntityUid uid, CardPdaPainterComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.TargetSlot);
    }
}
