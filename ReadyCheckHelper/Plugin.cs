using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using Dalamud.Plugin;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game;
using Dalamud.Data;
using Dalamud.Logging;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CheapLoc;

namespace ReadyCheckHelper
{
	public class Plugin : IDalamudPlugin
	{
		//	Initialization
		public Plugin(
			DalamudPluginInterface pluginInterface,
			Framework framework,
			ClientState clientState,
			CommandManager commandManager,
			Dalamud.Game.ClientState.Conditions.Condition condition,
			ChatGui chatGui,
			GameGui gameGui,
			DataManager dataManager,
			SigScanner sigScanner )
		{
			//	API Access
			mPluginInterface	= pluginInterface;
			mFramework			= framework;
			mClientState		= clientState;
			mCommandManager		= commandManager;
			mCondition			= condition;
			mChatGui			= chatGui;
			mGameGui			= gameGui;
			mSigScanner			= sigScanner;
			mDataManager		= dataManager;

			//	Configuration
			mPluginInterface = pluginInterface;
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			mConfiguration.Initialize( mPluginInterface );
			MemoryHandler.Init( mSigScanner );

			//	Localization and Command Initialization
			OnLanguageChanged( mPluginInterface.UiLanguage );
			mOpenReadyCheckWindowLink = mPluginInterface.AddChatLinkHandler( 1001, ( i, m ) =>
			{
				ShowBestAvailableReadyCheckWindow();
			} );

			//	UI Initialization
			mUI = new PluginUI( this, mPluginInterface, mConfiguration, mDataManager, mGameGui, mSigScanner );
			mPluginInterface.UiBuilder.Draw += DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
			mUI.Initialize();

			//	Misc.
			PopulateInstancedTerritoriesList();

			//	Event Subscription
			mPluginInterface.LanguageChanged += OnLanguageChanged;
			mCondition.ConditionChange += OnConditionChanged;
			mClientState.TerritoryChanged += OnTerritoryChanged;
			mClientState.Logout += OnLogout;
			mFramework.Update += OnGameFrameworkUpdate;
			MemoryHandler.ReadyCheckInitiatedEvent += OnReadyCheckInitiated;
			MemoryHandler.ReadyCheckCompleteEvent += OnReadyCheckCompleted;
		}

		//	Cleanup
		public void Dispose()
		{
			MemoryHandler.ReadyCheckInitiatedEvent -= OnReadyCheckInitiated;
			MemoryHandler.ReadyCheckCompleteEvent -= OnReadyCheckCompleted;
			mFramework.Update -= OnGameFrameworkUpdate;
			mClientState.Logout -= OnLogout;
			mClientState.TerritoryChanged -= OnTerritoryChanged;
			mCondition.ConditionChange -= OnConditionChanged;
			MemoryHandler.Uninit();
			mUI.Dispose();
			mPluginInterface.UiBuilder.Draw -= DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
			mPluginInterface.LanguageChanged -= OnLanguageChanged;
			mPluginInterface.RemoveChatLinkHandler();
			mCommandManager.RemoveHandler( mTextCommandName );
			mInstancedTerritories.Clear();
			mTimedOverlayCancellationSource?.Dispose();
			mTimedOverlayCancellationSource = null;
		}

		protected void OnLanguageChanged( string langCode )
		{
			//***** TODO *****
			var allowedLang = new List<string>{ /*"de", "ja", "fr", "it", "es"*/ };

			PluginLog.Information( "Trying to set up Loc for culture {0}", langCode );

			if( allowedLang.Contains( langCode ) )
			{
				Loc.Setup( File.ReadAllText( Path.Join( mPluginInterface.AssemblyLocation.FullName, $"loc_{langCode}.json" ) ) );
			}
			else
			{
				Loc.SetupWithFallbacks();
			}

			//	Set up the command handler with the current language.
			if( mCommandManager.Commands.ContainsKey( mTextCommandName ) )
			{
				mCommandManager.RemoveHandler( mTextCommandName );
			}
			mCommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = String.Format( Loc.Localize( "Plugin Text Command Description", "Use {0} to open the the configuration window." ), "\"/pready config\"" )
			} );
		}

		//	Text Commands
		protected void ProcessTextCommand( string command, string args )
		{
			//*****TODO: Don't split, just substring off of the first space so that other stuff is preserved verbatim.
			//	Seperate into sub-command and paramters.
			string subCommand = "";
			string subCommandArgs = "";
			string[] argsArray = args.Split( ' ' );
			if( argsArray.Length > 0 )
			{
				subCommand = argsArray[0];
			}
			if( argsArray.Length > 1 )
			{
				//	Recombine because there might be spaces in JSON or something, that would make splitting it bad.
				for( int i = 1; i < argsArray.Length; ++i )
				{
					subCommandArgs += argsArray[i] + ' ';
				}
				subCommandArgs = subCommandArgs.Trim();
			}

			//	Process the commands.
			bool suppressResponse = mConfiguration.SuppressCommandLineResponses;
			string commandResponse = "";
			if( subCommand.Length == 0 )
			{
				//	For now just have no subcommands act like the config subcommand
				mUI.SettingsWindowVisible = !mUI.SettingsWindowVisible;
			}
			else if( subCommand.ToLower() == "config" )
			{
				mUI.SettingsWindowVisible = !mUI.SettingsWindowVisible;
			}
			else if( subCommand.ToLower() == "debug" )
			{
				mUI.DebugWindowVisible = !mUI.DebugWindowVisible;
			}
			else if( subCommand.ToLower() == "results" )
			{
				mUI.ReadyCheckResultsWindowVisible = !mUI.ReadyCheckResultsWindowVisible;
			}
			else if( subCommand.ToLower() == "clear" )
			{
				mUI.InvalidateReadyCheck();
			}
			else if( subCommand.ToLower() == "help" || subCommand.ToLower() == "?" )
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
				suppressResponse = false;
			}
			else
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
			}

			//	Send any feedback to the user.
			if( commandResponse.Length > 0 && !suppressResponse )
			{
				mChatGui.Print( commandResponse );
			}
		}

		protected string ProcessTextCommand_Help( string args )
		{
			if( args.ToLower() == "config" )
			{
				return Loc.Localize( "Config Subcommand Help Message", "Opens the settings window." );
			}
			else if( args.ToLower() == "results" )
			{
				return Loc.Localize( "Results Subcommand Help Message", "Opens a window containing the results of the last ready check to occur." );
			}
			else if( args.ToLower() == "clear" )
			{
				return Loc.Localize( "Clear Subcommand Help Message", "Removes the most recent ready check icons from the party/alliance lists." );
			}
			else if( args.ToLower() == "debug" )
			{
				return Loc.Localize( "Debug Subcommand Help Message", "Opens a debugging window containing party and ready check object data." );
			}
			else
			{
				return String.Format( Loc.Localize( "Basic Help Message", "This plugin works automatically; however, some text commands are supported.  Valid subcommands are {0}, {1}, and {2}.  Use \"{3} <subcommand>\" for more information on each subcommand." ), "\"config\"", "\"results\"", "\"clear\"", "/pready help" );
			}
		}

		protected void DrawUI()
		{
			mUI.Draw();
		}

		protected void DrawConfigUI()
		{
			mUI.SettingsWindowVisible = true;
		}

		protected void OnGameFrameworkUpdate( Framework framework )
		{
			if( mClientState.IsLoggedIn && ReadyCheckActive ) ProcessReadyCheckResults();
		}

		protected void OnReadyCheckInitiated( object sender, System.EventArgs e )
		{
			//	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
			if( !mClientState.IsLoggedIn ) return;

			//	Flag that we should start processing the data every frame.
			ReadyCheckActive = true;
			mUI.ShowReadyCheckOverlay();
			mTimedOverlayCancellationSource?.Cancel();
		}

		protected void OnReadyCheckCompleted( object sender, System.EventArgs e )
		{
			//	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
			if( !mClientState.IsLoggedIn ) return;

			//	Flag that we don't need to keep updating.
			ReadyCheckActive = false;
			mUI.ShowReadyCheckOverlay();

			//	Process the data one last time to ensure that we have the latest results.
			ProcessReadyCheckResults();

			//	Construct a list of who's not ready.
			var notReadyList = new List<String>();

			lock( mProcessedReadyCheckDataLockObj )
			{
				foreach( var person in mProcessedReadyCheckData )
				{
					if( person.ReadyState == MemoryHandler.ReadyCheckStateEnum.NotReady ||
						person.ReadyState == MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
					{
						notReadyList.Add( person.Name );
					}
				}
			}

			//	Print it to chat in the desired format.
			ListUnreadyPlayersInChat( notReadyList );

			//	Start a task to clean up the icons on the party chat after the configured amount of time.
			if( mConfiguration.ClearReadyCheckOverlayAfterTime )
			{
				mTimedOverlayCancellationSource = new CancellationTokenSource();
				Task.Run( async () =>
				{
					int delay_Sec = Math.Max( 0, Math.Min( mConfiguration.TimeUntilClearReadyCheckOverlay_Sec, 900 ) ); //	Just to be safe...

					try
					{
						await Task.Delay( delay_Sec * 1000, mTimedOverlayCancellationSource.Token );
					}
					catch( OperationCanceledException )
					{
						return;
					}
					finally
					{
						mTimedOverlayCancellationSource?.Dispose();
						mTimedOverlayCancellationSource = null;
					}

					if( !ReadyCheckActive ) mUI.InvalidateReadyCheck();
				} );
			}
		}

		unsafe protected void ProcessReadyCheckResults()
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() != IntPtr.Zero )
			{
				if( (IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() != IntPtr.Zero )
				{
					//	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
					//	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
					if( FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm > 0 &&
					FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount < 1 )
					{
						ProcessReadyCheckResults_CrossWorld();
					}
					else
					{
						ProcessReadyCheckResults_Regular();
					}
				}
			}
		}

		unsafe protected void ProcessReadyCheckResults_Regular()
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();
					bool foundSelf = false;

					//	Grab all of the alliance members here to make lookups easier since there's no function in client structs to get an alliance member by object ID.
					Dictionary<UInt32, Tuple<UInt64, string, byte, byte>> allianceMemberDict = new Dictionary<UInt32, Tuple<UInt64, string, byte, byte>>();
					for( int j = 0; j < 2; ++j )
					{
						for( int i = 0; i < 8; ++i )
						{
							var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetAllianceMemberByGroupAndIndex( j, i );
							if( (IntPtr)pGroupMember != IntPtr.Zero )
							{
								string name = System.Text.Encoding.UTF8.GetString( pGroupMember->Name, 64 );    //***** TODO: How to get fixed buffer lenghth instead of magic numbering it here? *****
								name = name.Substring( 0, name.IndexOf( '\0' ) );
								allianceMemberDict.TryAdd( pGroupMember->ObjectID, Tuple.Create( (UInt64)pGroupMember->ContentID, name, (byte)( j + 1 ), (byte)i ) );
							}
						}
					}

					//	Correlate all of the ready check entries with party/alliance members.
					for( int i = 0; i < readyCheckData.Length; ++i )
					{
						//	For our party, we need to do the correlation based on party data.
						if( i < FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount )
						{
							//	For your immediate, local party, ready check data seems to be correlated with the party index, but with you always first in the list (anyone with an index below yours will be offset by one).
							var pFoundPartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetPartyMemberByIndex( i );
							if( (IntPtr)pFoundPartyMember != IntPtr.Zero )
							{
								string name = System.Text.Encoding.UTF8.GetString( pFoundPartyMember->Name, 64 );    //***** TODO: Magic Number *****
								name = name.Substring( 0, name.IndexOf( '\0' ) );
								//	If it's us, we need to use the first entry in the ready check data.
								if( pFoundPartyMember->ObjectID == mClientState.LocalPlayer?.ObjectId )
								{
									readyCheckProcessedList.Insert( 0, new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[0].ReadyFlag, 0, 0 ) );
									foundSelf = true;
								}
								//	If it's before we've found ourselves, look ahead by one in the ready check data.
								else if( !foundSelf )
								{
									readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[i+1].ReadyFlag, 0, (byte)(i + 1) ) );
								}
								//	Otherwise, use the same index in the ready check data.
								else
								{
									readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[i].ReadyFlag, 0, (byte)i ) );
								}
							}
						}
						//	For the alliance members, there should be object IDs to make matching easy.
						else if( readyCheckData[i].ID > 0 && (readyCheckData[i].ID & 0xFFFFFFFF) != 0xE0000000 )
						{
							Tuple<UInt64, string, byte, byte> temp = null;
							if( allianceMemberDict.TryGetValue( (uint)readyCheckData[i].ID, out temp ) )
							{
								readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( temp.Item2, temp.Item1, (UInt32)readyCheckData[i].ID, readyCheckData[i].ReadyFlag, temp.Item3, temp.Item4 ) );
							}
						}
						//***** TODO: How do things work if you're a non-cross-world alliance without people in the same zone? *****
						//This isn't possible through PF; is it still possible in the open world?
					}

					//	Assign to the persistent list if we've gotten through this without any problems.
					lock( mProcessedReadyCheckDataLockObj )
					{
						mProcessedReadyCheckData = readyCheckProcessedList;
					}
				}
				catch( Exception e )
				{
					PluginLog.LogDebug( $"Exception caught in \"ProcessReadyCheckResults_Regular()\": {e}." );
				}
			}
		}

		unsafe protected void ProcessReadyCheckResults_CrossWorld()
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();

					foreach( var readyCheckEntry in readyCheckData )
					{
						var pFoundPartyMember = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetMemberByContentId( readyCheckEntry.ID );
						if( (IntPtr)pFoundPartyMember != IntPtr.Zero )
						{
							string name = System.Text.Encoding.UTF8.GetString( pFoundPartyMember->Name, 30 );   //***** TODO: Magic Number *****
							name = name.Substring( 0, name.IndexOf( '\0' ) );
							readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, pFoundPartyMember->ContentId, pFoundPartyMember->ObjectId, readyCheckEntry.ReadyFlag, pFoundPartyMember->GroupIndex, pFoundPartyMember->MemberIndex ) );
						}
					}

					//	Assign to the persistent list if we've gotten through this without any problems.
					lock( mProcessedReadyCheckDataLockObj )
					{
						mProcessedReadyCheckData = readyCheckProcessedList;
					}
				}
				catch( Exception e )
				{
					PluginLog.LogDebug( $"Exception caught in \"ProcessReadyCheckResults_CrossWorld()\": {e}." );
				}
			}
		}

		protected void ListUnreadyPlayersInChat( List<String> notReadyList )
		{
			if( notReadyList.Count > 0 )
			{
				//***** TODO: Localizing this part will probably just require a separate function for each of the four game languages due to grammar rules. *****
				string notReadyString = "Not Ready: ";
				for( int i = 0; i < notReadyList.Count; ++i )
				{
					//	If there's only one person, just put their name.
					if( notReadyList.Count == 1 )
					{
						notReadyString += notReadyList[i];
					}
					//	Once we've reached the max configured number of individual names to show.
					else if( i >= mConfiguration.MaxUnreadyToListInChat )
					{
						notReadyString += $" and {notReadyList.Count - i} {(notReadyList.Count - i > 1 ? "others" : "other")}";
						break;
					}
					//	Grammar for showing the final name in a list.
					else if( i == notReadyList.Count - 1 )
					{
						notReadyString += $" and {notReadyList[i]}";
					}
					//	Grammar for the first item if there will only be two items listed.
					else if( i == 0 && ( notReadyList.Count == 2 || mConfiguration.MaxUnreadyToListInChat < 2 ) )
					{
						notReadyString += notReadyList[i];
					}
					//	Otherwise comma separate the list.
					else
					{
						notReadyString += notReadyList[i] + ", ";
					}
				}

				//	If we don't delay the actual printing to chat, sometimes it comes out before the system message in the chat log.  I don't understand why it's an issue, but this is an easy kludge to make it work right consistently.
				Task.Run( async () =>
				{
					await Task.Delay( 500 );    //***** TODO: Make this value configurable, or fix the underlying issue. *****
					var chatEntry = new Dalamud.Game.Text.XivChatEntry
					{
						Type = Dalamud.Game.Text.XivChatType.SystemMessage,
						Message = new Dalamud.Game.Text.SeStringHandling.SeString( new List<Dalamud.Game.Text.SeStringHandling.Payload>
						{
							//Dalamud.Game.Text.SeStringHandling.SeString.TextArrowPayloads,
							mOpenReadyCheckWindowLink,
							new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload( notReadyString ),
							Dalamud.Game.Text.SeStringHandling.Payloads.RawPayload.LinkTerminator
						} )
					};
					mChatGui.PrintChat( chatEntry );
				} );
			}
		}

		protected void ShowBestAvailableReadyCheckWindow()
		{
			unsafe
			{
				var pReadyCheckNotification = (AtkUnitBase*)mGameGui.GetAddonByName( "_NotificationReadyCheck", 1 );
				if( false /*(IntPtr)pReadyCheckNotification != IntPtr.Zero && pReadyCheckNotification->IsVisible*/ )
				{
					//***** TODO: Try to show built in ready check window.  The addon doesn't exist unless it's opened, so this might be difficult. *****
				}
				else
				{
					mUI.ReadyCheckResultsWindowVisible = true;
				}
			}
		}

		protected void OnConditionChanged( ConditionFlag flag, bool value )
		{
			if( flag == ConditionFlag.InCombat )
			{
				if( value )
				{
					if( mConfiguration.ClearReadyCheckOverlayInCombat )
					{
						mUI.InvalidateReadyCheck();
					}
					else if( mConfiguration.ClearReadyCheckOverlayInCombatInInstancedCombat && mInstancedTerritories.Contains( mClientState.TerritoryType ) )
					{
						mUI.InvalidateReadyCheck();
					}
				}
			}
		}

		protected void OnTerritoryChanged( object sender, UInt16 ID )
		{
			if( mConfiguration.ClearReadyCheckOverlayEnteringInstance && mInstancedTerritories.Contains( ID ) ) mUI.InvalidateReadyCheck();
		}

		protected void OnLogout( object sender, System.EventArgs e )
		{
			ReadyCheckActive = false;
			mTimedOverlayCancellationSource?.Cancel();
			mUI.InvalidateReadyCheck();
			mProcessedReadyCheckData = null;
		}

		public List<CorrelatedReadyCheckEntry> GetProcessedReadyCheckData()
		{
			lock( mProcessedReadyCheckDataLockObj )
			{
				return mProcessedReadyCheckData != null ? new List<CorrelatedReadyCheckEntry>( mProcessedReadyCheckData ) : null;
			}
		}

		protected void PopulateInstancedTerritoriesList()
		{
			ExcelSheet<ContentFinderCondition> contentFinderSheet = mDataManager.GetExcelSheet<ContentFinderCondition>();
			foreach( var zone in contentFinderSheet ) mInstancedTerritories.Add( zone.TerritoryType.Row );
		}

		public string Name => "ReadyCheckHelper";
		protected const string mTextCommandName = "/pready";
		private readonly Dalamud.Game.Text.SeStringHandling.Payloads.DalamudLinkPayload mOpenReadyCheckWindowLink;

		protected List<UInt32> mInstancedTerritories = new List<UInt32>();
		protected List<CorrelatedReadyCheckEntry> mProcessedReadyCheckData;
		protected Object mProcessedReadyCheckDataLockObj = new object();
		protected CancellationTokenSource mTimedOverlayCancellationSource = null;
		public bool ReadyCheckActive { get; protected set; } = false;

		protected DalamudPluginInterface mPluginInterface;
		protected Framework mFramework;
		protected ClientState mClientState;
		protected CommandManager mCommandManager;
		protected Dalamud.Game.ClientState.Conditions.Condition mCondition;
		protected ChatGui mChatGui;
		protected GameGui mGameGui;
		protected SigScanner mSigScanner;
		protected DataManager mDataManager;
		protected Configuration mConfiguration;
		protected PluginUI mUI;

		public struct CorrelatedReadyCheckEntry
		{
			public CorrelatedReadyCheckEntry( string name, UInt64 contentID, UInt32 objectID, MemoryHandler.ReadyCheckStateEnum readyState, byte groupIndex, byte memberIndex )
			{
				Name = name;
				ContentID = contentID;
				ObjectID = objectID;
				ReadyState = readyState;
				GroupIndex = groupIndex;
				MemberIndex = memberIndex;
			}

			public string Name { get; private set; }
			public UInt64 ContentID { get; private set; }
			public UInt32 ObjectID { get; private set; }
			public MemoryHandler.ReadyCheckStateEnum ReadyState { get; private set; }
			public byte GroupIndex { get; private set; }
			public byte MemberIndex { get; private set; }
		}
	}
}
