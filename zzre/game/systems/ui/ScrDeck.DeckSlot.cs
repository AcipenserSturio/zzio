using System;
using System.Numerics;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    private static readonly UID UIDSelectFairy = new(0x41912581);
    private static readonly UID UIDPlaceFairy = new(0x41912581);
    private static readonly UID UIDReplaceFairy = new(0x41912581);
    private static readonly UID UIDUseItemOnFairy = new(0x41912581);

    private DefaultEcs.Entity CreateDeckSlot(
        DefaultEcs.Entity parent,
        Vector2 pos,
        components.ui.ElementId id,
        int index
    )
    {
        var entity = CreateBaseSlot(parent, pos, id, index);
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.type = components.ui.Slot.Type.DeckSlot;

        CreateSlotSummary(entity, new(48, 4));

        slot.spellSlots = new DefaultEcs.Entity[4];
        for (int i = 0; i < InventoryFairy.SpellSlotCount; i++)
            slot.spellSlots[i] = CreateSpellSlot(entity, ref slot, i);

        return entity;
    }

    private void SetDeckSlot(DefaultEcs.Entity entity, InventoryCard card)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        SetSlot(ref slot, card);
        slot.button.Set(new components.ui.TooltipUID(UIDSelectFairy));

        for (int i = 0; i < InventoryFairy.SpellSlotCount; i++)
        {
            var spell = slot.card == null
                ? null
                : inventory.GetSpellAtSlot((InventoryFairy)slot.card, i);
            if (spell != default)
                SetSpellSlot(slot.spellSlots[i], spell);
            else
                UnsetSpellSlot(slot.spellSlots[i]);
        }
    }

    private void InfoMode(ref components.ui.Slot slot)
    {
        if (slot.card != default)
            slot.summary.Set(new components.ui.Label(slot.card switch
            {
                InventoryItem item => FormatSlotSummary(item),
                InventorySpell spell => FormatSlotSummary(spell),
                InventoryFairy fairy => FormatSlotSummary(fairy),
                _ => throw new NotSupportedException("Unknown inventory card type")
            }));
        foreach (var spellSlot in slot.spellSlots)
            InfoModeSpell(ref spellSlot.Get<components.ui.Slot>());
    }

    private static void SpellMode(ref components.ui.Slot slot)
    {
        slot.summary.Set(new components.ui.Label(""));
        foreach (var spellSlot in slot.spellSlots)
            SpellModeSpell(ref spellSlot.Get<components.ui.Slot>());
    }
}