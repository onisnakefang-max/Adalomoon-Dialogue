using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Adalomoon.Windows;

internal sealed class DialogueWindow : Window
{
    private string addonName = string.Empty;
    private string speaker = string.Empty;
    private string body = string.Empty;
    private string fingerprint = string.Empty;
    private readonly IFontHandle font;

    public Action? CloseRequested { get; set; }

    public DialogueWindow(IFontHandle font) : base("##AdalomoonDialogue")
    {
        this.font = font;
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

            var text = Clean(((AtkTextNode*)node)->NodeText.ExtractText());
            if (!string.IsNullOrWhiteSpace(text))
                found.Add((node->ScreenY, text));
        }

        var next = found.OrderBy(x => x.Y).Select(x => x.Text).Distinct().Take(16).ToList();
        if (next.Count == 0)
            return;

        var nextFingerprint = string.Join("\n", next);
        if (fingerprint == nextFingerprint && IsOpen)
            return;

        addonName = args.AddonName;
        speaker = next[0];
        body = string.Join("\n\n", next.Skip(1));
        fingerprint = nextFingerprint;
        IsOpen = true;
    }

    public void Clear()
    {
        IsOpen = false;
        addonName = string.Empty;
        speaker = string.Empty;
        body = string.Empty;
        fingerprint = string.Empty;
    }

    public bool IsShowingAddon(string name) => addonName == name;

    public override void PreDraw()
    {
        var display = ImGui.GetIO().DisplaySize;
        var maxWidth = Math.Max(320f, Math.Min(470f, display.X - 32f));
        var width = Math.Clamp(display.X * 0.31f, Math.Min(360f, maxWidth), maxWidth);
        var approximateLines = Math.Max(2, (body.Length / Math.Max(28, (int)(width / 9f))) + 1);
        var maxHeight = Math.Max(280f, Math.Min(490f, display.Y - 48f));
        var height = Math.Clamp(206f + approximateLines * 23f, Math.Min(300f, maxHeight), maxHeight);
        var maxY = Math.Max(24f, display.Y - height - 24f);
        var pos = new System.Numerics.Vector2(Math.Max(16f, display.X - width - 28f), Math.Clamp(display.Y * 0.43f, 24f, maxY));
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, height), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);
    }

    public override void Draw()
    {
        using var fontScope = font.Push();
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();

        var navy = new System.Numerics.Vector4(0.025f, 0.045f, 0.09f, 0.97f);
        var gold = new System.Numerics.Vector4(0.78f, 0.59f, 0.29f, 1f);
        var paper = new System.Numerics.Vector4(0.89f, 0.82f, 0.67f, 1f);
        draw.AddRectFilled(pos, pos + size, ImGui.GetColorU32(navy), 14f);
        draw.AddRect(pos, pos + size, ImGui.GetColorU32(gold), 14f, ImDrawFlags.None, 2f);
        draw.AddRectFilled(pos + new System.Numerics.Vector2(2f, 2f), pos + new System.Numerics.Vector2(size.X - 2f, 5f), ImGui.GetColorU32(gold), 2f);

        var paperMin = pos + new System.Numerics.Vector2(15f, 74f);
        var paperMax = pos + size - new System.Numerics.Vector2(15f, 53f);
        draw.AddRectFilled(paperMin + new System.Numerics.Vector2(0f, 3f), paperMax + new System.Numerics.Vector2(0f, 3f), ImGui.GetColorU32(new System.Numerics.Vector4(0f, 0f, 0f, 0.24f)), 8f);
        draw.AddRectFilled(paperMin, paperMax, ImGui.GetColorU32(paper), 8f);
        draw.AddRect(paperMin, paperMax, ImGui.GetColorU32(new System.Numerics.Vector4(0.69f, 0.53f, 0.3f, 1f)), 8f, ImDrawFlags.None, 1f);
        var headerSpeaker = string.IsNullOrWhiteSpace(speaker) ? "Разговор" : speaker;
        var maxSpeakerWidth = size.X - 105f;
        while (headerSpeaker.Length > 1 && ImGui.CalcTextSize(headerSpeaker).X > maxSpeakerWidth)
            headerSpeaker = headerSpeaker[..^2] + "…";

        ImGui.SetCursorPos(new System.Numerics.Vector2(25f, 21f));
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.92f, 0.79f, 0.52f, 1f));
        ImGui.TextUnformatted(headerSpeaker);
        ImGui.PopStyleColor();

        ImGui.SetCursorPos(new System.Numerics.Vector2(size.X - 45f, 13f));
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.12f, 0.14f, 0.2f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.35f, 0.18f, 0.16f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.96f, 0.87f, 0.68f, 1f));
        if (ImGui.Button("×##close-dialogue", new System.Numerics.Vector2(30f, 30f)))
        {
            IsOpen = false;
            CloseRequested?.Invoke();
        }
        ImGui.PopStyleColor(3);

        var textWidth = size.X - 62f;
        ImGui.SetCursorPos(new System.Numerics.Vector2(31f, 86f));
        if (ImGui.BeginChild("##dialogue-text", new System.Numerics.Vector2(textWidth, Math.Max(80f, size.Y - 145f)), false, ImGuiWindowFlags.NoBackground))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.16f, 0.13f, 0.1f, 1f));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth - 8f);
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(body) ? "…" : body);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
        }
        ImGui.EndChild();

        var dividerY = size.Y - 44f;
        draw.AddLine(pos + new System.Numerics.Vector2(22f, dividerY), pos + new System.Numerics.Vector2(size.X - 22f, dividerY), ImGui.GetColorU32(new System.Numerics.Vector4(0.65f, 0.5f, 0.29f, 0.48f)), 1f);
        ImGui.SetCursorPos(new System.Numerics.Vector2(25f, size.Y - 35f));
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.83f, 0.75f, 0.61f, 0.9f));
        ImGui.TextUnformatted("Enter / A — продолжить    ·    Esc / × — закрыть");
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

