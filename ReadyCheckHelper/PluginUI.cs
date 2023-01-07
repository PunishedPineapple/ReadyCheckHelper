using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

using ImGuiNET;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Game;
using Dalamud.Memory;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using CheapLoc;

namespace ReadyCheckHelper
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Plugin plugin, DalamudPluginInterface pluginInterface, Configuration configuration, DataManager dataManager, GameGui gameGui, SigScanner sigScanner )
		{
			mPlugin = plugin;
			mPluginInterface = pluginInterface;
			mConfiguration = configuration;
			mDataManager = dataManager;
			mGameGui = gameGui;
		}

		//	Destruction
		public void Dispose()
		{
			ClearPartyAllianceListIcons();

			mReadyCheckIconTexture?.Dispose();
			mUnknownStatusIconTexture?.Dispose();
			mNotPresentIconTexture?.Dispose();
			mReadyCheckIconTexture = null;
			mUnknownStatusIconTexture = null;
			mNotPresentIconTexture = null;
		}

		public void Initialize()
		{
			ExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob> classJobSheet = mDataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>();
			foreach( ClassJob job in classJobSheet.ToList() )
			{
				JobDict.Add( job.RowId, job.Abbreviation );
			}

			mReadyCheckIconTexture		??= mDataManager.GetImGuiTexture( "ui/uld/ReadyCheck_hr1.tex" ) ?? mDataManager.GetImGuiTexture( "ui/uld/ReadyCheck.tex" );
			mUnknownStatusIconTexture	??= mDataManager.GetImGuiTextureIcon( 60072 );
			mNotPresentIconTexture		??= mDataManager.GetImGuiTextureIcon( 61504 );
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawSettingsWindow();
			DrawReadyCheckResultsWindow();
			DrawDebugWindow();
			DrawDebugRawWindow();
			DrawDebugProcessedWindow();

			//	Draw the icons on the HUD.
			if( mConfiguration.UseImGuiForPartyAllianceIcons ) DrawOnPartyAllianceLists_ImGui();
			else UpdatePartyAllianceListIconNodes();
		}

		protected void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			if( ImGui.Begin( Loc.Localize( "Window Title: Config", "Ready Check Helper Settings" ) + "###Ready Check Helper Settings",
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Checkbox( Loc.Localize( "Config Option: Print Names of Unready in Chat", "Show the names of those not ready in the chat window." ) + "###List unready names in chat.", ref mConfiguration.mShowReadyCheckResultsInChat );

				if( mConfiguration.ShowReadyCheckResultsInChat )
				{
					ImGui.Spacing();

					ImGui.Indent();
					ImGui.Text( Loc.Localize( "Config Option: Max Names in Chat", "Maximum number of names to show in chat:" ) );
					ImGui.SliderInt( "##MaxUnreadyNamesToShowInChat", ref mConfiguration.mMaxUnreadyToListInChat, 1, 48 );
					ImGui.Spacing();
					ImGui.Text( Loc.Localize( "Config Option: Chat Message Channel", "Chat Log Channel:" ) );
					ImGuiHelpMarker( String.Format( Loc.Localize( "Help: Chat Message Channel", "Sets the channel in which this chat message is shown.  Leave this set to the default value ({0}) unless it causes problems with your chat configuration.  This only affects the unready players message; all other messages from this plugin respect your choice of chat channel in Dalamud settings." ), LocalizationHelpers.GetTranslatedChatChannelName( Dalamud.Game.Text.XivChatType.SystemMessage ) ) );
					if( ImGui.BeginCombo( "###NotReadyMessageChatChannelDropdown", LocalizationHelpers.GetTranslatedChatChannelName( mConfiguration.ChatChannelToUseForNotReadyMessage ) ) )
					{
						foreach( Dalamud.Game.Text.XivChatType entry in Enum.GetValues( typeof( Dalamud.Game.Text.XivChatType ) ) )
						{
							if( ImGui.Selectable( LocalizationHelpers.GetTranslatedChatChannelName( entry ) ) ) mConfiguration.ChatChannelToUseForNotReadyMessage = entry;
						}
						ImGui.EndCombo();
					}
					ImGui.Unindent();

					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
				}

				ImGui.Checkbox( Loc.Localize( "Config Option: Draw on Party Alliance Lists", "Draw ready check on party/alliance lists." ) + "###Draw ready check on party/alliance lists.", ref mConfiguration.mShowReadyCheckOnPartyAllianceList );

				if( mConfiguration.ShowReadyCheckOnPartyAllianceList )
				{
					ImGui.Spacing();

					ImGui.Indent();
					ImGui.Text( Loc.Localize( "Config Option: Clear Party Alliance List Settings", "Clear ready check from party/alliance lists:" ) );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List upon Entering Combat", "Upon entering combat." ) + "###Upon entering combat.", ref mConfiguration.mClearReadyCheckOverlayInCombat );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List upon Entering Instance", "Upon entering instance." ) + "###Upon entering instance.", ref mConfiguration.mClearReadyCheckOverlayEnteringInstance );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List upon Enteringing Combat in Instance", "Upon entering combat while in instance." ) + "###Upon entering combat while in instance.", ref mConfiguration.mClearReadyCheckOverlayInCombatInInstancedCombat );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List after X Seconds:", "After a certain number of seconds:" ) + "###After X seconds.", ref mConfiguration.mClearReadyCheckOverlayAfterTime );
					ImGuiHelpMarker( Loc.Localize( "Help: Clear Party Alliance List after X Seconds", "Changes to this setting will not take effect until the next ready check concludes." ) );
					ImGui.DragInt( "###TimeUntilClearOverlaySlider", ref mConfiguration.mTimeUntilClearReadyCheckOverlay_Sec, 1.0f, 30, 900, "%d", ImGuiSliderFlags.AlwaysClamp );
					ImGui.Spacing();
					ImGui.Text( Loc.Localize( "Config Section: Icon Size/Offset", "Party and Alliance List Icon Size/Offset:" ) );
					ImGui.DragFloat2( Loc.Localize( "Config Option: Party List Icon Offset", "Party List Icon Offset" ) + "###PartyListIconOffset", ref mConfiguration.mPartyListIconOffset, 1f, -100f, 100f );
					ImGui.DragFloat( Loc.Localize( "Config Option: Party List Icon Scale", "Party List Icon Scale" ) + "###PartyListIconScale", ref mConfiguration.mPartyListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp );
					ImGui.DragFloat2( Loc.Localize( "Config Option: Alliance List Icon Offset", "Alliance List Icon Offset" ) + "###AllianceListIconOffset", ref mConfiguration.mAllianceListIconOffset, 1f, -100f, 100f );
					ImGui.DragFloat( Loc.Localize( "Config Option: Alliance List Icon Scale", "Alliance List Icon Scale" ) + "###AllianceListIconScale", ref mConfiguration.mAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp );
					//ImGui.DragFloat2( Loc.Localize( "Config Option: Cross-World Alliance List Icon Offset", "Cross-World Alliance List Icon Offset" ) + "###CrossWorldAllianceListIconOffset", ref mConfiguration.mCrossWorldAllianceListIconOffset, 1f, -100f, 100f );
					//ImGui.DragFloat( Loc.Localize( "Config Option: Cross-World Alliance List Icon Scale", "Cross-World Alliance List Icon Scale" ) + "###CrossWorldAllianceListIconScale", ref mConfiguration.mCrossWorldAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%d", ImGuiSliderFlags.AlwaysClamp );
					ImGui.Checkbox( Loc.Localize( "Config Option: Use ImGui Icon Drawing", "Use alternate drawing method." ) + "###Use ImGui Icon Drawing Checkbox", ref mConfiguration.mUseImGuiForPartyAllianceIcons );
					ImGuiHelpMarker( Loc.Localize( "Help: Use ImGui Icon Drawing", "Uses an overlay for drawing ready/not ready icons instead of the game's native UI.  Leave this option disabled unless you know that you need it." ) );
					ImGui.Unindent();
				}

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				/*if( ImGui.Button( Loc.Localize( "Button: Save", "Save" ) + "###Save Button" ) )
				{
					mConfiguration.Save();
				}
				ImGui.SameLine();*/
				if( ImGui.Button( Loc.Localize( "Button: Save and Close", "Save and Close" ) + "###Save and Close" ) )
				{
					mConfiguration.Save();
					SettingsWindowVisible = false;
				}
			}

			ImGui.End();
		}

		protected void DrawReadyCheckResultsWindow()
		{
			if( !ReadyCheckResultsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSizeConstraints( new Vector2( 180, 100 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Ready Check Results", "Latest Ready Check Results" ) + "###Latest Ready Check Results", ref mReadyCheckResultsWindowVisible,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				var list = mPlugin.GetProcessedReadyCheckData();
				if( list != null )
				{
					//	We have to sort and reorganize this yet again because of how ImGui tables work ;_;
					list.Sort( ( a, b ) => a.GroupIndex.CompareTo( b.GroupIndex ) );
					var tableList = new List<List<CorrelatedReadyCheckEntry>>();
					foreach( var player in list )
					{
						if( tableList.Count <= player.GroupIndex )
						{
							tableList.Add( new List<CorrelatedReadyCheckEntry>() );
						}
						tableList[player.GroupIndex].Add( player );
					}
					
					if( ImGui.BeginTable( "###LatestReadyCheckResultsTable", tableList.Count ) )
					{
						for( int i = 0; i < 8; ++i )
						{
							ImGui.TableNextRow();
							for( int j = 0; j < tableList.Count; ++j )
							{
								ImGui.TableSetColumnIndex( j );
								if( i < tableList[j].Count )
								{
									if( tableList[j][i].ReadyState == ReadyCheckState.Ready )
									{
										ImGui.Image( mReadyCheckIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
									}
									else if( tableList[j][i].ReadyState == ReadyCheckState.NotReady )
									{
										ImGui.Image( mReadyCheckIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
									}
									else if( tableList[j][i].ReadyState == ReadyCheckState.CrossWorldMemberNotPresent )
									{
										ImGui.Image( mNotPresentIconTexture.ImGuiHandle, new Vector2( 24 ) );
									}
									else
									{
										ImGui.Image( mUnknownStatusIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.0f ), new Vector2( 1.0f ), new Vector4( 0.0f ) );
									}
									ImGui.SameLine();
									ImGui.Text( tableList[j][i].Name );
								}
								//	Probably don't need this, but tables are sometimes getting clobbered, so putting it here just in case that helps.
								else
								{
									ImGui.Text( " " );
								}
							}
						}
						ImGui.EndTable();
					}
				}
				else
				{
					ImGui.Text( Loc.Localize( "Placeholder: No Ready Check Results Exist", "No ready check has yet occurred.") );
				}

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				if( ImGui.Button( Loc.Localize( "Button: Close", "Close" ) + "###Close" ) )
				{
					ReadyCheckResultsWindowVisible = false;
				}
			}

			ImGui.End();
		}

		protected void DrawDebugWindow()
		{
			if( !DebugWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 1340, 568 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Ready Check and Alliance Debug Data", "Ready Check and Alliance Debug Data" ) + "###Ready Check and Alliance Debug Data", ref mDebugWindowVisible ) )
			{
				ImGui.PushFont( UiBuilder.MonoFont );
				try
				{
					unsafe
					{
						var pAgentHUD = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentHUD();
						if( (IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() == IntPtr.Zero )
						{
							ImGui.Text( "The GroupManager instance pointer is null!" );
						}
						else if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() == IntPtr.Zero )
						{
							ImGui.Text( "The InfoProxyCrossRealm instance pointer is null!" );
						}
						else
						{
							var readyCheckdata = MemoryHandler.GetReadyCheckInfo();

							ImGui.Columns( 5 );
							ImGui.Text( "General Info:" );

							ImGui.Text( $"Number of Party Members: {FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount}" );
							ImGui.Text( $"Is Cross-World: {FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm}" );
							byte crossWorldGroupCount = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->GroupCount;
							ImGui.Text( $"Number of Cross-World Groups: {crossWorldGroupCount}" );
							for( int i = 0; i < crossWorldGroupCount; ++i )
							{
								ImGui.Text( $"Number of Party Members (Group {i}): {FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetGroupMemberCount( i )}" );
							}
							ImGui.Text( $"Ready check is active: {mPlugin.ReadyCheckActive}" );
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.Text( $"Ready Check Object Address: 0x{MemoryHandler.DEBUG_GetReadyCheckObjectAddress():X}" );
							ImGui.Text( $"Hud Agent Address: 0x{new IntPtr(pAgentHUD):X}" );
							ImGui.Checkbox( "Show Raw Readycheck Data", ref mDebugRawWindowVisible );
							ImGui.Checkbox( "Show Processed Readycheck Data", ref mDebugProcessedWindowVisible );
							ImGui.Checkbox( "Debug Drawing on Party List", ref mDEBUG_DrawPlaceholderData );
							ImGui.Checkbox( "Allow Cross-world Alliance List Drawing", ref mDEBUG_AllowCrossWorldAllianceDrawing );
							{
								if( ImGui.Button( "Test Chat Message" ) )
								{
									mPlugin.ListUnreadyPlayersInChat( new List<string>( LocalizationHelpers.TestNames.Take( mDEBUG_NumNamesToTestChatMessage ) ) );
								}
								ImGui.SliderInt( "Number of Test Names", ref mDEBUG_NumNamesToTestChatMessage, 1, LocalizationHelpers.TestNames.Length );
							}
							if( ImGui.Button( "Export Localizable Strings" ) )
							{
								string pwd = Directory.GetCurrentDirectory();
								Directory.SetCurrentDirectory( mPluginInterface.AssemblyLocation.DirectoryName );
								Loc.ExportLocalizable();
								Directory.SetCurrentDirectory( pwd );
							}
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
							ImGui.Text( "Ready Check Object Address:" );
							ImGuiHelpMarker( Loc.Localize( "Help: Debug Set Object Address Warning", "DO NOT TOUCH THIS UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING AND WHY; THE ABSOLUTE BEST CASE IS A PLUGIN CRASH." ) );
							ImGui.InputText( "##ObjectAddressSetInputBox", ref mDEBUG_ReadyCheckObjectAddressInputString, 16 );
							if( ImGui.Button( "Set Ready Check Object Address" ) )
							{
								bool isValidPointer = IntPtr.TryParse( mDEBUG_ReadyCheckObjectAddressInputString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out IntPtr ptr );
								if( isValidPointer ) MemoryHandler.DEBUG_SetReadyCheckObjectAddress( ptr );
							}
							ImGui.PopStyleColor();
							ImGui.NextColumn();
							ImGui.Text( "Ready Check Data:" );
							for( int i = 0; i < readyCheckdata.Length; ++i )
							{
								ImGui.Text( $"ID: {readyCheckdata[i].ID:X16}, State: {readyCheckdata[i].ReadyFlag}" );
							}
							ImGui.NextColumn();
							ImGui.Text( "Party Data:" );
							for( int i = 0; i < 8; ++i )
							{
								var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetPartyMemberByIndex( i );
								if( (IntPtr)pGroupMember != IntPtr.Zero )
								{
									string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pGroupMember->Name ).ToString();
									string classJobAbbr = JobDict.TryGetValue( pGroupMember->ClassJob, out classJobAbbr ) ? classJobAbbr : "ERR";
									ImGui.Text( $"Job: {classJobAbbr}, OID: {pGroupMember->ObjectID:X8}, CID: {pGroupMember->ContentID:X16}, Name: {name}" );
								}
								else
								{
									ImGui.Text( "Party member returned as null pointer." );
								}
							}
							for( int i = 0; i < 16; ++i )
							{
								var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetAllianceMemberByIndex( i );
								if( (IntPtr)pGroupMember != IntPtr.Zero )
								{
									string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pGroupMember->Name ).ToString();
									string classJobAbbr = JobDict.TryGetValue( pGroupMember->ClassJob, out classJobAbbr ) ? classJobAbbr : "ERR";
									ImGui.Text( $"Job: {classJobAbbr}, OID: {pGroupMember->ObjectID:X8}, CID: {pGroupMember->ContentID:X16}, Name: {name}" );
								}
								else
								{
									ImGui.Text( "Alliance member returned as null pointer." );
								}
							}
							ImGui.NextColumn();
							ImGui.Text( "Cross-World Party Data:" );
							for( int i = 0; i < crossWorldGroupCount; ++i )
							{
								for( int j = 0; j < FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetGroupMemberCount( i ); ++j )
								{
									var pGroupMember = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetGroupMember( (uint)j, i );
									if( (IntPtr)pGroupMember != IntPtr.Zero )
									{
										string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pGroupMember->Name ).ToString();
										ImGui.Text( $"Group: {pGroupMember->GroupIndex}, OID: {pGroupMember->ObjectId:X8}, CID: {pGroupMember->ContentId:X16}, Name: {name}" );
									}
								}
							}
							ImGui.NextColumn();
							ImGui.Text( $"AgentHUD Group Size: {pAgentHUD->RaidGroupSize}" );
							ImGui.Text( $"AgentHUD Party Size: {pAgentHUD->PartyMemberCount}" );
							ImGui.Text( "AgentHUD Party Members:" );
							IntPtr pPartyData = new( pAgentHUD->PartyMemberList );
							for( int i = 0; i < 8; ++i )
							{
								var partyMemberData = Marshal.PtrToStructure<PartyListCharInfo>( new( pPartyData.ToInt64() + ( i * Marshal.SizeOf<PartyListCharInfo>() ) ) );
								ImGui.Text( $"Object Address: 0x{partyMemberData.ObjectAddress:X}\r\nName Address: 0x{partyMemberData.ObjectNameAddress:X}\r\nName: {partyMemberData.GetName()}\r\nCID: {partyMemberData.ContentID:X}\r\nOID: {partyMemberData.ObjectID:X}\r\nUnknown: {partyMemberData.Unknown:X}" );
							}
							ImGui.Text( "AgentHUD Raid Members:" );
							for( int i = 0; i < 40; ++i )
							{
								ImGui.Text( $"{i:D2}: {pAgentHUD->RaidMemberIds[i]:X8}" );
							}
							ImGui.Columns();
						}
					}
				}
				finally
				{
					ImGui.PopFont();
				}
			}

			//	We're done.
			ImGui.End();
		}

		protected void DrawDebugRawWindow()
		{
			if( !DebugRawWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 1340, 568 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Raw Ready Check Data", "Debug: Raw Ready Check Data" ) + "###Raw Ready Check Data", ref mDebugRawWindowVisible ) )
			{
				ImGui.PushFont( UiBuilder.MonoFont );
				try
				{
					ImGui.Text( "Early object bytes:" );
					if( MemoryHandler.DEBUG_GetRawReadyCheckObjectStuff( out byte[] readyCheckObjectBytes ) )
					{
						string str = "";
						for( int i = 0; i < readyCheckObjectBytes.Length; ++i )
						{
							str += readyCheckObjectBytes[i].ToString( "X2" );
							if( ( i + 1 ) % 8 == 0 )
							{
								ImGui.Text( str + " " );
								str = "";
								if( ( i + 1 ) % 64 > 0 ) ImGui.SameLine();
							}
						}
					}
					else
					{
						ImGui.Text( "Raw ready check object is unavailable." );
					}

					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();

					ImGui.Text( "Ready check array:" );
					if( MemoryHandler.DEBUG_GetRawReadyCheckData( out IntPtr[] rawData ) )
					{
						for( int i = 0; i < rawData.Length; ++i )
						{
							if( i % 8 > 0 ) ImGui.SameLine();
							ImGui.Text( $"{rawData[i]:X16} " );
						}
					}
					else
					{
						ImGui.Text( "Raw ready check data is unavailable, most likely due to not yet having located the ready check object." );
					}
				}
				finally
				{
					ImGui.PopFont();
				}
			}

			//	We're done.
			ImGui.End();
		}

		protected void DrawDebugProcessedWindow()
		{
			if( !DebugProcessedWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 1340, 568 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Processed Ready Check Data", "Debug: Processed Ready Check Data" ) + "###Processed Ready Check Data", ref mDebugProcessedWindowVisible ) )
			{
				ImGui.PushFont( UiBuilder.MonoFont );
				try
				{
					var list = mPlugin.GetProcessedReadyCheckData();
					if( list != null )
					{
						foreach( var player in list )
						{
							ImGui.Text( $"OID: {player.ObjectID:X8}, CID: {player.ContentID:X16}, Group: {player.GroupIndex}, Index: {player.MemberIndex}, State: {(byte)player.ReadyState}, Name: {player.Name}" );
						}
					}

					if( ImGui.Button( Loc.Localize( "Button: Close", "Close" ) + "###Close" ) )
					{
						DebugProcessedWindowVisible = false;
					}
				}
				finally
				{
					ImGui.PopFont();
				}
			}

			//	We're done.
			ImGui.End();
		}

		protected unsafe void ClearPartyAllianceListIcons()
		{
			if( mGameGui == null ) return;

			AtkUnitBase* pAddon = null;
			pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "_PartyList", 1 );
			if( pAddon != null )
			{
				for( uint i = 0; i < 8; ++i ) AtkNodeHelpers.HideNode( pAddon, mReadyCheckPartyListNodeIDBase + i );
			}
			pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "_AllianceList1", 1 );
			if( pAddon != null )
			{
				for( uint i = 0; i < 8; ++i ) AtkNodeHelpers.HideNode( pAddon, mReadyCheckPartyListNodeIDBase + i );
			}
			pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "_AllianceList2", 1 );
			if( pAddon != null )
			{
				for( uint i = 0; i < 8; ++i ) AtkNodeHelpers.HideNode( pAddon, mReadyCheckPartyListNodeIDBase + i );
			}
			pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "Alliance48", 1 );
			if( pAddon != null )
			{
				for( uint i = 0; i < 48; ++i ) AtkNodeHelpers.HideNode( pAddon, mReadyCheckPartyListNodeIDBase + i );
			}
		}

		protected unsafe void UpdatePartyAllianceListIconNodes()
		{
			if( mGameGui == null ) return;

			//	It's kind of jank treating a retained-mode UI as immediate mode, but it's the easiest thing to do for now, and performance isn't awful.
			ClearPartyAllianceListIcons();

			if( mDEBUG_DrawPlaceholderData )
			{
				for( uint i = 0; i < 8; ++i )	UpdateReadyCheckImageNode( GameAddonEnum.PartyList,					i, (ReadyCheckState)( i % 2 + 2 ) );
				for( uint i = 0; i < 8; ++i )	UpdateReadyCheckImageNode( GameAddonEnum.AllianceList1,				i, (ReadyCheckState)( i % 2 + 2 ) );
				for( uint i = 0; i < 8; ++i )	UpdateReadyCheckImageNode( GameAddonEnum.AllianceList2,				i, (ReadyCheckState)( i % 2 + 2 ) );
				for( uint i = 0; i < 48; ++i )	UpdateReadyCheckImageNode( GameAddonEnum.CrossWorldAllianceList,	i, (ReadyCheckState)( i % 2 + 2 ) );
			}
			else if( mConfiguration.ShowReadyCheckOnPartyAllianceList && ReadyCheckValid )
			{
				var data = mPlugin.GetProcessedReadyCheckData();
				if( data != null )
				{
					foreach( var result in data )
					{
						var indices = MemoryHandler.GetHUDIndicesForChar( result.ContentID, result.ObjectID );
						if( indices == null ) continue;
						switch( indices.Value.GroupNumber )
						{
							case 0:
								UpdateReadyCheckImageNode( GameAddonEnum.PartyList, (uint)indices.Value.PartyMemberIndex, result.ReadyState );
								break;
							case 1:
								if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
								else UpdateReadyCheckImageNode( GameAddonEnum.AllianceList1, (uint)indices.Value.PartyMemberIndex, result.ReadyState );
								break;
							case 2:
								if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
								else UpdateReadyCheckImageNode( GameAddonEnum.AllianceList2, (uint)indices.Value.PartyMemberIndex, result.ReadyState );
								break;
							default:
								if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
								break;
						}
					}
				}
			}
		}

		unsafe protected void DrawOnPartyAllianceLists_ImGui()
		{
			if( ( mDEBUG_DrawPlaceholderData || ( mConfiguration.ShowReadyCheckOnPartyAllianceList && ReadyCheckValid ) ) && mGameGui != null )
			{
				const ImGuiWindowFlags flags =	ImGuiWindowFlags.NoDecoration |
												ImGuiWindowFlags.NoSavedSettings |
												ImGuiWindowFlags.NoMove |
												ImGuiWindowFlags.NoMouseInputs |
												ImGuiWindowFlags.NoFocusOnAppearing |
												ImGuiWindowFlags.NoBackground |
												ImGuiWindowFlags.NoNav;

				ImGuiHelpers.ForceNextWindowMainViewport();
				ImGui.SetNextWindowPos( ImGui.GetMainViewport().Pos );
				ImGui.SetNextWindowSize( ImGui.GetMainViewport().Size );
				if( ImGui.Begin( "##ReadyCheckOverlayWindow", flags ) )
				{
					var pPartyList = (AtkUnitBase*)mGameGui.GetAddonByName( "_PartyList", 1 );
					var pAlliance1List = (AtkUnitBase*)mGameGui.GetAddonByName( "_AllianceList1", 1 );
					var pAlliance2List = (AtkUnitBase*)mGameGui.GetAddonByName( "_AllianceList2", 1 );
					var pCrossWorldAllianceList = (AtkUnitBase*)mGameGui.GetAddonByName( "Alliance48", 1 );

					if( mDEBUG_DrawPlaceholderData )
					{
						if( pPartyList != null && pPartyList->IsVisible )
						{
							for( int i = 0; i < 8; ++i )
							{
								DrawOnPartyList( i, (ReadyCheckState)( i % 2 + 2 ), pPartyList, ImGui.GetWindowDrawList() );
							}
						}
						if( pAlliance1List != null && pAlliance1List->IsVisible )
						{
							for( int i = 0; i < 8; ++i )
							{
								DrawOnAllianceList( i, (ReadyCheckState)( i % 2 + 2 ), pAlliance1List, ImGui.GetWindowDrawList() );
							}
						}
						if( pAlliance2List != null && pAlliance2List->IsVisible )
						{
							for( int i = 0; i < 8; ++i )
							{
								DrawOnAllianceList( i, (ReadyCheckState)( i % 2 + 2 ), pAlliance2List, ImGui.GetWindowDrawList() );
							}
						}
						if( pCrossWorldAllianceList != null && pCrossWorldAllianceList->IsVisible )
						{
							for( int j = 1; j < 6; ++j )
							{
								for( int i = 0; i < 8; ++i )
								{
									DrawOnCrossWorldAllianceList( j, i, (ReadyCheckState)( i % 2 + 2 ), pCrossWorldAllianceList, ImGui.GetWindowDrawList() );
								}
							}
						}
					}
					else
					{
						var data = mPlugin.GetProcessedReadyCheckData();
						if( data != null )
						{
							foreach( var result in data )
							{
								var indices = MemoryHandler.GetHUDIndicesForChar( result.ContentID, result.ObjectID );
								if( indices == null ) continue;
								switch( indices.Value.GroupNumber )
								{
									case 0:
										DrawOnPartyList( indices.Value.PartyMemberIndex, result.ReadyState, pPartyList, ImGui.GetWindowDrawList() );
										break;
									case 1:
										if( indices.Value.CrossWorld ) break;	//***** TODO: Do something when crossworld alliances are fixed.
										else DrawOnAllianceList( indices.Value.PartyMemberIndex, result.ReadyState, pAlliance1List, ImGui.GetWindowDrawList() );
										break;
									case 2:
										if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
										else DrawOnAllianceList( indices.Value.PartyMemberIndex, result.ReadyState, pAlliance2List, ImGui.GetWindowDrawList() );
										break;
									default:
										if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
										break;
								}
							}
						}
					}
				}

				ImGui.End();
			}
		}

		unsafe protected void DrawOnPartyList( int listIndex, ReadyCheckState readyCheckState, AtkUnitBase* pPartyList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			int partyMemberNodeIndex = 22 - listIndex;
			int iconNodeIndex = 4;

			var pPartyMemberNode = pPartyList->UldManager.NodeListCount > partyMemberNodeIndex ? (AtkComponentNode*) pPartyList->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
			if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
			{
				var pIconNode = pPartyMemberNode->Component->UldManager.NodeListCount > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
				if( (IntPtr)pIconNode != IntPtr.Zero )
				{
					//	Note: sub-nodes don't scale, so we have to account for the addon's scale.
					Vector2 iconOffset = ( new Vector2( -7, -5 ) + mConfiguration.PartyListIconOffset ) * pPartyList->Scale;
					Vector2 iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) * mConfiguration.PartyListIconScale * pPartyList->Scale;
					Vector2 iconPos = new Vector2(	pPartyList->X + pPartyMemberNode->AtkResNode.X * pPartyList->Scale + pIconNode->X * pPartyList->Scale + pIconNode->Width * pPartyList->Scale / 2,
													pPartyList->Y + pPartyMemberNode->AtkResNode.Y * pPartyList->Scale + pIconNode->Y * pPartyList->Scale + pIconNode->Height * pPartyList->Scale / 2 );
					iconPos += iconOffset;

					if( readyCheckState == ReadyCheckState.NotReady )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == ReadyCheckState.Ready )
					{ 
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState == ReadyCheckState.CrossWorldMemberNotPresent )
					{
						drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnAllianceList( int listIndex, ReadyCheckState readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			int partyMemberNodeIndex = 9 - listIndex;
			int iconNodeIndex = 5;

			var pAllianceMemberNode = pAllianceList->UldManager.NodeListCount > partyMemberNodeIndex ? (AtkComponentNode*) pAllianceList->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
			if( (IntPtr)pAllianceMemberNode != IntPtr.Zero )
			{
				var pIconNode = pAllianceMemberNode->Component->UldManager.NodeListCount > iconNodeIndex ? pAllianceMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
				if( (IntPtr)pIconNode != IntPtr.Zero )
				{
					Vector2 iconOffset = ( new Vector2( 0, 0 ) + mConfiguration.AllianceListIconOffset ) * pAllianceList->Scale;
					Vector2 iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) * mConfiguration.AllianceListIconScale * pAllianceList->Scale;
					Vector2 iconPos = new Vector2(	pAllianceList->X + pAllianceMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2,
													pAllianceList->Y + pAllianceMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2 );
					iconPos += iconOffset;

					if( readyCheckState == ReadyCheckState.NotReady )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == ReadyCheckState.Ready )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState == ReadyCheckState.CrossWorldMemberNotPresent )
					{
						drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnCrossWorldAllianceList( int allianceIndex, int partyMemberIndex, ReadyCheckState readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( allianceIndex < 1 || allianceIndex > 5 ) return;
			if( partyMemberIndex < 0 || partyMemberIndex > 7 ) return;
			int allianceNodeIndex = 8 - allianceIndex;
			int partyMemberNodeIndex = 8 - partyMemberIndex;
			int iconNodeIndex = 2;

			//***** TODO: This *occasionally* crashes, and I don't understand why.  Best guess is that the node list is not populated all at once, but grows as the addon is created.*****
			if( !mDEBUG_AllowCrossWorldAllianceDrawing ) return;
			var pAllianceNode = pAllianceList->UldManager.NodeListCount > allianceNodeIndex ? (AtkComponentNode*) pAllianceList->UldManager.NodeList[allianceNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
			if( (IntPtr)pAllianceNode != IntPtr.Zero )
			{
				var pPartyMemberNode = pAllianceNode->Component->UldManager.NodeListCount > partyMemberNodeIndex ? (AtkComponentNode*) pAllianceNode->Component->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
				if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
				{
					var pIconNode = pPartyMemberNode->Component->UldManager.NodeListCount > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
					if( (IntPtr)pIconNode != IntPtr.Zero )
					{
						Vector2 iconOffset = ( new Vector2( 0, 0 ) + mConfiguration.CrossWorldAllianceListIconOffset ) * pAllianceList->Scale;
						Vector2 iconSize = new Vector2( pIconNode->Width / 2, pIconNode->Height / 2 ) * mConfiguration.CrossWorldAllianceListIconScale * pAllianceList->Scale;
						Vector2 iconPos = new Vector2(	pAllianceList->X + pAllianceNode->AtkResNode.X * pAllianceList->Scale + pPartyMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2,
														pAllianceList->Y + pAllianceNode->AtkResNode.Y * pAllianceList->Scale + pPartyMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2 );
						iconPos += iconOffset;

						if( readyCheckState == ReadyCheckState.NotReady )
						{
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
						}
						else if( readyCheckState == ReadyCheckState.Ready )
						{
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
						}
						else if( readyCheckState == ReadyCheckState.CrossWorldMemberNotPresent )
						{
							drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						}
					}
				}
			}
		}

		protected unsafe void UpdateReadyCheckImageNode( GameAddonEnum addon, uint memberIndex, ReadyCheckState readyCheckState )
		{
			AtkImageNode* pNode = null;
			AtkResNode* pParentNode = null;
			AtkUnitBase* pAddon = null;

			uint nodeID = mReadyCheckPartyListNodeIDBase + memberIndex;

			if( addon == GameAddonEnum.PartyList )
			{
				if( memberIndex > 7 )
				{
					PluginLog.LogDebug( $"Error updating party list ready check node: Member Index {memberIndex} is invalid." );
					return;
				}
				pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "_PartyList", 1 );

				if( pAddon != null )
				{
					//	Find our node by ID.  Doing this allows us to not have to deal with freeing the node resources and removing connections to sibling nodes (we'll still leak, but only once).
					pNode = AtkNodeHelpers.GetImageNodeByID( pAddon, nodeID );

					if( pNode != null )
					{
						uint partyMemberNodeIndex = 22 - memberIndex;
						uint iconNodeIndex = 4;

						var pPartyMemberNode = pAddon->UldManager.NodeListCount > partyMemberNodeIndex ? (AtkComponentNode*) pAddon->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
						if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
						{
							var pIconNode = pPartyMemberNode->Component->UldManager.NodeListCount > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
							if( (IntPtr)pIconNode != IntPtr.Zero )
							{
								Vector2 iconOffset = ( new Vector2( -7, -5 ) + mConfiguration.PartyListIconOffset );
								Vector2 iconScale = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) / new Vector2( pNode->AtkResNode.Width, pNode->AtkResNode.Height ) * mConfiguration.PartyListIconScale;
								Vector2 iconPos = new Vector2(	pPartyMemberNode->AtkResNode.X + pIconNode->X + pIconNode->Width / 2,
																pPartyMemberNode->AtkResNode.Y + pIconNode->Y + pIconNode->Height / 2 );
								iconPos += iconOffset;

								pNode->AtkResNode.ToggleVisibility( readyCheckState is ReadyCheckState.Ready or ReadyCheckState.NotReady );
								pNode->PartId = (ushort)( readyCheckState == ReadyCheckState.Ready ? 0 : 1 );
								pNode->AtkResNode.SetPositionFloat( iconPos.X, iconPos.Y );
								pNode->AtkResNode.SetScale( iconScale.X, iconScale.Y );
							}
							else
							{
								pNode->AtkResNode.ToggleVisibility( false );
							}
						}
						else
						{
							pNode->AtkResNode.ToggleVisibility( false );
						}
					}
					else
					{
						var partList = new List<AtkUldPart>();
						partList.Add( new() { U = 0, V = 0, Width = 48, Height = 48 } );
						partList.Add( new() { U = 48, V = 48, Width = 48, Height = 48 } );
						var pNewNode = AtkNodeHelpers.CreateOrphanImageNode( nodeID, partList, GameAddonEnum.PartyList );
						if( pNewNode != null ) AtkNodeHelpers.AttachImageNode( pAddon, pNewNode );
					}
				}
			}
			else if( addon == GameAddonEnum.AllianceList1 )
			{
				if( memberIndex > 7 )
				{
					PluginLog.LogDebug( $"Error updating alliance list 1 ready check node: Member Index {memberIndex} is invalid." );
					return;
				}
				pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "_AllianceList1", 1 );
			}
			else if( addon == GameAddonEnum.AllianceList2 )
			{
				if( memberIndex > 7 )
				{
					PluginLog.LogDebug( $"Error updating alliance list 2 ready check node: Member Index {memberIndex} is invalid." );
					return;
				}
				pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "_AllianceList2", 1 );
			}
			else if( addon == GameAddonEnum.CrossWorldAllianceList )
			{
				if( memberIndex > 47 )
				{
					PluginLog.LogDebug( $"Error updating cross world alliance list ready check node: Member Index {memberIndex} is invalid." );
					return;
				}
				pAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "Alliance48", 1 );
			}
		}

		protected void ImGuiHelpMarker( string description, bool sameLine = true, string marker = "(?)" )
		{
			if( sameLine ) ImGui.SameLine();
			ImGui.TextDisabled( marker );
			if( ImGui.IsItemHovered() )
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos( ImGui.GetFontSize() * 35.0f );
				ImGui.TextUnformatted( description );
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
		}

		public void ShowReadyCheckOverlay()
		{
			ReadyCheckValid = true;
		}

		public void InvalidateReadyCheck()
		{
			ReadyCheckValid = false;
		}

		protected Plugin mPlugin;
		protected DalamudPluginInterface mPluginInterface;
		protected Configuration mConfiguration;
		protected DataManager mDataManager;
		protected GameGui mGameGui;

		protected Dictionary<uint, string> JobDict { get; set; } = new Dictionary<uint, string>();

		protected TextureWrap mReadyCheckIconTexture = null;
		protected TextureWrap mUnknownStatusIconTexture = null;
		protected TextureWrap mNotPresentIconTexture = null;

		protected bool ReadyCheckValid { get; set; }
		protected bool mDEBUG_DrawPlaceholderData = false;
		protected string mDEBUG_ReadyCheckObjectAddressInputString = "";
		protected bool mDEBUG_AllowCrossWorldAllianceDrawing = false;
		protected int mDEBUG_NumNamesToTestChatMessage = 5;

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mSettingsWindowVisible = false;
		public bool SettingsWindowVisible
		{
			get { return mSettingsWindowVisible; }
			set { mSettingsWindowVisible = value; }
		}

		protected bool mReadyCheckResultsWindowVisible = false;
		public bool ReadyCheckResultsWindowVisible
		{
			get { return mReadyCheckResultsWindowVisible; }
			set { mReadyCheckResultsWindowVisible = value; }
		}

		protected bool mDebugWindowVisible = false;
		public bool DebugWindowVisible
		{
			get { return mDebugWindowVisible; }
			set { mDebugWindowVisible = value; }
		}

		protected bool mDebugRawWindowVisible = false;
		public bool DebugRawWindowVisible
		{
			get { return mDebugRawWindowVisible; }
			set { mDebugRawWindowVisible = value; }
		}

		protected bool mDebugProcessedWindowVisible = false;
		public bool DebugProcessedWindowVisible
		{
			get { return mDebugProcessedWindowVisible; }
			set { mDebugProcessedWindowVisible = value; }
		}

		private static readonly uint mReadyCheckPartyListNodeIDBase = 0x6C78B200;	//YOLO hoping for no collisions.  Can use the same ID base for each addon.
	}
}