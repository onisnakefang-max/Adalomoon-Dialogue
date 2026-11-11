using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Adalomoon.Windows;

internal sealed class DialogueWindow : Window
{
    private string addonName = string.Empty;
    private List<string> lines = [];
    private string fingerprint = string.Empty;

    public DialogueWindow() : base("##AdalomoonDialogue")
    {
        IsOpen = false;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoNav;
    }

    public unsafe void UpdateFromAddon(AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null || addon->UldManager.NodeList is null)
            return;

        var found = new List<(float Y, string Text)>();
        var count = Math.Min((int)addon->UldManager.NodeListCount, 512);
        for (var i = 0; i < count; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node is null || node->Type != NodeType.Text || node->IsDrawDisabled)
                continue;

            var textNode = (AtkTextNode*)node;
            var text = textNode->NodeText.ExtractText();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            text = Clean(text);
            if (text.Length > 0)
                found.Add((node->ScreenY, text));
        }

        var next = found.OrderBy(x => x.Y).Select(x => x.Text).Distinct().Take(16).ToList();
        if (next.Count == 0)
            return;

        var nextFingerprint = string.Join("\n", next);
        if (fingerprint == nextFingerprint && IsOpen)
            return;

        addonName = args.AddonName;
        lines = next;
        fingerprint = nextFingerprint;
        IsOpen = true;
    }

    public void Clear()
    {
        IsOpen = false;
        lines.Clear();
        fingerprint = string.Empty;
        addonName = string.Empty;
    }

    public bool IsShowingAddon(string name) => addonName == name;

    public override void PreDraw()
    {
        var display = ImGui.GetIO().DisplaySize;
        var width = Math.Clamp(display.X * 0.29f, 380f, 520f);
        var height = Math.Clamp(display.Y * 0.68f, 420f, 760f);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(display.X - width - 28f, (display.Y - height) * 0.5f), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, height), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);
    }

    public override void Draw()
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        draw.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new System.Numerics.Vector4(0.035f, 0.055f, 0.11f, 0.97f)), 12f);
        draw.AddRect(pos, pos + size, ImGui.GetColorU32(new System.Numerics.Vector4(0.78f, 0.61f, 0.29f, 0.98f)), 12f, ImDrawFlags.None, 2f);
        var paperMin = pos + new System.Numerics.Vector2(16f, 54f);
        var paperMax = pos + size - new System.Numerics.Vector2(16f, 18f);
        draw.AddRectFilled(paperMin, paperMax, ImGui.GetColorU32(new System.Numerics.Vector4(0.82f, 0.75f, 0.59f, 0.98f)), 7f);
        draw.AddRect(paperMin, paperMax, ImGui.GetColorU32(new System.Numerics.Vector4(0.93f, 0.82f, 0.57f, 1f)), 7f, ImDrawFlags.None, 1.5f);

        ImGui.SetCursorPos(new System.Numerics.Vector2(26, 18));
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.92f, 0.76f, 0.43f, 1));
        ImGui.TextUnformatted("ДИАЛОГ  ·  " + addonName);
        ImGui.PopStyleColor();
        ImGui.Separator();
        ImGui.SetCursorPos(new System.Numerics.Vector2(34, 72));

        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.17f, 0.14f, 0.11f, 1f));
        foreach (var line in lines)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 42);
            ImGui.TextUnformatted(line);
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
        }
        ImGui.PopStyleColor();

        ImGui.SetCursorPos(new System.Numerics.Vector2(size.X - 168, size.Y - 28));
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.72f, 0.7f, 0.66f, 0.8f));
        ImGui.TextUnformatted("ВЫБОР — В ИГРЕ");
        ImGui.PopStyleColor();
    }

    private static string Clean(string text)
    {
        var visible = new StringBuilder(text.Length);
        foreach (var c in text)
            if (!char.IsControl(c)) visible.Append(c);
        return visible.ToString().Trim();
    }
}

