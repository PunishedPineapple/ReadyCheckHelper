using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ReadyCheckHelper;

public unsafe class MemoryHandler
{
    //	Events
    public event EventHandler? ReadyCheckInitiatedEvent;
    public event EventHandler? ReadyCheckCompleteEvent;

    private static Hook<AgentReadyCheck.Delegates.InitiateReadyCheck> ReadyCheckInitiatedHook = null!;
    private static Hook<AgentReadyCheck.Delegates.EndReadyCheck> ReadyCheckEndHook= null!;

    public MemoryHandler()
    {
        ReadyCheckInitiatedHook = Plugin.Hook.HookFromAddress<AgentReadyCheck.Delegates.InitiateReadyCheck>(AgentReadyCheck.MemberFunctionPointers.InitiateReadyCheck, ReadyCheckInitiatedDetour);
        ReadyCheckInitiatedHook.Enable();

        ReadyCheckEndHook = Plugin.Hook.HookFromAddress<AgentReadyCheck.Delegates.EndReadyCheck>(AgentReadyCheck.MemberFunctionPointers.EndReadyCheck, ReadyCheckEndDetour);
        ReadyCheckEndHook.Enable();
    }

    public void Dispose()
    {
        ReadyCheckInitiatedHook?.Dispose();
        ReadyCheckEndHook?.Dispose();
    }

    private void ReadyCheckInitiatedDetour(AgentReadyCheck* ptr)
    {
        ReadyCheckInitiatedHook.Original(ptr);

        try
        {
            ReadyCheckInitiatedEvent?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in ReadyCheckInitiatedEvent handler");
        }
    }

    private void ReadyCheckEndDetour(AgentReadyCheck* ptr)
    {
        ReadyCheckEndHook.Original(ptr);

        try
        {
            ReadyCheckCompleteEvent?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in ReadyCheckCompleteEvent handler");
        }
    }

    internal PartyListLayoutResult? GetHUDIndicesForChar(ulong contentId, uint entityId)
    {
        if (contentId == 0 && entityId is 0 or 0xE0000000)
            return null;

        var infoProxyCrossRealm = InfoProxyCrossRealm.Instance();
        var groupManager = GroupManager.Instance();
        var agentHud = AgentHUD.Instance();
        if (infoProxyCrossRealm == null || groupManager == null || agentHud == null)
            return null;

        //	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
        //	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
        if (groupManager->MainGroup.MemberCount > 0)
        {
            for (var i = 0; i < 8; ++i)
            {
                var charData = agentHud->PartyMembers[i];
                if (contentId > 0 && contentId == charData.ContentId)
                    return new PartyListLayoutResult(false, 0, i);

                if (entityId > 0 && entityId != 0xE0000000 && entityId == charData.EntityId)
                    return new PartyListLayoutResult(false, 0, i);
            }

            for (var i = 0; i < 40; ++i)
            {
                if (entityId > 0 && entityId != 0xE0000000 && entityId == agentHud->RaidMemberIds[i])
                    return new PartyListLayoutResult(false, i / 8 + 1, i % 8);
            }
        }
        else if (infoProxyCrossRealm->IsCrossRealm)
        {
            var pGroupMember = InfoProxyCrossRealm.GetMemberByContentId(contentId);
            if (pGroupMember == null || contentId == 0)
                return null;

            //  In order to match how this plugin indexes in the UI, for cross-world alliances, we need
            //  to make the player's group be 0, and shift any groups before the player's group by one.
            var groupIndex = pGroupMember->GroupIndex;
            if( groupIndex == infoProxyCrossRealm->LocalPlayerGroupIndex ) groupIndex = 0;
            else if( groupIndex < infoProxyCrossRealm->LocalPlayerGroupIndex )
                     groupIndex += 1;

            return new PartyListLayoutResult( !infoProxyCrossRealm->IsInAllianceRaid, groupIndex, pGroupMember->MemberIndex);
        }

        return null;
    }
}

public struct PartyListLayoutResult
{
    public PartyListLayoutResult(bool crossWorld, int groupNumber, int partyMemberIndex)
    {
        CrossWorld = crossWorld;
        GroupIndex = groupNumber;
        PartyMemberIndex = partyMemberIndex;
    }

    public readonly bool CrossWorld;
    public readonly int GroupIndex;
    public readonly int PartyMemberIndex;
}