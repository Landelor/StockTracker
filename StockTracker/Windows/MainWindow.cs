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
        Settings,
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
        // Native ImGui border/corner resize must stay fully disabled, not just "not opted out of" -
        // leaving it enabled let it fight the manual resize handles below: whenever native resize
        // won a frame, the very next frame's unconditional `Size = manualSize` (needed to make our
        // own drags stick) snapped straight back to the old size. Toggling the resize lock re-primes
        // ImGui's native hit-test and made this surface reliably, but the underlying conflict was
        // always there - only our own InvisibleButton-based handles may ever change this window's size.
        Flags |= ImGuiWindowFlags.NoResize;
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
            DrawResizeHandles();
    }

    /// <summary>
    /// Three manual resize handles - right edge (width only), bottom edge (height only), and the
    /// corner (both). Native ImGui border/corner resize-drag is unreliable on this custom-chrome
    /// window (a body child covers the hit area regardless of the margin left for it - see the
    /// dalamud-adt-ui-kit skill's history), so every direction is implemented as a plain
    /// InvisibleButton + MouseDelta drag instead of relying on ImGui's own hit-test. Without the
    /// edge handles, only diagonal (corner) resizing worked - dragging the right edge alone just
    /// "snapped back" each frame because nothing was listening there at all.
    /// </summary>
    private void DrawResizeHandles()
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        const float hitSize = 22f;
        var hit = hitSize * scale;
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var corner = windowPos + windowSize;
        var headerClearance = (HeaderHeight + 8f) * scale;

        DrawEdgeHandle("##st_resize_right", new Vector2(corner.X - hit, windowPos.Y + headerClearance),
            new Vector2(hit, Math.Max(1f, windowSize.Y - headerClearance - hit)), horizontal: true);
        DrawEdgeHandle("##st_resize_bottom", new Vector2(windowPos.X, corner.Y - hit),
            new Vector2(Math.Max(1f, windowSize.X - hit), hit), horizontal: false);
        DrawCornerHandle(corner, scale, hit);
    }

    private void DrawEdgeHandle(string id, Vector2 pos, Vector2 size, bool horizontal)
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        if (hovered || active)
            ImGui.SetMouseCursor(horizontal ? ImGuiMouseCursor.ResizeEw : ImGuiMouseCursor.ResizeNs);

        if (active)
        {
            var current = manualSize ?? ImGui.GetWindowSize() / scale;
            var delta = ImGui.GetIO().MouseDelta / scale;
            var next = horizontal
                ? new Vector2(current.X + delta.X, current.Y)
                : new Vector2(current.X, current.Y + delta.Y);
            manualSize = Vector2.Clamp(next, expandedConstraints.MinimumSize, expandedConstraints.MaximumSize);
        }

        if (!hovered && !active)
            return;

        var color = ImGui.GetColorU32(Adt.AdtStyling.WithAlpha(Adt.AdtStyling.AccentTeal, active ? 1f : 0.65f));
        var dl = ImGui.GetWindowDrawList();
        var mid = pos + size * 0.5f;
        var thickness = 2f * scale;
        if (horizontal)
        {
            var half = Math.Min(16f * scale, size.Y * 0.5f - 2f * scale);
            dl.AddLine(mid - new Vector2(0f, half), mid + new Vector2(0f, half), color, thickness);
        }
        else
        {
            var half = Math.Min(16f * scale, size.X * 0.5f - 2f * scale);
            dl.AddLine(mid - new Vector2(half, 0f), mid + new Vector2(half, 0f), color, thickness);
        }
    }

    private void DrawCornerHandle(Vector2 corner, float scale, float hit)
    {
        const float gripSize = 14f;
        const float inset = 5f;
        var grip = gripSize * scale;
        var padding = inset * scale;

        ImGui.SetCursorScreenPos(corner - new Vector2(hit, hit));
        ImGui.InvisibleButton("##st_resize_corner", new Vector2(hit, hit));
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        if (hovered || active)
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
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
            case MainTab.Settings:
                DrawSettingsTab();
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
