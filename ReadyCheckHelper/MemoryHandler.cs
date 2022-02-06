using System;
using System.Runtime.InteropServices;

using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;

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

			//	Create underlying array.
			mRawReadyCheckArray = new IntPtr[mArrayLength];

			//	Get Function Pointers, etc.
			try
			{
				mfpOnReadyCheckBegin = sigScanner.ScanText( "40 ?? 48 83 ?? ?? ?? B9 ?? ?? ?? ?? ?? 48 8B ?? ?? 41 ?? ?? ?? ?? ?? 75" );
				if( mfpOnReadyCheckBegin != IntPtr.Zero )
				{
					mReadyCheckBeginHook = new Hook<ReadyCheckFuncDelegate>( mfpOnReadyCheckBegin, mdReadyCheckBegin );
					mReadyCheckBeginHook.Enable();
				}

				mfpOnReadyCheckEnd = sigScanner.ScanText( "40 ?? 53 48 ?? ?? ?? ?? 48 81 ?? ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 33 ?? ?? 89 ?? ?? ?? 83 ?? ?? ?? 48 8B ?? 75 ?? 48" );
				if( mfpOnReadyCheckEnd != IntPtr.Zero )
				{
					mReadyCheckEndHook = new Hook<ReadyCheckFuncDelegate>( mfpOnReadyCheckEnd, mdReadyCheckEnd );
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
			mReadyCheckBeginHook.Disable();
			mReadyCheckEndHook.Disable();
			mReadyCheckBeginHook.Dispose();
			mReadyCheckEndHook.Dispose();
			mReadyCheckBeginHook = null;
			mReadyCheckEndHook = null;
			mpReadyCheckObject = IntPtr.Zero;
			mRawReadyCheckArray = null;
		}

		private static void ReadyCheckBeginDetour( IntPtr ptr )
		{
			mReadyCheckBeginHook.Original( ptr );
			PluginLog.LogInformation( $"Ready check initiated by self with object location: 0x{ptr.ToString( "X" )}" );
			mpReadyCheckObject = ptr;
			IsReadyCheckHappening = true;
		}

		private static void ReadyCheckEndDetour( IntPtr ptr )
		{
			mReadyCheckEndHook.Original( ptr );
			mpReadyCheckObject = ptr;   //	Do this for now because we don't get the ready check begin function called if we don't initiate ready check ourselves.
			PluginLog.LogInformation( $"Ready check completed with object location: 0x{ptr.ToString( "X" )}" );
			IsReadyCheckHappening = false;
			UpdateRawReadyCheckData();  //	Update our copy of the data one last time.
										//mpReadyCheckObject = IntPtr.Zero;	//Ideally clean this up once the ready check is complete, because this isn't in the static section, so we don't have a guarantee that it's the same every time.  For now, we can't really get rid of it, because we don't have a ready check started hook unless you're the initiator.
			if( ReadyCheckCompleteEvent != null ) ReadyCheckCompleteEvent( null, EventArgs.Empty );
		}

		private static bool CanGetRawReadyCheckData()
		{
			return mpReadyCheckObject != IntPtr.Zero && mRawReadyCheckArray != null;
		}

		private static void UpdateRawReadyCheckData()
		{
			if( CanGetRawReadyCheckData() )
			{
				Marshal.Copy( new IntPtr( mpReadyCheckObject.ToInt64() + mArrayOffset ), mRawReadyCheckArray, 0, mArrayLength );
			}
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
			Array.Copy( mRawReadyCheckArray, rawDataArray, mArrayLength );
			return CanGetRawReadyCheckData();
		}

		public static ReadyCheckInfo[] GetReadyCheckInfo()
		{
			UpdateRawReadyCheckData();

			ReadyCheckInfo[] retVal = new ReadyCheckInfo[mArrayLength/2];

			if( mRawReadyCheckArray != null )
			{
				for( int i = 0; i < retVal.Length; ++i )
				{
					retVal[i] = new ReadyCheckInfo( (ReadyCheckStateEnum)(mRawReadyCheckArray[i * 2 + 1].ToInt64() & 0xFF),
													(UInt64)mRawReadyCheckArray[i * 2] );
				}
			}

			return retVal;
		}

		//	Magic Numbers
		private static readonly int mArrayOffset = 0xB0;
		private static readonly int mArrayLength = 96;

		//	Misc.
		private static IntPtr mpReadyCheckObject;
		private static IntPtr[] mRawReadyCheckArray; //Need to use IntPtr as the type here because of our marshaling options.  Can convert it later.

		public static bool IsReadyCheckHappening { get; private set; } = false;

		//	Delgates
		private delegate void ReadyCheckFuncDelegate( IntPtr ptr );

		private static ReadyCheckFuncDelegate mdReadyCheckBegin = new ReadyCheckFuncDelegate( ReadyCheckBeginDetour );
		private static IntPtr mfpOnReadyCheckBegin = IntPtr.Zero;
		private static Hook<ReadyCheckFuncDelegate> mReadyCheckBeginHook;

		private static ReadyCheckFuncDelegate mdReadyCheckEnd = new ReadyCheckFuncDelegate( ReadyCheckEndDetour );
		private static IntPtr mfpOnReadyCheckEnd = IntPtr.Zero;
		private static Hook<ReadyCheckFuncDelegate> mReadyCheckEndHook;

		//	Events
		public static event EventHandler ReadyCheckCompleteEvent;

		//*****TODO: If we decide to not create a new array each time we're asked for the data, We need to have a synchronization object and use it for outside access vs. when we're updating our data.*****
		//private static Object mLockObj = new object();

		public struct ReadyCheckInfo
		{
			public ReadyCheckInfo( ReadyCheckStateEnum readyFlag, UInt64 id )
			{
				ReadyFlag = readyFlag;
				ID = id;
			}

			public ReadyCheckStateEnum ReadyFlag { get; private set; }
			public UInt64 ID { get; private set; }
		}

		public enum ReadyCheckStateEnum : byte
		{
			Unknown = 0,
			AwaitingResponse = 1,
			Ready = 2,
			NotReady = 3,
			CrossWorldMemberNotPresent = 4
		}
	}
}