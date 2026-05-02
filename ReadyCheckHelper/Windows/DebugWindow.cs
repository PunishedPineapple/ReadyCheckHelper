using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;
using ReadyCheckHelper.Resources;

namespace ReadyCheckHelper.Windows;

public class DebugWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private readonly Dictionary<uint, string> JobDict = new();

    public bool DrawPlaceholderData;
    private bool AllowCrossWorldAllianceDrawing;
    private int NumNamesToTestChatMessage = 5;

    public DebugWindow(Plugin plugin) : base($"{Language.WindowTitleReadyCheckandAllianceDebugData}###Ready Check and Alliance Debug Data")
    {
        Plugin = plugin;

        var classJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        foreach(var job in classJobSheet)
            JobDict.Add(job.RowId, job.Abbreviation.ToString());

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 340),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public void Dispose()
    {
    }

    public override unsafe void Draw()
    {
        //	Draw the window.

        using var mono = ImRaii.PushFont(UiBuilder.MonoFont);

        var pAgentHUD = AgentHUD.Instance();
        var groupManager = GroupManager.Instance();
        var infoproxy = InfoProxyCrossRealm.Instance();
        if ((nint)groupManager == nint.Zero)
        {
            ImGui.Text("The GroupManager instance pointer is null!");
            return;
        }

        if ((nint)infoproxy == nint.Zero)
        {
            ImGui.Text("The InfoProxyCrossRealm instance pointer is null!");
            return;
        }

        ImGui.Columns(5);
        ImGui.Text("General Info:");

        ImGui.Text($"Number of Party Members: {groupManager->MainGroup.MemberCount}");
        ImGui.Text($"Is Cross-World: {infoproxy->IsCrossRealm}");

        var crossWorldGroupCount = infoproxy->GroupCount;
        ImGui.Text($"Number of Cross-World Groups: {crossWorldGroupCount}");
        for (var i = 0; i < crossWorldGroupCount; ++i)
            ImGui.Text($"Number of Party Members (Group {i}): {InfoProxyCrossRealm.GetGroupMemberCount(i)}");

        ImGui.Text($"Ready check is active: {Plugin.ReadyCheckActive}");
        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Text($"Hud Agent Address: 0x{new nint(pAgentHUD):X}");

        var isOpen = Plugin.ProcessedWindow.IsOpen;
        if (ImGui.Checkbox("Show Processed Readycheck Data", ref isOpen))
            Plugin.ProcessedWindow.IsOpen = isOpen;

        ImGui.Checkbox("Debug Drawing on Party List", ref DrawPlaceholderData);
        ImGui.Checkbox("Allow Cross-world Alliance List Drawing", ref AllowCrossWorldAllianceDrawing);

        if (ImGui.Button("Test Chat Message"))
            Plugin.ListUnreadyPlayersInChat([..LocalizationHelpers.TestNames.Take(NumNamesToTestChatMessage)]);

        ImGui.SliderInt("Number of Test Names", ref NumNamesToTestChatMessage, 1, LocalizationHelpers.TestNames.Length);

        ImGui.NextColumn();
        ImGui.Text("Ready Check Data:");
        var readyCheckdata = AgentReadyCheck.Instance()->ReadyCheckEntries;
        for (var i = 0; i < readyCheckdata.Length; ++i)
            ImGui.Text($"ID: {readyCheckdata[i].ContentId:X16}, State: {readyCheckdata[i].Status}");

        ImGui.NextColumn();

        ImGui.Text("Party Data:");
        for (var i = 0; i < 8; ++i)
        {
            var pGroupMember = groupManager->MainGroup.GetPartyMemberByIndex(i);
            if ((nint)pGroupMember != nint.Zero)
            {
                var name = pGroupMember->NameString;
                var abbr = JobDict.GetValueOrDefault(pGroupMember->ClassJob, "ERR");
                ImGui.Text($"Job: {abbr}, OID: {pGroupMember->EntityId:X8}, CID: {pGroupMember->ContentId:X16}, Name: {name}");
            }
            else
            {
                ImGui.Text("Party member returned as null pointer.");
            }
        }

        for (var i = 0; i < 16; ++i)
        {
            var pGroupMember = groupManager->MainGroup.GetAllianceMemberByIndex(i);
            if ((nint)pGroupMember != nint.Zero)
            {
                var name = pGroupMember->NameString;
                var abbr = JobDict.GetValueOrDefault(pGroupMember->ClassJob, "ERR");
                ImGui.Text($"Job: {abbr}, OID: {pGroupMember->EntityId:X8}, CID: {pGroupMember->ContentId:X16}, Name: {name}");
            }
            else
            {
                ImGui.Text("Alliance member returned as null pointer.");
            }
        }

        ImGui.NextColumn();
        ImGui.Text("Cross-World Party Data:");
        for (var i = 0; i < crossWorldGroupCount; ++i)
        {
            for (var j = 0; j < InfoProxyCrossRealm.GetGroupMemberCount(i); ++j)
            {
                var pGroupMember = InfoProxyCrossRealm.GetGroupMember((uint)j, i);
                if ((nint)pGroupMember != nint.Zero)
                {
                    var name = pGroupMember->NameString;
                    ImGui.Text($"Group: {pGroupMember->GroupIndex}, OID: {pGroupMember->EntityId:X8}, CID: {pGroupMember->ContentId:X16}, Name: {name}");
                }
            }
        }

        ImGui.NextColumn();
        ImGui.Text($"AgentHUD Group Size: {pAgentHUD->RaidGroupSize}");
        ImGui.Text($"AgentHUD Party Size: {pAgentHUD->PartyMemberCount}");
        ImGui.Text("AgentHUD Party Members:");
        for (var i = 0; i < 8; ++i)
        {
            var partyMemberData = pAgentHUD->PartyMembers[i];
            ImGui.Text($"Object Address: 0x{(nint)partyMemberData.Object:X}\r\nName Address: 0x{(nint)partyMemberData.Name.Value:X}\r\nName: {partyMemberData.Name.ToString()}\r\nCID: {partyMemberData.ContentId:X}\r\nOID: {partyMemberData.EntityId:X}");
        }

        ImGui.Text("AgentHUD Raid Members:");
        for (var i = 0; i < 40; ++i)
            ImGui.Text($"{i:D2}: {pAgentHUD->RaidMemberIds[i]:X8}");

        ImGui.Columns();
    }
}