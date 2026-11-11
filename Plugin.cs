using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Adalomoon.Windows;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Adalomoon;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private static readonly string[] DialogueAddons = ["Talk", "BattleTalk", "TalkSubtitle", "MiniTalk"];
    private readonly WindowSystem windowSystem = new("Adalomoon Dialogue");
    private readonly DialogueWindow dialogueWindow;
    private readonly CameraFramer cameraFramer = new();
    private nint hiddenTalkRoot;
    private bool talkRootWasDrawDisabled;

    public Plugin()
    {
        dialogueWindow = new DialogueWindow(PluginInterface.UiBuilder.DefaultFontHandle);
        dialogueWindow.CloseRequested = CloseCurrentDialogue;
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
        RestoreNativeTalk();
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
            {
                cameraFramer.Begin();
                HideNativeTalk(args);
            }
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
        {
            RestoreNativeTalk(args);
            cameraFramer.Restore();
        }
    }

    private unsafe void HideNativeTalk(AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null || addon->RootNode is null)
            return;

        var rootAddress = (nint)addon->RootNode;
        if (hiddenTalkRoot != 0 && hiddenTalkRoot != rootAddress)
            RestoreNativeTalk();

        if (hiddenTalkRoot == 0)
        {
            hiddenTalkRoot = rootAddress;
            talkRootWasDrawDisabled = addon->RootNode->IsDrawDisabled;
        }

        // Suppress only the native Talk visuals; the game addon remains alive so
        // normal confirmation and Escape handling stay with FFXIV.
        addon->RootNode->IsDrawDisabled = true;
    }

    private unsafe void RestoreNativeTalk(AddonArgs? args = null)
    {
        if (hiddenTalkRoot == 0)
            return;

        AtkUnitBase* addon = args is null
            ? GameGui.GetAddonByName<AtkUnitBase>("Talk")
            : (AtkUnitBase*)args.Addon.Address;
        if (addon is not null && addon->RootNode is not null && (nint)addon->RootNode == hiddenTalkRoot)
            addon->RootNode->IsDrawDisabled = talkRootWasDrawDisabled;

        hiddenTalkRoot = 0;
        talkRootWasDrawDisabled = false;
    }

    private unsafe void CloseCurrentDialogue()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>("Talk");
        if (addon is not null && addon->IsVisible)
            addon->Close(true);
    }
}

