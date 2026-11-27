using System;
using System.IO;
using System.Linq;
using StockTracker.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace StockTracker;

public sealed partial class Plugin : IDalamudPlugin
{
    internal const string CommandName = "/stocktracker";
    internal const string ShortCommandName = "/st";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly string configPath;
    private readonly WindowSystem windowSystem = new("StockTracker");
    private readonly MainWindow mainWindow;

    internal Configuration Config { get; }
    internal AllaganToolsIpc AllaganTools { get; }

    public Plugin()
    {
        configPath = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "config.json");
        Config = Configuration.Load(configPath);
        AllaganTools = new AllaganToolsIpc();

        Windows.Adt.AdtFonts.Initialize(PluginInterface.UiBuilder);
        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);

        var command = new CommandInfo(OnCommand) { HelpMessage = "Open StockTracker." };
        CommandManager.AddHandler(CommandName, command);
        CommandManager.AddHandler(ShortCommandName, command);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenMainUi;
        Framework.Update += OnFrameworkUpdate;
    }

    internal static (bool Installed, bool Loaded) GetPluginState(string internalName)
    {
        var installedPlugin = PluginInterface.InstalledPlugins
            .FirstOrDefault(plugin => plugin.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase));
        return (installedPlugin is not null, installedPlugin?.IsLoaded == true);
    }

    internal void SaveConfig() => Config.Save(configPath);

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenMainUi;
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShortCommandName);
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        Windows.Adt.AdtFonts.Dispose();
    }

    private void OnCommand(string command, string args) => mainWindow.Open();

    private void OpenMainUi() => mainWindow.Open();
}
