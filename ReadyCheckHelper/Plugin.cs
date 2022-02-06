using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dalamud.Plugin;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game;
using Dalamud.Data;
using Dalamud.Logging;

namespace ReadyCheckHelper
{
	public class Plugin : IDalamudPlugin
	{
		//	Initialization
		public Plugin(
			DalamudPluginInterface pluginInterface,
			ClientState clientState,
			CommandManager commandManager,
			Condition condition,
			ChatGui chatGui,
			GameGui gameGui,
			DataManager dataManager,
			SigScanner sigScanner )
		{
			//	API Access
			mPluginInterface	= pluginInterface;
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

			//	Text Command Initialization
			mCommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Use \"/pready config\" to open the the configuration window."
			} );
			mOpenReadyCheckWindowLink = mPluginInterface.AddChatLinkHandler( 1001, ( i, m ) =>
			{
				ShowBestAvailableReadyCheckWindow();
			} );

			//	UI Initialization
			mUI = new PluginUI( mPluginInterface, mConfiguration, mDataManager, mGameGui );
			mPluginInterface.UiBuilder.Draw += DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
			//mUI.SetCurrentTerritoryTypeID( mClientState.TerritoryType );
			mUI.Initialize();

			//	Event Subscription
			MemoryHandler.ReadyCheckCompleteEvent += ProcessReadyCheckResults;
		}

		//	Cleanup
		public void Dispose()
		{
			MemoryHandler.ReadyCheckCompleteEvent -= ProcessReadyCheckResults;
			MemoryHandler.Uninit();
			mUI.Dispose();
			mPluginInterface.UiBuilder.Draw -= DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
			mPluginInterface.RemoveChatLinkHandler();
			mCommandManager.RemoveHandler( mTextCommandName );
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
				return "Opens the settings window.";
			}
			else if( args.ToLower() == "debug" )
			{
				return "Opens a window containing party and ready check object data.";
			}
			else
			{
				return "Use \"/pready config\" to open the the configuration window.";
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

		unsafe protected void ProcessReadyCheckResults( object sender, System.EventArgs e )
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
				else
				{
					PluginLog.LogError( "Error in \"ProcessReadyCheckResults()\": The GroupManager instance pointer was null." );
				}
			}
			else
			{
				PluginLog.LogError( "Error in \"ProcessReadyCheckResults()\": The InfoProxyCrossRealm instance pointer was null." );
			}
		}

		unsafe protected void ProcessReadyCheckResults_Regular()
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<Tuple<String, MemoryHandler.ReadyCheckStateEnum>>();
					bool foundSelf = false;

					//	Grab all of the alliance members here to make lookups easier since there's no function in client structs to get an alliance member by object ID.
					Dictionary<UInt32, String> allianceMemberDict = new Dictionary<UInt32, string>();
					for( int i = 0; i < 16; ++i )
					{
						var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetAllianceMemberByIndex( i );
						if( (IntPtr)pGroupMember != IntPtr.Zero )
						{
							string name = System.Text.Encoding.UTF8.GetString( pGroupMember->Name, 64 );    //***** TODO: How to get fixed buffer lenghth instead of magic numbering it here? *****
							name = name.Substring( 0, name.IndexOf( '\0' ) );
							allianceMemberDict.TryAdd( pGroupMember->ObjectID, name );
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
									readyCheckProcessedList.Insert( 0, Tuple.Create( name, readyCheckData[0].ReadyFlag ) );
									foundSelf = true;
								}
								//	If it's before we've found ourselves, look ahead by one in the ready check data.
								else if( !foundSelf )
								{
									readyCheckProcessedList.Add( Tuple.Create( name, readyCheckData[i+1].ReadyFlag ) );
								}
								//	Otherwise, use the same index in the ready check data.
								else
								{
									readyCheckProcessedList.Add( Tuple.Create( name, readyCheckData[i].ReadyFlag ) );
								}
							}
						}
						//	For the alliance members, there should be object IDs to make matching easy.
						else if( readyCheckData[i].ID > 0 && (readyCheckData[i].ID & 0xFFFFFFFF) != 0xE0000000 )
						{
							string name = "";
							if( allianceMemberDict.TryGetValue( (uint)readyCheckData[i].ID, out name ) )
							{
								readyCheckProcessedList.Add( Tuple.Create( name, readyCheckData[i].ReadyFlag ) );
							}
						}
						//***** TODO: How do things work if you're a non-cross-world alliance without people in the same zone? *****
						//This isn't possible through PF; is it still possible in the open world?
					}

					var notReadyList = new List<String>();

					foreach( var person in readyCheckProcessedList )
					{
						if( person.Item2 == MemoryHandler.ReadyCheckStateEnum.NotReady )
						{
							notReadyList.Add( person.Item1 );
						}
					}

					ListUnreadyPlayersInChat( notReadyList );
				}
				catch( Exception e )
				{
					PluginLog.LogError( $"Exception caught in \"ProcessReadyCheckResults_Regular()\": {e}." );
				}
			}
			else
			{
				PluginLog.LogError( "Error in \"ProcessReadyCheckResults_Regular()\": The GroupManager instance pointer was null." );
			}
		}

		unsafe protected void ProcessReadyCheckResults_CrossWorld()
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<Tuple<String, MemoryHandler.ReadyCheckStateEnum>>();

					foreach( var readyCheckEntry in readyCheckData )
					{
						var pFoundPartyMember = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetMemberByContentId( readyCheckEntry.ID );
						if( (IntPtr)pFoundPartyMember != IntPtr.Zero )
						{
							string name = System.Text.Encoding.UTF8.GetString( pFoundPartyMember->Name, 30 );   //***** TODO: Magic Number *****
							name = name.Substring( 0, name.IndexOf( '\0' ) );
							readyCheckProcessedList.Add( Tuple.Create( name, readyCheckEntry.ReadyFlag ) );
						}
					}

					var notReadyList = new List<String>();

					foreach( var person in readyCheckProcessedList )
					{
						if( person.Item2 == MemoryHandler.ReadyCheckStateEnum.NotReady )
						{
							notReadyList.Add( person.Item1 );
						}
					}

					ListUnreadyPlayersInChat( notReadyList );
				}
				catch( Exception e )
				{
					PluginLog.LogError( $"Exception caught in \"ProcessReadyCheckResults_CrossWorld()\": {e}." );
				}
			}
			else
			{
				PluginLog.LogError( "Error in \"ProcessReadyCheckResults_CrossWorld()\": The InfoProxyCrossRealm instance pointer was null." );
			}
		}

		protected void ListUnreadyPlayersInChat( List<String> notReadyList )
		{
			if( notReadyList.Count > 0 )
			{
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
						notReadyString += $" and {notReadyList.Count - i} other{(notReadyList.Count - i > 1 ? "s" : "")}";
						break;
					}
					//	Grammar for showing the final name in a list.
					else if( i == notReadyList.Count - 1 )
					{
						notReadyString += " and " + notReadyList[i];
					}
					//	Grammar for the first item if there will only be two items listed.
					else if( i == 0 && (notReadyList.Count == 2 || mConfiguration.MaxUnreadyToListInChat < 2) )
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
			//***** TODO: Show built in ready check window if it's still available; otherwise show a reconstruction through ImGui. *****
			mChatGui.Print( "TODO: Handle link." );
		}

		public string Name => "ReadyCheckHelper";
		protected const string mTextCommandName = "/pready";
		private readonly Dalamud.Game.Text.SeStringHandling.Payloads.DalamudLinkPayload mOpenReadyCheckWindowLink;

		protected DalamudPluginInterface mPluginInterface;
		protected ClientState mClientState;
		protected CommandManager mCommandManager;
		protected Condition mCondition;
		protected ChatGui mChatGui;
		protected GameGui mGameGui;
		protected SigScanner mSigScanner;
		protected DataManager mDataManager;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
	}
}
