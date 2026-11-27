using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private void DrawNavRail()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = 40f * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size) * 0.5f);

        DrawNavItem(MainTab.Dashboard, FontAwesomeIcon.TachometerAlt, "Dashboard\nDependency status and a quick summary.", size);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size) * 0.5f);
        DrawNavItem(MainTab.TrackedItems, FontAwesomeIcon.ListUl, "Tracked Items\nItems you craft, their target stock, and retainer.", size);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size) * 0.5f);
        DrawNavItem(MainTab.NeedsRecraft, FontAwesomeIcon.ExclamationTriangle, "Needs Recraft\nTracked items currently below target stock.", size);
    }

    private void DrawNavItem(MainTab tab, FontAwesomeIcon icon, string tooltip, float size)
    {
        if (AdtNavRail.Item(icon, $"##st_nav_{tab}", size, activeTab == tab, tooltip))
            activeTab = tab;
        AdtStyling.VSpace(8f);
    }
}
