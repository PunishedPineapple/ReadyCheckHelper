using System;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Data.Files;

namespace ReadyCheckHelper.Windows;

public class PartyListOverlay : Window, IDisposable
{
    private readonly Plugin Plugin;

    private bool ReadyCheckValid;
    private readonly IDalamudTextureWrap ReadyCheckIconTexture;
    private readonly IDalamudTextureWrap NotPresentIconTexture;

    public PartyListOverlay(Plugin plugin) : base("##ReadyCheckOverlayWindow")
    {
        Plugin = plugin;
        ReadyCheckIconTexture = Plugin.Texture.CreateFromTexFile(Plugin.DataManager.GetFile<TexFile>("ui/uld/ReadyCheck_hr1.tex")!);
        NotPresentIconTexture = Plugin.Texture.GetFromGameIcon(61504).RentAsync().Result;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(180, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        ForceMainWindow = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoNav;
    }

    public void Dispose()
    {
    }

    public override void PreOpenCheck()
    {
        IsOpen = Plugin.DebugWindow.DrawPlaceholderData || (Plugin.Configuration.ShowReadyCheckOnPartyAllianceList && ReadyCheckValid);
    }

    public override void PreDraw()
    {
        Size = ImGuiHelpers.MainViewport.Size;
        Position = ImGuiHelpers.MainViewport.Pos;
    }

    public override unsafe void Draw()
    {
        var pPartyList = (AddonPartyList*)Plugin.GameGui.GetAddonByName("_PartyList").Address;
        var pAlliance1List = (AddonAllianceListX*)Plugin.GameGui.GetAddonByName("_AllianceList1").Address;
        var pAlliance2List = (AddonAllianceListX*)Plugin.GameGui.GetAddonByName("_AllianceList2").Address;
        var pCrossWorldAllianceList = (AddonAlliance48*)Plugin.GameGui.GetAddonByName("Alliance48").Address;

        var drawList = ImGui.GetWindowDrawList();
        if (Plugin.DebugWindow.DrawPlaceholderData)
        {
            if ((nint)pPartyList != nint.Zero && pPartyList->IsVisible)
            {
                for (var i = 0; i < 8; ++i)
                    DrawOnPartyList(i, ReadyCheckStatus.Ready, pPartyList, drawList);
            }

            if ((nint)pAlliance1List != nint.Zero && pAlliance1List->IsVisible)
            {
                for (var i = 0; i < 8; ++i)
                    DrawOnAllianceList(i, ReadyCheckStatus.Ready, pAlliance1List, drawList);
            }

            if ((nint)pAlliance2List != nint.Zero && pAlliance2List->IsVisible)
            {
                for (var i = 0; i < 8; ++i)
                    DrawOnAllianceList(i, ReadyCheckStatus.Ready, pAlliance2List, drawList);
            }

            if ((nint)pCrossWorldAllianceList != nint.Zero && pCrossWorldAllianceList->IsVisible)
            {
                for (var j = 1; j < 6; ++j)
                for (var i = 0; i < 8; ++i)
                    DrawOnCrossWorldAllianceList(j, i, ReadyCheckStatus.Ready, pCrossWorldAllianceList, drawList);
            }
        }
        else
        {
            var data = Plugin.GetProcessedReadyCheckData();
            if (data == null)
                return;

            foreach (var result in data)
            {
                var indices = MemoryHandler.GetHUDIndicesForChar(result.ContentId, result.EntityId);
                if (indices == null)
                    continue;

                switch (indices.Value.GroupNumber)
                {
                    case 0:
                        DrawOnPartyList(indices.Value.PartyMemberIndex, result.ReadyState, pPartyList, drawList);
                        break;
                    case 1:
                        if (indices.Value.CrossWorld)
                            DrawOnCrossWorldAllianceList(indices.Value.GroupNumber, indices.Value.PartyMemberIndex, result.ReadyState, pCrossWorldAllianceList, drawList);
                        else
                            DrawOnAllianceList(indices.Value.PartyMemberIndex, result.ReadyState, pAlliance1List, drawList);
                        break;
                    case 2:
                        if (indices.Value.CrossWorld)
                            DrawOnCrossWorldAllianceList(indices.Value.GroupNumber, indices.Value.PartyMemberIndex, result.ReadyState, pCrossWorldAllianceList, drawList);
                        else
                            DrawOnAllianceList(indices.Value.PartyMemberIndex, result.ReadyState, pAlliance2List, drawList);
                        break;
                    default:
                        if (indices.Value.CrossWorld)
                            DrawOnCrossWorldAllianceList(indices.Value.GroupNumber, indices.Value.PartyMemberIndex, result.ReadyState, pCrossWorldAllianceList, drawList);
                        break;
                }
            }
        }
    }

    private unsafe void DrawOnPartyList(int index, ReadyCheckStatus readyCheckState, AddonPartyList* pPartyList, ImDrawListPtr drawList)
    {
        if (index is < 0 or > 7)
            return;

        if ((nint)pPartyList == nint.Zero || !IsActuallyVisible(pPartyList->AtkUnitBase))
            return;

        var partyMember = pPartyList->PartyMembers[index];
        var pPartyMemberNode = partyMember.PartyMemberComponent->OwnerNode;
        var pIconNode = partyMember.ClassJobIcon;
        var partyAlign = pPartyList->PartyListAtkResNode->Y;

        //	Note: sub-nodes don't scale, so we have to account for the addon's scale.
        var iconOffset = (new Vector2(-7, -5) + Plugin.Configuration.PartyListIconOffset) * pPartyList->Scale;
        var iconSize = new Vector2(pIconNode->Width / 1.5f, pIconNode->Height / 1.5f) * Plugin.Configuration.PartyListIconScale * pPartyList->Scale;
        var iconPos = new Vector2(pPartyList->X + pPartyMemberNode->AtkResNode.X * pPartyList->Scale + pIconNode->X * pPartyList->Scale + pIconNode->Width * pPartyList->Scale / 2, pPartyList->Y + partyAlign + pPartyMemberNode->AtkResNode.Y * pPartyList->Scale + pIconNode->Y * pPartyList->Scale + pIconNode->Height * pPartyList->Scale / 2);
        iconPos += iconOffset;

        if (readyCheckState == ReadyCheckStatus.NotReady)
            drawList.AddImage(ReadyCheckIconTexture.Handle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        else if (readyCheckState == ReadyCheckStatus.Ready)
            drawList.AddImage(ReadyCheckIconTexture.Handle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
        else if (readyCheckState == ReadyCheckStatus.MemberNotPresent)
            drawList.AddImage(NotPresentIconTexture.Handle, iconPos, iconPos + iconSize);
    }

    private unsafe void DrawOnAllianceList(int index, ReadyCheckStatus readyCheckState, AddonAllianceListX* pAllianceList, ImDrawListPtr drawList)
    {
        if (index is < 0 or > 7)
            return;

        if ((nint)pAllianceList == nint.Zero || !IsActuallyVisible(pAllianceList->AtkUnitBase))
            return;

        var allianceMember = pAllianceList->AllianceMembers[index];
        var allianceMemberNode = allianceMember.ComponentBase->OwnerNode;
        var pIconNode = allianceMember.ComponentBase->GetImageNodeById(9)->GetAsAtkImageNode();

        var iconOffset = (new Vector2(0, 0) + Plugin.Configuration.AllianceListIconOffset) * pAllianceList->Scale;
        var iconSize = new Vector2(pIconNode->Width / 3.0f, pIconNode->Height / 3.0f) * Plugin.Configuration.AllianceListIconScale * pAllianceList->Scale;
        var iconPos = new Vector2(pAllianceList->X + allianceMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2, pAllianceList->Y + allianceMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2);
        iconPos += iconOffset;

        if (readyCheckState == ReadyCheckStatus.NotReady)
            drawList.AddImage(ReadyCheckIconTexture.Handle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        else if (readyCheckState == ReadyCheckStatus.Ready)
            drawList.AddImage(ReadyCheckIconTexture.Handle, iconPos, iconPos + iconSize, new Vector2(0.0f), new Vector2(0.5f, 1.0f));
        else if (readyCheckState == ReadyCheckStatus.MemberNotPresent)
            drawList.AddImage(NotPresentIconTexture.Handle, iconPos, iconPos + iconSize);
    }

    private unsafe void DrawOnCrossWorldAllianceList(int allianceIndex, int partyMemberIndex, ReadyCheckStatus readyCheckState, AddonAlliance48* pAllianceList, ImDrawListPtr drawList)
    {
        if (allianceIndex is < 1 or > 5)
            return;

        if (partyMemberIndex is < 0 or > 7)
            return;

        if ((nint)pAllianceList == nint.Zero || !IsActuallyVisible(pAllianceList->AtkUnitBase))
            return;

        var alliance = pAllianceList->Alliances[allianceIndex-1]; // Group 1 is not in the span, so we need to subtract 1 group from this
        var allianceNode = alliance.ComponentBase->OwnerNode;
        var allianceMember = alliance.Members[partyMemberIndex];
        var allianceMemberNode = allianceMember.AtkComponentBase->OwnerNode;
        var pIconNode = allianceMember.AtkComponentBase->GetImageNodeById(2)->GetAsAtkImageNode();

        var iconOffset = (new Vector2(0, 0) + Plugin.Configuration.CrossWorldAllianceListIconOffset) * pAllianceList->Scale;
        var iconSize = new Vector2(pIconNode->Width / 2.0f, pIconNode->Height / 2.0f) * Plugin.Configuration.CrossWorldAllianceListIconScale * pAllianceList->Scale;
        var iconPos = new Vector2(pAllianceList->X + allianceNode->AtkResNode.X * pAllianceList->Scale + allianceMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2, pAllianceList->Y + allianceNode->AtkResNode.Y * pAllianceList->Scale + allianceMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2);
        iconPos += iconOffset;

        if (readyCheckState == ReadyCheckStatus.NotReady)
            drawList.AddImage(ReadyCheckIconTexture.Handle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        else if (readyCheckState == ReadyCheckStatus.Ready)
            drawList.AddImage(ReadyCheckIconTexture.Handle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
        else if (readyCheckState == ReadyCheckStatus.MemberNotPresent)
            drawList.AddImage(NotPresentIconTexture.Handle, iconPos, iconPos + iconSize);
    }

    public void ShowReadyCheckOverlay()
    {
        ReadyCheckValid = true;
    }

    public void InvalidateReadyCheck()
    {
        ReadyCheckValid = false;
    }

    public static unsafe bool IsActuallyVisible(AtkUnitBase addon)
    {
        if (!addon.IsVisible)
            return false;
        if (addon.RootNode is null)
            return false;
        if (!addon.RootNode->IsVisible())
            return false;
        if ((addon.VisibilityFlags & 5) is not 0)
            return false;

        return true;
    }
}