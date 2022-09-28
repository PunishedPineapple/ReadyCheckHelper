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
using Dalamud.Memory;
using Dalamud.Logging;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CheapLoc;

namespace ReadyCheckHelper
{
	public sealed class Plugin : IDalamudPlugin
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
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration;
			if( mConfiguration == null )
			{
				mConfiguration = new Configuration();
				mConfiguration.mUseImGuiForPartyAllianceIcons = false;	//	We want to default this to off for all new configurations.
			}
			mConfiguration.Initialize( mPluginInterface );
			MemoryHandler.Init( mSigScanner );

			//	Localization and Command Initialization
			OnLanguageChanged( mPluginInterface.UiLanguage );
			mOpenReadyCheckWindowLink = mPluginInterface.AddChatLinkHandler( 1001, ( i, m ) =>
			{
				ShowBestAvailableReadyCheckWindow();
			} );
			LocalizationHelpers.Init( dataManager );

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
			mPluginInterface.UiBuilder.Draw -= DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
			mPluginInterface.LanguageChanged -= OnLanguageChanged;
			mPluginInterface.RemoveChatLinkHandler();
			mCommandManager.RemoveHandler( mTextCommandName );
			mUI?.Dispose();
			mInstancedTerritories.Clear();
			LocalizationHelpers.Uninit();
			mTimedOverlayCancellationSource?.Dispose();
			mTimedOverlayCancellationSource = null;
		}

		private void OnLanguageChanged( string langCode )
		{
			var allowedLang = new List<string>{ "es", "fr", "ja" };

			PluginLog.Information( "Trying to set up Loc for culture {0}", langCode );

			if( allowedLang.Contains( langCode ) )
			{
				Loc.Setup( File.ReadAllText( Path.Join( Path.Join( mPluginInterface.AssemblyLocation.DirectoryName, "Resources\\Localization\\" ), $"loc_{langCode}.json" ) ) );
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
		private void ProcessTextCommand( string command, string args )
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

		private string ProcessTextCommand_Help( string args )
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

		private void DrawUI()
		{
			mUI.Draw();
		}

		private void DrawConfigUI()
		{
			mUI.SettingsWindowVisible = true;
		}

		private void OnGameFrameworkUpdate( Framework framework )
		{
			if( mClientState.IsLoggedIn && ReadyCheckActive ) ProcessReadyCheckResults();
		}

		private void OnReadyCheckInitiated( object sender, System.EventArgs e )
		{
			//	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
			if( !mClientState.IsLoggedIn ) return;

			//	Flag that we should start processing the data every frame.
			ReadyCheckActive = true;
			mUI.ShowReadyCheckOverlay();
			mTimedOverlayCancellationSource?.Cancel();
		}

		private void OnReadyCheckCompleted( object sender, System.EventArgs e )
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
					if( person.ReadyState == ReadyCheckState.NotReady ||
						person.ReadyState == ReadyCheckState.CrossWorldMemberNotPresent )
					{
						notReadyList.Add( person.Name );
					}
				}
			}

			//	Print it to chat in the desired format.
			if( mConfiguration.ShowReadyCheckResultsInChat )
			{
				ListUnreadyPlayersInChat( notReadyList );
			}

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

		private unsafe void ProcessReadyCheckResults()
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

		private unsafe void ProcessReadyCheckResults_Regular()
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();
					bool foundSelf = false;

					//	Grab all of the alliance members here to make lookups easier since there's no function in client structs to get an alliance member by object ID.
					var allianceMemberDict = new Dictionary<UInt32, Tuple<UInt64, string, byte, byte>>();
					for( int j = 0; j < 2; ++j )
					{
						for( int i = 0; i < 8; ++i )
						{
							var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetAllianceMemberByGroupAndIndex( j, i );
							if( (IntPtr)pGroupMember != IntPtr.Zero )
							{
								string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pGroupMember->Name ).ToString();
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
								string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pFoundPartyMember->Name ).ToString();

								//	If it's us, we need to use the first entry in the ready check data.
								if( pFoundPartyMember->ObjectID == mClientState.LocalPlayer?.ObjectId )
								{
									readyCheckProcessedList.Insert( 0, new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[0].ReadyFlag, 0, 0 ) );
									foundSelf = true;
								}
								//	If it's before we've found ourselves, look ahead by one in the ready check data.
								else if( !foundSelf )
								{
									readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[i + 1].ReadyFlag, 0, (byte)( i + 1 ) ) );
								}
								//	Otherwise, use the same index in the ready check data.
								else
								{
									readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[i].ReadyFlag, 0, (byte)i ) );
								}
							}
						}
						//	For the alliance members, there should be object IDs to make matching easy.
						else if( readyCheckData[i].ID > 0 && ( readyCheckData[i].ID & 0xFFFFFFFF ) != 0xE0000000 )
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

		private unsafe void ProcessReadyCheckResults_CrossWorld()
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
							string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pFoundPartyMember->Name ).ToString();
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

		public void ListUnreadyPlayersInChat( List<String> notReadyList )
		{
			if( notReadyList.Count > 0 )
			{
				//	Getting this from separate functions instead of just a localized string, since list construction may follow different rules in different languages.
				string notReadyString = "";
				switch( mClientState.ClientLanguage )
				{
					case Dalamud.ClientLanguage.Japanese:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_ja( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;
					case Dalamud.ClientLanguage.English:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_en( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;
					case Dalamud.ClientLanguage.German:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_de( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;
					case Dalamud.ClientLanguage.French:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_fr( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;
					/*case Dalamud.ClientLanguage.Korean:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_ko( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;
					case Dalamud.ClientLanguage.Chinese:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_zh( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;*/
					default:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_en( notReadyList, mConfiguration.MaxUnreadyToListInChat );
						break;
				}

				//	If we don't delay the actual printing to chat, sometimes it comes out before the system message in the chat log.  I don't understand why it's an issue, but this is an easy kludge to make it work right consistently.
				Task.Run( async () =>
				{
					await Task.Delay( 500 );    //***** TODO: Make this value configurable, or fix the underlying issue. *****
					var chatEntry = new Dalamud.Game.Text.XivChatEntry
					{
						Type = mConfiguration.ChatChannelToUseForNotReadyMessage,
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

		private void ShowBestAvailableReadyCheckWindow()
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

		private void OnConditionChanged( ConditionFlag flag, bool value )
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

		private void OnTerritoryChanged( object sender, UInt16 ID )
		{
			if( mConfiguration.ClearReadyCheckOverlayEnteringInstance && mInstancedTerritories.Contains( ID ) ) mUI.InvalidateReadyCheck();
		}

		private void OnLogout( object sender, System.EventArgs e )
		{
			ReadyCheckActive = false;
			mTimedOverlayCancellationSource?.Cancel();
			mUI.InvalidateReadyCheck();
			mProcessedReadyCheckData = null;
		}

		internal List<CorrelatedReadyCheckEntry> GetProcessedReadyCheckData()
		{
			lock( mProcessedReadyCheckDataLockObj )
			{
				return mProcessedReadyCheckData != null ? new List<CorrelatedReadyCheckEntry>( mProcessedReadyCheckData ) : null;
			}
		}

		private void PopulateInstancedTerritoriesList()
		{
			ExcelSheet<ContentFinderCondition> contentFinderSheet = mDataManager.GetExcelSheet<ContentFinderCondition>();
			foreach( var zone in contentFinderSheet ) mInstancedTerritories.Add( zone.TerritoryType.Row );
		}

		public string Name => "Ready Check Helper";
		private const string mTextCommandName = "/pready";
		private readonly Dalamud.Game.Text.SeStringHandling.Payloads.DalamudLinkPayload mOpenReadyCheckWindowLink;

		private List<UInt32> mInstancedTerritories = new();
		private List<CorrelatedReadyCheckEntry> mProcessedReadyCheckData;
		private Object mProcessedReadyCheckDataLockObj = new();
		private CancellationTokenSource mTimedOverlayCancellationSource = null;
		public bool ReadyCheckActive { get; private set; } = false;

		private DalamudPluginInterface mPluginInterface;
		private Framework mFramework;
		private ClientState mClientState;
		private CommandManager mCommandManager;
		private Dalamud.Game.ClientState.Conditions.Condition mCondition;
		private ChatGui mChatGui;
		private GameGui mGameGui;
		private SigScanner mSigScanner;
		private DataManager mDataManager;
		private Configuration mConfiguration;
		private PluginUI mUI;
	}
}
