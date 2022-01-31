using System;
using System.Collections.Generic;
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
			DataManager dataManager,
			SigScanner sigScanner )
		{
			//	API Access
			mPluginInterface	= pluginInterface;
			mClientState		= clientState;
			mCommandManager		= commandManager;
			mCondition			= condition;
			mChatGui			= chatGui;
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
				HelpMessage = "Testing plugin.  See code for commands."
			} );

			//	UI Initialization
			mUI = new PluginUI( mConfiguration, mDataManager );
			mPluginInterface.UiBuilder.Draw += DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
			mUI.SetCurrentTerritoryTypeID( mClientState.TerritoryType );
			mUI.Initialize();

			//	Event Subscription
			mClientState.TerritoryChanged += OnTerritoryChanged;
			MemoryHandler.ReadyCheckCompleteEvent += ProcessReadyCheckResults;
		}

		//	Cleanup
		public void Dispose()
		{
			MemoryHandler.ReadyCheckCompleteEvent -= ProcessReadyCheckResults;
			MemoryHandler.Uninit();
			mUI.Dispose();
			mClientState.TerritoryChanged -= OnTerritoryChanged;
			mPluginInterface.UiBuilder.Draw -= DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
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
				mUI.MainWindowVisible = !mUI.MainWindowVisible;
			}
			else if( subCommand.ToLower() == "config" )
			{
				mUI.SettingsWindowVisible = !mUI.SettingsWindowVisible;
			}
			else if( subCommand.ToLower() == "debug" )
			{
				mUI.DebugWindowVisible = !mUI.DebugWindowVisible;
			}
/*			else if( subCommand.ToLower() == "place" )
			{
				commandResponse = ProcessTextCommand_Place( subCommandArgs );
			}
			else if( subCommand.ToLower() == "import" )
			{
				commandResponse = ProcessTextCommand_Import( subCommandArgs );
			}
			else if( subCommand.ToLower() == "export" )
			{
				commandResponse = ProcessTextCommand_Export( subCommandArgs );
			}
			else if( subCommand.ToLower() == "exportall" )
			{
				commandResponse = ProcessTextCommand_ExportAll( subCommandArgs );
			}
			else if( subCommand.ToLower() == "help" || subCommand.ToLower() == "?" )
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
				suppressResponse = false;
			}*/
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
			return "See code for valid commands.";
		}

		protected void DrawUI()
		{
			mUI.Draw();
		}

		protected void DrawConfigUI()
		{
			mUI.SettingsWindowVisible = true;
		}

		protected void OnTerritoryChanged( object sender, UInt16 ID )
		{
			CurrentTerritoryTypeID = ID;
			mUI.SetCurrentTerritoryTypeID( ID );
		}

		unsafe protected void ProcessReadyCheckResults( object sender, System.EventArgs e )
		{
			if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() != IntPtr.Zero )
			{
				if( FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm > 0 )
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
					var readyCheckProcessedList = new List<Tuple<String, Byte>>();

					for( int i = 0; i < readyCheckData.Length; ++i )
					{
						//	For our party, we need to do the correlation based on party data.
						if( i < FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount )
						{
							//***** TODO: Do something, but it's really difficult to tell how the ready check data is correlated for your immediate local party. *****
						}
						//	For the alliance members (anything above our current party), there should be object IDs to make matching easy.
						else if( readyCheckData[i].ID > 0 && ( readyCheckData[i].ID & 0xFFFFFFFF ) != 0xE0000000 )
						{
							var pFoundAllianceMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetPartyMemberByObjectId( (uint)readyCheckData[i].ID );
							if( (IntPtr)pFoundAllianceMember != IntPtr.Zero )
							{
								string name = System.Text.Encoding.UTF8.GetString( pFoundAllianceMember->Name, 64 );	//***** TODO: Magic Number *****
								name = name.Substring( 0, name.IndexOf( '\0' ) );
								readyCheckProcessedList.Add( Tuple.Create( name, readyCheckData[i].ReadyFlag ) );
							}
						}
						//***** TODO: How do things work if you're a non-cross-world alliance without people in the same zone? *****
					}

					var notReadyList = new List<String>();

					foreach( var person in readyCheckProcessedList )
					{
						if( person.Item2 == 3 )
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
					var readyCheckProcessedList = new List<Tuple<String, Byte>>();

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
						if( person.Item2 == 3 )
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
					notReadyString += notReadyList[i]+ ", ";
				}
			}

			//***** TODO: We should delay actually printing it to chat by a short amount to ensure that it comes after the game's built in message. *****
			if( notReadyList.Count > 0 )
			{
				mChatGui.Print( notReadyString );
			}
		}

		public string Name => "ReadyCheckHelper";
		protected const string mTextCommandName = "/pready";

		public UInt16 CurrentTerritoryTypeID { get; protected set; }

		protected DalamudPluginInterface mPluginInterface;
		protected ClientState mClientState;
		protected CommandManager mCommandManager;
		protected Condition mCondition;
		protected ChatGui mChatGui;
		protected SigScanner mSigScanner;
		protected DataManager mDataManager;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
	}
}
