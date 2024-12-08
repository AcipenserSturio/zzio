using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;
using KeyCode = Silk.NET.SDL.KeyCode;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrExtBookMenu : BaseScreen<components.ui.ScrExtBook, messages.ui.OpenExtBook>
{
    private readonly MappedDB db;

    private static readonly UID[] UIDStatNames =
    [
        new(0x3D26ACB1), // Hit Points
        new(0xAB46B8B1), // Dexterity
        new(0xB031B8B1), // Jump Ability
        new(0xB6CA5A11)  // Special
    ];
    private string StatsText() =>
        String.Join("\n", UIDStatNames.Select(uid => db.GetText(uid).Text));
    private string StatsLights(int[] values) =>
        String.Join("\n", values.Select(value => UIBuilder.GetLightsIndicator(value)));

    private static readonly UID UIDEvol = new(0x69226721); // Evolution at level
    private const int bookColumns = 15;
    private static Vector2 sidebarOffset = new Vector2(-159, -159);
    private static Vector2 crosshairOffset = new Vector2(-2, -2);

    public ScrExtBookMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenExtBook message)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrExtBook>();
        ref var book = ref entity.Get<components.ui.ScrExtBook>();
        book.Inventory = inventory;

        preload.CreateFullBackOverlay(entity);

        book.Fairies = [.. db.Fairies.OrderBy(fairyRow => fairyRow.CardId.EntityId)];
        book.FairyButtons = [];
        book.Sidebar = default;
        book.Crosshair = default;
        book.CurrentFairy = 0;

        preload.CreateImage(entity)
            .With(components.ui.FullAlignment.Center)
            .WithBitmap("ext000")
            .WithRenderOrder(1)
            .Build();

        preload.CreateTooltipTarget(entity)
            .With(new Vector2(-320 + 11, -240 + 11))
            .WithText("{205} - ")
            .Build();

        CreateFairyButtons(preload, entity, inventory, ref book);
    }

    private static void CreateFairyButtons(UIBuilder preload, in DefaultEcs.Entity entity, Inventory inventory, ref components.ui.ScrExtBook book)
    {
        var fairies = book.Fairies;
        for (int i = 0; i < 215; i++)
        {
            var element = new components.ui.ElementId(1 + i);
            var button = preload.CreateButton(entity)
                .With(element)
                .With(Mid + FairyButtonPos(i))
                .With(new components.ui.ButtonTiles(fairies[i].CardId.EntityId))
                .With(UIPreloadAsset.Wiz000)
                .Build();
            button.Set(button.Get<Rect>().GrownBy(new Vector2(5, 5))); // No gaps
            button.Set(new components.ui.Silent());
            book.FairyButtons.Add(element, i);
        }
    }

    private DefaultEcs.Entity CreateSidebar(UIBuilder preload, in DefaultEcs.Entity parent, FairyRow fairyRow, ref components.ui.ScrExtBook book)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var fairyI = Array.IndexOf(book.Fairies, fairyRow) + 1;

        var element = new components.ui.ElementId(0);
        preload.CreateButton(entity)
            .With(element)
            .With(Mid + sidebarOffset + new Vector2(160, 218))
            .With(new components.ui.ButtonTiles(fairyRow.CardId.EntityId))
            .With(UIPreloadAsset.Wiz000)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + sidebarOffset + new Vector2(21, 57))
            .WithText($"#{fairyI} {fairyRow.Name}")
            .With(UIPreloadAsset.Fnt000)
            .Build();

        preload.CreateImage(entity)
            .With(Mid + sidebarOffset + new Vector2(22, 81))
            .With(UIPreloadAsset.Cls000, (int)fairyRow.Class0)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + sidebarOffset + new Vector2(36, 80))
            .WithText(preload.GetClassText(fairyRow.Class0))
            .With(UIPreloadAsset.Fnt002)
            .Build();

        if (fairyRow.EvolVar != -1)
            preload.CreateLabel(entity)
                .With(Mid + sidebarOffset + new Vector2(22, 246))
                .WithText($"{db.GetText(UIDEvol).Text} {fairyRow.EvolVar}")
                .With(UIPreloadAsset.Fnt002)
                .Build();

        preload.CreateLabel(entity)
            .With(Mid + sidebarOffset + new Vector2(21, 271))
            .WithText(StatsText())
            .WithLineHeight(17)
            .With(UIPreloadAsset.Fnt002)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + sidebarOffset + new Vector2(111, 266))
            .WithText(StatsLights([
                Math.Min(500, fairyRow.MHP) / 100,
                fairyRow.MovSpeed + 1,
                fairyRow.JumpPower + 1,
                fairyRow.CriticalHit + 1
            ]))
            .WithLineHeight(17)
            .With(UIPreloadAsset.Fnt001)
            .Build();

        const float MaxTextWidth = 190f;
        preload.CreateLabel(entity)
            .With(Mid + sidebarOffset + new Vector2(21, 346))
            .WithText(fairyRow.Info)
            .With(UIPreloadAsset.Fnt002)
            .WithLineWrap(MaxTextWidth)
            .Build();

        return entity;
    }

    private static Vector2 FairyButtonPos(int fairyI) =>
        new Vector2(
            75 + 45 * (fairyI % bookColumns),
            -95 + 45 * (fairyI / bookColumns)
        );

    private void TrySetCurrentFairy(int fairyI)
    {
        var entity = Set.GetEntities()[0];
        ref var book = ref entity.Get<components.ui.ScrExtBook>();
        var fairyRow = book.Fairies.ElementAtOrDefault(fairyI);
        if (fairyRow == default)
            return;

        book.CurrentFairy = fairyI;
        book.Sidebar.Dispose();
        book.Sidebar = CreateSidebar(preload, entity, fairyRow!, ref book);
        book.Crosshair.Dispose();
        book.Crosshair = preload.CreateImage(entity)
            .With(Mid + crosshairOffset + FairyButtonPos(fairyI))
            .With(UIPreloadAsset.Dnd000, 0)
            .WithRenderOrder(-2)
            .Build();
    }

    private void HandleElementDown(DefaultEcs.Entity clickedEntity, components.ui.ElementId id)
    {
        var entity = Set.GetEntities()[0];
        ref var book = ref entity.Get<components.ui.ScrExtBook>();

        if (book.FairyButtons.TryGetValue(id, out var fairyI))
            TrySetCurrentFairy(fairyI);
    }

    protected override void HandleKeyDown(KeyCode key)
    {
        var entity = Set.GetEntities()[0];
        ref var book = ref entity.Get<components.ui.ScrExtBook>();

        base.HandleKeyDown(key);
        if (key == KeyCode.KLeft)
            TrySetCurrentFairy(book.CurrentFairy - 1);
        else if (key == KeyCode.KRight)
            TrySetCurrentFairy(book.CurrentFairy + 1);
        else if (key == KeyCode.KUp)
            TrySetCurrentFairy(book.CurrentFairy - bookColumns);
        else if (key == KeyCode.KDown)
            TrySetCurrentFairy(book.CurrentFairy + bookColumns);
        else if (key == KeyCode.KReturn || key == KeyCode.KEscape || key == KeyCode.KF6)
            Set.DisposeAll();
    }
}
