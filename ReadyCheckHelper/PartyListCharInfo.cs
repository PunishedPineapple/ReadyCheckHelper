using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ReadyCheckHelper;

//	This is how the party list data is laid out in AgentHUD.PartyMemberList.  It's a sequential array of this struct.
[StructLayout( LayoutKind.Explicit, Size = 0x20 )]
internal struct PartyListCharInfo
{
	[FieldOffset(0x00)] internal IntPtr ObjectAddress;
	[FieldOffset(0x08)] internal IntPtr ObjectNameAddress;
	[FieldOffset(0x10)] internal UInt64 ContentID;
	[FieldOffset(0x18)] internal UInt32 ObjectID;
	[FieldOffset(0x1C)] internal UInt32 Unknown;

	internal string GetName()
	{
		if( ObjectAddress == IntPtr.Zero || ObjectNameAddress == IntPtr.Zero ) return "";

		return Marshal.PtrToStringUTF8( ObjectNameAddress ) ?? "";
	}
}
