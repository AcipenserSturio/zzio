using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.db;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrBookMenu : BaseScreen<components.ui.ScrBookMenu, messages.ui.OpenBookMenu>
{
    private readonly MappedDB db;

    private static readonly UID[] UIDStatNames = new UID[]
    {
        new(0xE946ECA1), // Damage
        new(0x238A3981), // Mana
        new(0x4211121), // Fire Rate
    };
    private static readonly UID[] UIDClassNames = new UID[]
    {
        new(0x448DD8A1), // Nature
        new(0x30D5D8A1), // Air
        new(0xC15AD8A1), // Water
        new(0x6EE2D8A1), // Light
        new(0x44AAD8A1), // Energy
        new(0xEC31D8A1), // Psi
        new(0xAD78D8A1), // Stone
        new(0x6483DCA1), // Ice
        new(0x8EC9DCA1), // Fire
        new(0x8313DCA1), // Dark
        new(0xC659DCA1), // Chaos
        new(0x3CE1DCA1)  // Metal
    };
    private static readonly UID UIDEvol = new (0x69226721); // Evolution at level

    public ScrBookMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenBookMenu message)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        if (!inventory.Contains(StdItemId.FairyBook))
            return;

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrBookMenu>();
        ref var book = ref entity.Get<components.ui.ScrBookMenu>();
        book.Inventory = inventory;

        preload.CreateFullBackOverlay(entity);

        book.Spells = db.Spells.OrderBy(spellRow => spellRow.PriceA).ThenBy(spellRow => spellRow.CardId.EntityId).ToArray();
        book.SpellButtons = new Dictionary<components.ui.ElementId, SpellRow>();
        book.Sidebar = default;
        book.Crosshair = default;

        preload.CreateImage(entity)
            .With(-new Vector2(320, 240))
            .WithBitmap("col000")
            .WithRenderOrder(1)
            .Build();

        preload.CreateTooltipTarget(entity)
            .With(new Vector2(-320 + 11, -240 + 11))
            .WithText("{205} - ")
            .Build();

        CreateTopButtons(preload, entity, inventory, IDOpenFairybook);
        CreateSpellButtons(preload, entity, inventory, ref book);
    }

    private void CreateSpellButtons(UIPreloader preload, in DefaultEcs.Entity entity, Inventory inventory, ref components.ui.ScrBookMenu book)
    {
        var spells = book.Spells;
        for (int i = 0; i < spells.Length; i++)
        {
            if (inventory.Contains(spells[i].CardId))
            {
                var element = new components.ui.ElementId(1 + i);
                var button = preload.CreateButton(entity)
                    .With(element)
                    .With(Mid + SpellButtonPos(i))
                    .With(new components.ui.ButtonTiles(spells[i].CardId.EntityId))
                    .With(preload.Spl000)
                    .Build();
                book.SpellButtons.Add(element, spells[i]);

                // In the original engine, only the first fairy is checked for isInUse
                // This is an intentional bug fix
                if (inventory.Spells.Any(c => spells[i].CardId == c.cardId && c.isInUse)) {
                    preload.CreateImage(entity)
                        .With(Mid + SpellButtonPos(i))
                        .With(preload.Inf000, 16)
                        .WithRenderOrder(-1)
                        .Build();
                }
            }
        }
    }

    private DefaultEcs.Entity CreateSidebar(UIPreloader preload, in DefaultEcs.Entity parent, SpellRow spellRow, ref components.ui.ScrBookMenu book)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var spellI = Array.IndexOf(book.Spells, spellRow) + 1;

        var element = new components.ui.ElementId(0);
        preload.CreateButton(entity)
            .With(element)
            .With(Mid + new Vector2(160, 218))
            .With(new components.ui.ButtonTiles(spellRow.CardId.EntityId))
            .With(preload.Spl000)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 57))
            .WithText($"#{spellI} {spellRow.Name}")
            .With(preload.Fnt000)
            .Build();

        preload.CreateImage(entity)
            .With(Mid + new Vector2(22, 81))
            .With(preload.Cls000, (int)spellRow.PriceA)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(36, 80))
            .WithText(db.GetText(UIDClassNames[(int)spellRow.PriceA-1]).Text)
            .With(preload.Fnt002)
            .Build();

        CreateStat(preload, entity, 0, spellRow.Damage + 1);
        CreateStat(preload, entity, 1, spellRow.MaxMana switch {
            5 => 1,
            15 => 2,
            30 => 3,
            40 => 4,
            55 => 5,
            1000 => 5,
            _ => 5
        });
        CreateStat(preload, entity, 2, spellRow.Loadup + 1);

        const float MaxTextWidth = 190f;
        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 346))
            .WithText(spellRow.Info)
            .With(preload.Fnt002)
            .WithLineWrap(MaxTextWidth)
            .Build();

        return entity;
    }

    private void CreateStat(UIPreloader preload, in DefaultEcs.Entity entity, int index, int value)
    {
        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 271 + index*17))
            .WithText(db.GetText(UIDStatNames[index]).Text)
            .With(preload.Fnt002)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(111, 266 + index*17))
            .WithText(string.Concat(Enumerable.Repeat("{1017}", value)) + string.Concat(Enumerable.Repeat("{1018}", 5-value)))
            .With(preload.Fnt001)
            .Build();
    }

    private Vector2 SpellButtonPos(int spellI) {
        return new Vector2(226 + 45 * (spellI % 10), 66 + 45 * (spellI / 10));
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrBookMenu bookMenu)
    {
        base.Update(timeElapsed, entity, ref bookMenu);
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId id)
    {
        var bookMenuEntity = Set.GetEntities()[0];
        ref var book = ref bookMenuEntity.Get<components.ui.ScrBookMenu>();

        if (book.SpellButtons.TryGetValue(id, out var spellRow))
        {
            book.Sidebar.Dispose();
            book.Sidebar = CreateSidebar(preload, entity, spellRow, ref book);
            book.Crosshair.Dispose();
            book.Crosshair = preload.CreateImage(entity)
                .With(Mid + new Vector2(-2, -2) + SpellButtonPos(book.Spells.IndexOf(spellRow)))
                .With(preload.Dnd000, 0)
                .WithRenderOrder(-2)
                .Build();
        }

        if (id == IDOpenDeck)
        {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        else if (id == IDOpenRunes)
        {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        else if (id == IDOpenMap)
        {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        else if (id == IDClose)
            bookMenuEntity.Dispose();
    }

    protected override void HandleKeyDown(Key key)
    {
        var bookMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        if (key == Key.F2) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        if (key == Key.F4) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        if (key == Key.F5) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        if (key == Key.Enter || key == Key.Escape || key == Key.F3)
            Set.DisposeAll();
    }
}
