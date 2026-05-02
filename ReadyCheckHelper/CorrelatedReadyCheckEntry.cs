using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ReadyCheckHelper;

//	A struct to hold data for a player involved in the ready check.  This isn't a game construct, but something
//	for our data model since the game has this information scattered in annoying to use ways.
public struct CorrelatedReadyCheckEntry
{
    public CorrelatedReadyCheckEntry( string name, ulong contentId, uint entityId, ReadyCheckStatus readyState, byte groupIndex, byte memberIndex )
	{
		Name = name;
		ContentId = contentId;
		EntityId = entityId;
		ReadyState = readyState;
		GroupIndex = groupIndex;
		MemberIndex = memberIndex;
	}

    public string Name { get; private set; }
    public ulong ContentId { get; private set; }
    public uint EntityId { get; private set; }
    public ReadyCheckStatus ReadyState { get; private set; }
    public byte GroupIndex { get; private set; }
    public byte MemberIndex { get; private set; }	//	Take care using this; it can be very misleading.
}
