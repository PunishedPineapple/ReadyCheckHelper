using System;
using System.Runtime.InteropServices;

using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;

using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ReadyCheckHelper
{
	public static class MemoryHandler
	{
		public static void Init( SigScanner sigScanner )
		{
			if( sigScanner == null )
			{
				throw new Exception( "Error in \"MemoryHandler.Init()\": A null SigScanner was passed!" );
			}

			//	Get Function Pointers, etc.
			try
			{
				//	When a ready check has been initiated by anyone.
				mfpOnReadyCheckInitiated = sigScanner.ScanText( "40 ?? 48 83 ?? ?? 48 8B ?? E8 ?? ?? ?? ?? 48 ?? ?? ?? 33 C0 ?? 89" );
				if( mfpOnReadyCheckInitiated != IntPtr.Zero )
				{
					mReadyCheckInitiatedHook = Hook<ReadyCheckFuncDelegate>.FromAddress( mfpOnReadyCheckInitiated, ReadyCheckInitiatedDetour );
					mReadyCheckInitiatedHook.Enable();
				}

				//	When a ready check has been completed and processed.
				mfpOnReadyCheckEnd = sigScanner.ScanText( "40 ?? 53 48 ?? ?? ?? ?? 48 81 ?? ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 33 ?? ?? 89 ?? ?? ?? 83 ?? ?? ?? 48 8B ?? 75 ?? 48" );
				if( mfpOnReadyCheckEnd != IntPtr.Zero )
				{
					mReadyCheckEndHook = Hook<ReadyCheckFuncDelegate>.FromAddress( mfpOnReadyCheckEnd, ReadyCheckEndDetour );
					mReadyCheckEndHook.Enable();
				}
			}
			catch( Exception e )
			{
				throw new Exception( $"Error in \"MemoryHandler.Init()\" while searching for required function signatures; this probably means that the plugin needs to be updated due to changes in Final Fantasy XIV.  Raw exception as follows:\r\n{e}" );
			}
		}

		public static void Uninit()
		{
			mReadyCheckInitiatedHook?.Disable();
			mReadyCheckEndHook?.Disable();
			mReadyCheckInitiatedHook?.Dispose();
			mReadyCheckEndHook?.Dispose();
			mReadyCheckInitiatedHook = null;
			mReadyCheckEndHook = null;
			mpReadyCheckObject = IntPtr.Zero;
		}

		private static void ReadyCheckInitiatedDetour( IntPtr ptr )
		{
			mReadyCheckInitiatedHook.Original( ptr );
			PluginLog.LogDebug( $"Ready check initiated with object location: 0x{ptr:X}" );
			mpReadyCheckObject = ptr;
			IsReadyCheckHappening = true;
			ReadyCheckInitiatedEvent?.Invoke( null, EventArgs.Empty );
		}

		private static void ReadyCheckEndDetour( IntPtr ptr )
		{
			mReadyCheckEndHook.Original( ptr );
			mpReadyCheckObject = ptr;   //	Do this for now because we don't get the ready check begin function called if we don't initiate ready check ourselves.
			PluginLog.LogDebug( $"Ready check completed with object location: 0x{ptr:X}" );
			IsReadyCheckHappening = false;
			UpdateRawReadyCheckData();  //	Update our copy of the data one last time.
			//***** TODO: Should we uncomment the next line now? The ready check object never seems to move, but we can't guarantee that...It is nice to keep it around for debugging. Maybe at the end of this function, save it off as a debug only address used only by the debug functions? *****
			//mpReadyCheckObject = IntPtr.Zero;	//Ideally clean this up once the ready check is complete, because this isn't in the static section, so we don't have a guarantee that it's the same every time.  For now, we can't really get rid of it, because we don't have a ready check started hook unless you're the initiator.
			ReadyCheckCompleteEvent?.Invoke( null, EventArgs.Empty );
		}

		private static bool CanGetRawReadyCheckData()
		{
			return mpReadyCheckObject != IntPtr.Zero;
		}

		private static void UpdateRawReadyCheckData()
		{
			lock( mRawReadyCheckArray.SyncRoot )
			{
				if( CanGetRawReadyCheckData() )
				{
					Marshal.Copy( new IntPtr( mpReadyCheckObject.ToInt64() + mArrayOffset ), mRawReadyCheckArray, 0, mRawReadyCheckArray.Length );
				}
			}
		}

		public static IntPtr DEBUG_GetReadyCheckObjectAddress()
		{
			return mpReadyCheckObject;
		}

		public static void DEBUG_SetReadyCheckObjectAddress( IntPtr ptr )
		{
			mpReadyCheckObject = ptr;
		}

		public static bool DEBUG_GetRawReadyCheckObjectStuff( out byte[] rawDataArray )
		{
			rawDataArray = new byte[mArrayOffset];
			if( CanGetRawReadyCheckData() )
			{
				Marshal.Copy( new IntPtr( mpReadyCheckObject.ToInt64() ), rawDataArray, 0, mArrayOffset );
			}
			return CanGetRawReadyCheckData();
		}

		public static bool DEBUG_GetRawReadyCheckData( out IntPtr[] rawDataArray )
		{
			rawDataArray = new IntPtr[mArrayLength];
			UpdateRawReadyCheckData();
			lock( mRawReadyCheckArray.SyncRoot )
			{
				Array.Copy( mRawReadyCheckArray, rawDataArray, mArrayLength );
			}
			return CanGetRawReadyCheckData();
		}

		public static ReadyCheckInfo[] GetReadyCheckInfo()
		{
			UpdateRawReadyCheckData();

			ReadyCheckInfo[] retVal = new ReadyCheckInfo[mArrayLength/2];

			lock( mRawReadyCheckArray.SyncRoot )
			{
				for( int i = 0; i < retVal.Length; ++i )
				{
					retVal[i] = new ReadyCheckInfo( (ReadyCheckState)(mRawReadyCheckArray[i * 2 + 1].ToInt64() & 0xFF),
													(UInt64)mRawReadyCheckArray[i * 2] );
				}
			}

			return retVal;
		}

		internal static unsafe PartyListLayoutResult? GetHUDIndicesForChar( UInt64 contentID, UInt32 objectID )
		{
			if( contentID == 0  && ( objectID == 0 || objectID == 0xE0000000 ) )
			{
				return null;
			}

			//	MS please give us an ?-> operator ;_;
			if( FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() == null ||
				FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() == null ||
				FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule() == null ||
				FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetAgentModule() == null ||
				FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentHUD() == null )
			{
				return null;
			}

			var pAgentHUD = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentHUD();

			//	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
			//	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
			if( FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount > 0 )
			{
				for( int i = 0; i < 8; ++i )
				{
					var offset = i * Marshal.SizeOf<PartyListCharInfo>();
					var pCharData = pAgentHUD->PartyMemberList + offset;
					var charData = *(PartyListCharInfo*)pCharData;
					if( contentID > 0 && contentID == charData.ContentID ) return new( false, 0, i );
					if( objectID > 0 && objectID != 0xE0000000 && objectID == charData.ObjectID ) return new( false, 0, i );
				}
				for( int i = 0; i < 40; ++i )
				{
					if( objectID > 0 && objectID != 0xE0000000 && objectID == pAgentHUD->RaidMemberIds[i] )
					{
						return new( false, i / 8 + 1, i % 8 );
					}
				}
			}
			else if( FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm > 0 )
			{
				var pGroupMember = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetMemberByContentId( contentID );
				if( pGroupMember == null || contentID == 0 ) return null;
				return new( true, pGroupMember->GroupIndex, pGroupMember->MemberIndex );
			}

			return null;
		}

		//	Magic Numbers
		private static readonly int mArrayOffset = 0xB0;
		private static readonly int mArrayLength = 96;

		//	Misc.
		private static IntPtr mpReadyCheckObject;
		private static readonly IntPtr[] mRawReadyCheckArray = new IntPtr[mArrayLength]; //Need to use IntPtr as the type here because of our marshaling options.  Can convert it later.

		public static bool IsReadyCheckHappening { get; private set; } = false;

		//	Delgates
		private delegate void ReadyCheckFuncDelegate( IntPtr ptr );

		private static IntPtr mfpOnReadyCheckInitiated = IntPtr.Zero;
		private static Hook<ReadyCheckFuncDelegate> mReadyCheckInitiatedHook;

		private static IntPtr mfpOnReadyCheckEnd = IntPtr.Zero;
		private static Hook<ReadyCheckFuncDelegate> mReadyCheckEndHook;

		//	Events
		public static event EventHandler ReadyCheckInitiatedEvent;
		public static event EventHandler ReadyCheckCompleteEvent;

		public struct ReadyCheckInfo
		{
			public ReadyCheckInfo( ReadyCheckState readyFlag, UInt64 id )
			{
				ReadyFlag = readyFlag;
				ID = id;
			}

			public ReadyCheckState ReadyFlag { get; private set; }
			public UInt64 ID { get; private set; }
		}
	}

	internal struct PartyListLayoutResult
	{
		internal PartyListLayoutResult( bool crossWorld, int groupNumber, int partyMemberIndex )
		{
			CrossWorld = crossWorld;
			GroupNumber = groupNumber;
			PartyMemberIndex = partyMemberIndex;
		}

		internal bool CrossWorld;
		internal int GroupNumber;
		internal int PartyMemberIndex;
	}
}