using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ReadyCheckHelper
{
    public static class MemoryHandler
    {
        //	Events
        public static event EventHandler ReadyCheckInitiatedEvent;
        public static event EventHandler ReadyCheckCompleteEvent;

        private static Hook<AgentReadyCheck.Delegates.InitiateReadyCheck> MReadyCheckInitiatedHook;
        private static Hook<AgentReadyCheck.Delegates.EndReadyCheck> MReadyCheckEndHook;

        public static unsafe void Init()
        {
            try
            {
                MReadyCheckInitiatedHook = Plugin.Hook.HookFromAddress<AgentReadyCheck.Delegates.InitiateReadyCheck>(AgentReadyCheck.MemberFunctionPointers.InitiateReadyCheck, ReadyCheckInitiatedDetour);
                MReadyCheckInitiatedHook.Enable();

                MReadyCheckEndHook = Plugin.Hook.HookFromAddress<AgentReadyCheck.Delegates.EndReadyCheck>(AgentReadyCheck.MemberFunctionPointers.EndReadyCheck, ReadyCheckEndDetour);
                MReadyCheckEndHook.Enable();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while searching for required function signatures; this probably means that the plugin needs to be updated due to changes in Final Fantasy XIV.\n{ex}");
            }
        }

        public static void Uninit()
        {
            MReadyCheckInitiatedHook?.Dispose();
            MReadyCheckEndHook?.Dispose();
        }

        private static unsafe void ReadyCheckInitiatedDetour(AgentReadyCheck* ptr)
        {
            MReadyCheckInitiatedHook.Original(ptr);
            ReadyCheckInitiatedEvent?.Invoke(null, EventArgs.Empty);
        }

        private static unsafe void ReadyCheckEndDetour(AgentReadyCheck* ptr)
        {
            MReadyCheckEndHook.Original(ptr);

            //	Update our copy of the data one last time.
            ReadyCheckCompleteEvent?.Invoke(null, EventArgs.Empty);
        }

        internal static unsafe PartyListLayoutResult? GetHUDIndicesForChar(ulong ContentId, uint EntityId)
        {
            if (ContentId == 0 && EntityId is 0 or 0xE0000000)
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
                    if (ContentId > 0 && ContentId == charData.ContentId)
                        return new PartyListLayoutResult(false, 0, i);

                    if (EntityId > 0 && EntityId != 0xE0000000 && EntityId == charData.EntityId)
                        return new PartyListLayoutResult(false, 0, i);
                }

                for (var i = 0; i < 40; ++i)
                {
                    if (EntityId > 0 && EntityId != 0xE0000000 && EntityId == agentHud->RaidMemberIds[i])
                        return new PartyListLayoutResult(false, i / 8 + 1, i % 8);
                }
            }
            else if (infoProxyCrossRealm->IsCrossRealm)
            {
                var pGroupMember = InfoProxyCrossRealm.GetMemberByContentId(ContentId);
                if (pGroupMember == null || ContentId == 0)
                    return null;
                return new PartyListLayoutResult(true, pGroupMember->GroupIndex, pGroupMember->MemberIndex);
            }

            return null;
        }
    }

    internal struct PartyListLayoutResult
    {
        internal PartyListLayoutResult(bool crossWorld, int groupNumber, int partyMemberIndex)
        {
            CrossWorld = crossWorld;
            GroupNumber = groupNumber;
            PartyMemberIndex = partyMemberIndex;
        }

        internal readonly bool CrossWorld;
        internal readonly int GroupNumber;
        internal readonly int PartyMemberIndex;
    }
}