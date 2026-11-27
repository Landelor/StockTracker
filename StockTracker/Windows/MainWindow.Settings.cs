using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private void DrawSettingsTab()
    {
        ImGui.Text("Settings");
        AdtStyling.VSpace(8f);

        var contextMenuEnabled = plugin.Config.ContextMenuEnabled;
        if (ImGui.Checkbox("Add StockTracker to the Crafting Log's right-click menu", ref contextMenuEnabled))
        {
            plugin.Config.ContextMenuEnabled = contextMenuEnabled;
            plugin.SaveConfig();
        }
        ImGui.TextWrapped(
            "When enabled, right-clicking the selected recipe in the Crafting Log (RecipeNote) " +
            "adds a \"StockTracker\" entry letting you add that item to one of your lists (or a " +
            "new list) without leaving the crafting window.");
    }
}
