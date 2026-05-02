using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ReadyCheckHelper.Resources;

namespace ReadyCheckHelper.Windows;

public class ProcessedWindow : Window, IDisposable
{
    private readonly Plugin Plugin;


    public ProcessedWindow(Plugin plugin) : base($"{Language.WindowTitleProcessedReadyCheckData}###Processed Ready Check Data")
    {
        Plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 340),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public void Dispose() { }

    public override void Draw()
    {
        using var mono = ImRaii.PushFont(UiBuilder.MonoFont);

        var list = Plugin.GetProcessedReadyCheckData();
        foreach (var player in list)
            ImGui.Text($"OID: {player.EntityId:X8}, CID: {player.ContentId:X16}, Group: {player.GroupIndex}, Index: {player.MemberIndex}, State: {(byte)player.ReadyState}, Name: {player.Name}");

        if (ImGui.Button($"{Language.ButtonClose}###Close"))
            IsOpen = false;
    }
}