using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace StockTracker.Windows;

public sealed partial class MainWindow : Window, IDisposable
{
    private enum MainTab
    {
        Dashboard,
        TrackedItems,
        NeedsRecraft,
    }

    private readonly record struct ItemChoice(uint Id, string Name);

    private readonly Plugin plugin;
    private readonly List<ItemChoice> items = [];
    private readonly WindowSizeConstraints expandedConstraints;
    private MainTab activeTab = MainTab.Dashboard;
    private Guid selectedListId;
    private bool headerCollapsed;
    private bool resizeLocked;
    private IDisposable? chrome;
    private Vector2? manualSize;

    public MainWindow(Plugin plugin) : base("StockTracker###StockTrackerMain")
    {
        this.plugin = plugin;
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        expandedConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        SizeConstraints = expandedConstraints;

        foreach (var item in Plugin.DataManager.GetExcelSheet<Item>())
        {
            var name = item.Name.ToString();
            if (item.ItemSearchCategory.RowId != 0 && !string.IsNullOrWhiteSpace(name))
                items.Add(new ItemChoice(item.RowId, name));
        }

        items.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
    }

    public void Open()
    {
        IsOpen = true;
        plugin.RefreshStatuses();
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        base.PreDraw();
        Flags &= ~ImGuiWindowFlags.NoResize;
        if (headerCollapsed)
        {
            var collapsedHeight = (HeaderHeight + 8f) * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(320, collapsedHeight),
                MaximumSize = new Vector2(float.MaxValue, collapsedHeight),
            };
            Size = null;
        }
        else
        {
            SizeConstraints = expandedConstraints;
            if (resizeLocked && manualSize is { } locked)
            {
                SizeConstraints = new WindowSizeConstraints { MinimumSize = locked, MaximumSize = locked };
                Size = locked;
                SizeCondition = ImGuiCond.Always;
            }
            else if (manualSize is { } wanted)
            {
                Size = wanted;
                SizeCondition = ImGuiCond.Always;
            }
        }

        // Chrome must be pushed in PreDraw/popped in PostDraw, never Draw() - ImGui.Begin() (which
        // paints the window's background/border) runs between the two, so pushing in Draw() is one
        // call too late. See the dalamud-adt-ui-kit skill / AutoMarket's MainWindow for the history.
        chrome = Adt.AdtStyling.PushChrome(Vector2.Zero);
    }

    public override void PostDraw()
    {
        chrome?.Dispose();
        chrome = null;
        base.PostDraw();
    }

    public override void Draw()
    {
        DrawContent();
        if (!headerCollapsed && !resizeLocked)
            DrawResizeGrip();
    }

    private void DrawResizeGrip()
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        const float gripSize = 14f;
        const float inset = 5f;
        const float hitSize = 22f;
        var grip = gripSize * scale;
        var padding = inset * scale;
        var hit = hitSize * scale;
        var corner = ImGui.GetWindowPos() + ImGui.GetWindowSize();

        ImGui.SetCursorScreenPos(corner - new Vector2(hit, hit));
        ImGui.InvisibleButton("##st_resize_grip", new Vector2(hit, hit));
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        if (active)
        {
            var current = manualSize ?? ImGui.GetWindowSize() / scale;
            var next = current + ImGui.GetIO().MouseDelta / scale;
            manualSize = Vector2.Clamp(next, expandedConstraints.MinimumSize, expandedConstraints.MaximumSize);
        }

        var color = ImGui.GetColorU32(Adt.AdtStyling.WithAlpha(Adt.AdtStyling.AccentTeal, active ? 1f : hovered ? 0.85f : 0.45f));
        var dl = ImGui.GetWindowDrawList();
        var anchor = corner - new Vector2(padding, padding);
        for (var line = 1; line <= 3; line++)
        {
            var offset = line * grip / 3f;
            dl.AddLine(anchor - new Vector2(offset, 0f), anchor - new Vector2(0f, offset), color, 1.6f * scale);
        }
    }

    private void DrawContent()
    {
        DrawHeaderBar();
        if (headerCollapsed)
            return;

        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        var bodyInset = 28f * scale;
        var bodySize = ImGui.GetContentRegionAvail() - new Vector2(bodyInset, bodyInset);

        const float railWidth = 64f;
        const float railGap = 8f;
        var rail = railWidth * scale;
        var gap = railGap * scale;

        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8f, 14f) * scale))
        using (var nav = ImRaii.Child("##st_nav", new Vector2(rail, bodySize.Y), false))
        {
            if (nav)
                DrawNavRail();
        }

        ImGui.SameLine(0, gap);

        var contentSize = new Vector2(Math.Max(1f, bodySize.X - rail - gap), bodySize.Y);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f, 12f) * scale))
        using (var body = ImRaii.Child("##st_body", contentSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (body)
                DrawBodyContent();
        }
    }

    private void DrawBodyContent()
    {
        switch (activeTab)
        {
            case MainTab.Dashboard:
                DrawDashboardTab();
                break;
            case MainTab.TrackedItems:
                DrawTrackedItemsTab();
                break;
            case MainTab.NeedsRecraft:
                DrawNeedsRecraftTab();
                break;
        }
    }

    private static void DrawPluginStatus(string name, (bool Installed, bool Loaded) state, bool required)
    {
        var (color, text) = state switch
        {
            { Loaded: true } => (Adt.AdtStyling.AccentMint, "loaded"),
            { Installed: true } => (Adt.AdtStyling.AccentAmber, "installed, disabled"),
            _ when required => (Adt.AdtStyling.AccentRose, "missing, required"),
            _ => (Adt.AdtStyling.TextMuted, "not installed, optional"),
        };
        Adt.AdtChip.Draw($"{name}: {text}", color);
    }
}
