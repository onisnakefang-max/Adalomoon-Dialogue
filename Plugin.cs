using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Adalomoon.Windows;

namespace Adalomoon;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private static readonly string[] DialogueAddons = ["Talk", "BattleTalk", "TalkSubtitle", "MiniTalk"];
    private readonly WindowSystem windowSystem = new("Adalomoon Dialogue");
    private readonly DialogueWindow dialogueWindow;
    private readonly CameraFramer cameraFramer = new();

    public Plugin()
    {
        dialogueWindow = new DialogueWindow();
        windowSystem.AddWindow(dialogueWindow);
        Framework.Update += cameraFramer.Update;

        foreach (var addon in DialogueAddons)
        {
            AddonLifecycle.RegisterListener(AddonEvent.PreDraw, addon, OnDialogueUpdate);
            AddonLifecycle.RegisterListener(AddonEvent.PreHide, addon, OnDialogueClosed);
            AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, addon, OnDialogueClosed);
        }

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += Toggle;
        Log.Information("Adalomoon Dialogue загружен.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Toggle;
        Framework.Update -= cameraFramer.Update;
        cameraFramer.Restore();
        foreach (var addon in DialogueAddons)
        {
            AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, addon, OnDialogueUpdate);
            AddonLifecycle.UnregisterListener(AddonEvent.PreHide, addon, OnDialogueClosed);
            AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, addon, OnDialogueClosed);
        }

        windowSystem.RemoveAllWindows();
    }

    private void Toggle() => dialogueWindow.IsOpen = !dialogueWindow.IsOpen;

    private unsafe void OnDialogueUpdate(AddonEvent type, AddonArgs args)
    {
        try
        {
            if (args.AddonName == "Talk")
                cameraFramer.Begin();
            dialogueWindow.UpdateFromAddon(args);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось прочитать текущее диалоговое окно.");
        }
    }

    private void OnDialogueClosed(AddonEvent type, AddonArgs args)
    {
        if (dialogueWindow.IsShowingAddon(args.AddonName))
            dialogueWindow.Clear();
        if (args.AddonName == "Talk")
            cameraFramer.Restore();
    }
}

