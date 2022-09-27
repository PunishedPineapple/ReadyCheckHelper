using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadyCheckHelper;

//	A struct to hold data for a player involved in the ready check.  This isn't a game construct, but something
//	for our data model since the game has this information scattered in annoying to use ways.
internal struct CorrelatedReadyCheckEntry
{
	internal CorrelatedReadyCheckEntry( string name, UInt64 contentID, UInt32 objectID, ReadyCheckState readyState, byte groupIndex, byte memberIndex )
	{
		Name = name;
		ContentID = contentID;
		ObjectID = objectID;
		ReadyState = readyState;
		GroupIndex = groupIndex;
		MemberIndex = memberIndex;
	}

	internal string Name { get; private set; }
	internal UInt64 ContentID { get; private set; }
	internal UInt32 ObjectID { get; private set; }
	internal ReadyCheckState ReadyState { get; private set; }
	internal byte GroupIndex { get; private set; }
	internal byte MemberIndex { get; private set; }	//	Take care using this; it can be very misleading.
}
