using System;
using System.Collections.Generic;
using zzio.db;

namespace zzre.game.components.ui;

public struct ScrBookMenu
{
    public Inventory Inventory;
    public SpellRow[] Spells;
    public Dictionary<components.ui.ElementId, SpellRow> SpellButtons;
    public DefaultEcs.Entity Sidebar;
    public DefaultEcs.Entity Crosshair;
}
