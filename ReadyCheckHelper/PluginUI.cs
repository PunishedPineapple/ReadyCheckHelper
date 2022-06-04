using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Globalization;
using System.IO;

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
using FFXIVClientStructs.FFXIV.Component.GUI;
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
			mHudManager = new HudManager( sigScanner );
		}

		//	Destruction
		public void Dispose()
		{
			mReadyCheckIconTexture?.Dispose();
			mUnknownStatusIconTexture?.Dispose();
			mNotPresentIconTexture?.Dispose();
			mHudManager?.Dispose();
			mReadyCheckIconTexture = null;
			mUnknownStatusIconTexture = null;
			mNotPresentIconTexture = null;
			mHudManager = null;
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

			//	Draw other UI stuff.
			DrawOnPartyAllianceLists();
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
					var tableList = new List<List<Plugin.CorrelatedReadyCheckEntry>>();
					foreach( var player in list )
					{
						if( tableList.Count <= player.GroupIndex )
						{
							tableList.Add( new List<Plugin.CorrelatedReadyCheckEntry>() );
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
									if( tableList[j][i].ReadyState == MemoryHandler.ReadyCheckStateEnum.Ready )
									{
										ImGui.Image( mReadyCheckIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
									}
									else if( tableList[j][i].ReadyState == MemoryHandler.ReadyCheckStateEnum.NotReady )
									{
										ImGui.Image( mReadyCheckIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
									}
									else if( tableList[j][i].ReadyState == MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
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
				unsafe
				{
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

						ImGui.Columns( 4 );
						ImGui.Text( "General Info:" );

						ImGui.Text( $"Number of Party Members: {FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount}" );
						ImGui.Text( $"Is Alliance: {FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->IsAlliance}" );
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
						ImGui.Text( $"Hud Agent Address: 0x{mHudManager._hudAgentPtr:X}" );
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
						ImGui.Columns();
					}
				}
				ImGui.PopFont();
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
				ImGui.PopFont();
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
				ImGui.PopFont();
			}

			//	We're done.
			ImGui.End();
		}

		unsafe protected void DrawOnPartyAllianceLists()
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
						if( (IntPtr)pPartyList != IntPtr.Zero && pPartyList->IsVisible )
						{
							for( int i = 0; i < 8; ++i )
							{
								DrawOnPartyList( i, MemoryHandler.ReadyCheckStateEnum.Ready, pPartyList, ImGui.GetWindowDrawList() );
							}
						}
						if( (IntPtr)pAlliance1List != IntPtr.Zero && pAlliance1List->IsVisible )
						{
							for( int i = 0; i < 8; ++i )
							{
								DrawOnAllianceList( i, MemoryHandler.ReadyCheckStateEnum.Ready, pAlliance1List, ImGui.GetWindowDrawList() );
							}
						}
						if( (IntPtr)pAlliance2List != IntPtr.Zero && pAlliance2List->IsVisible )
						{
							for( int i = 0; i < 8; ++i )
							{
								DrawOnAllianceList( i, MemoryHandler.ReadyCheckStateEnum.Ready, pAlliance2List, ImGui.GetWindowDrawList() );
							}
						}
						if( (IntPtr)pCrossWorldAllianceList != IntPtr.Zero && pCrossWorldAllianceList->IsVisible )
						{
							for( int j = 1; j < 6; ++j )
							{
								for( int i = 0; i < 8; ++i )
								{
									DrawOnCrossWorldAllianceList( j, i, MemoryHandler.ReadyCheckStateEnum.Ready, pCrossWorldAllianceList, ImGui.GetWindowDrawList() );
								}
							}
						}
					}
					else
					{
						//	For finding out order in the party list, use HudManager for regular parties/alliances.  Cross-world seems to be just the order that the proxy has them indexed.
						if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() != IntPtr.Zero &&
							(IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() != IntPtr.Zero )
						{
							var data = mPlugin.GetProcessedReadyCheckData();
							if( data != null )
							{
								//	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
								//	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
								if( FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount > 0  )
								{
									foreach( var result in data )
									{
										if( result.GroupIndex == 0 )
										{
											bool resultFound = false;
											if( result.ContentID != 0 )
											{
												var idx = mHudManager.FindPartyMemberByCID( result.ContentID );
												if( idx != null )
												{
													resultFound = true;
													if( (IntPtr)pPartyList != IntPtr.Zero && pPartyList->IsVisible )
													{
														DrawOnPartyList( idx.Value, result.ReadyState, pPartyList, ImGui.GetWindowDrawList() );
													}
												}
											}
											else if( result.ObjectID is not 0 and not 0xE0000000 )
											{
												var group = mHudManager.FindGroupMemberByOID( result.ObjectID );
												if( group != null )
												{
													resultFound = true;
													if( group.Value.groupIdx == 0 && (IntPtr)pPartyList != IntPtr.Zero && pPartyList->IsVisible )
													{
														DrawOnPartyList( group.Value.idx, result.ReadyState, pPartyList, ImGui.GetWindowDrawList() );
													}
												}
											}
												
											if( !resultFound /*&& useStringComparisonFallback*/ )
											{
												//***** TODO: Do a fallback comparison by player name if we want to.  Need to investigate performance doing this. *****
											}
										}
										//***** TODO: If an overworld alliance is still possible, what we have here will still not be good enough. *****
										else
										{
											bool resultFound = false;
											if( result.ObjectID is not 0 and not 0xE0000000 )
											{
												var group = mHudManager.FindGroupMemberByOID( result.ObjectID );
												if( group != null )
												{
													resultFound = true;
													if( group.Value.groupIdx == 1 && (IntPtr)pAlliance1List != IntPtr.Zero && pAlliance1List->IsVisible )
													{
														DrawOnAllianceList( group.Value.idx, result.ReadyState, pAlliance1List, ImGui.GetWindowDrawList() );
													}
													else if( group.Value.groupIdx == 2 && (IntPtr)pAlliance2List != IntPtr.Zero && pAlliance2List->IsVisible )
													{
														DrawOnAllianceList( group.Value.idx, result.ReadyState, pAlliance2List, ImGui.GetWindowDrawList() );
													}
												}
											}

											if( !resultFound /*&& useStringComparisonFallback*/ )
											{
												//***** TODO: Do a fallback comparison by player name if we want to.  Need to investigate performance doing this. *****
											}
										}
									}
								}
								else if( FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm > 0 )
								{
									foreach( var result in data )
									{
										if( result.GroupIndex == 0 && (IntPtr)pPartyList != IntPtr.Zero && pPartyList->IsVisible )
										{
											DrawOnPartyList( result.MemberIndex, result.ReadyState, pPartyList, ImGui.GetWindowDrawList() );
										}
										else if( result.GroupIndex >= 1 && (IntPtr)pCrossWorldAllianceList != IntPtr.Zero && pCrossWorldAllianceList->IsVisible )
										{
											//***** TODO: Remove the debug conditional on this when we can figure out what is causing it to occasionally crash. *****
											if( mDEBUG_AllowCrossWorldAllianceDrawing ) DrawOnCrossWorldAllianceList( result.GroupIndex, result.MemberIndex, result.ReadyState, pAlliance1List, ImGui.GetWindowDrawList() );
										}
									}
								}
							}
						}
					}
				}

				ImGui.End();
			}
		}

		unsafe protected void DrawOnPartyList( int listIndex, MemoryHandler.ReadyCheckStateEnum readyCheckState, AtkUnitBase* pPartyList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			int partyMemberNodeIndex = 21 - listIndex;
			int iconNodeIndex = 4;

			var pPartyMemberNode = pPartyList->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*) pPartyList->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
			if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
			{
				var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
				if( (IntPtr)pIconNode != IntPtr.Zero )
				{
					//	Note: sub-nodes don't scale, so we have to account for the addon's scale.
					Vector2 iconOffset = new Vector2( -7, -5 ) * pPartyList->Scale;
					Vector2 iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) * pPartyList->Scale;
					Vector2 iconPos = new Vector2(	pPartyList->X + pPartyMemberNode->AtkResNode.X * pPartyList->Scale + pIconNode->X * pPartyList->Scale + pIconNode->Width * pPartyList->Scale / 2,
													pPartyList->Y + pPartyMemberNode->AtkResNode.Y * pPartyList->Scale + pIconNode->Y * pPartyList->Scale + pIconNode->Height * pPartyList->Scale / 2 );
					iconPos += iconOffset;

					if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.NotReady )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.Ready )
					{ 
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
					{
						drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnAllianceList( int listIndex, MemoryHandler.ReadyCheckStateEnum readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			int partyMemberNodeIndex = 9 - listIndex;
			int iconNodeIndex = 5;

			var pAllianceMemberNode = pAllianceList->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*) pAllianceList->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
			if( (IntPtr)pAllianceMemberNode != IntPtr.Zero )
			{
				var pIconNode = pAllianceMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pAllianceMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
				if( (IntPtr)pIconNode != IntPtr.Zero )
				{
					Vector2 iconOffset = new Vector2( 0, 0 ) * pAllianceList->Scale;
					Vector2 iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) * pAllianceList->Scale;
					Vector2 iconPos = new Vector2(	pAllianceList->X + pAllianceMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2,
													pAllianceList->Y + pAllianceMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2 );
					iconPos += iconOffset;

					if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.NotReady )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.Ready )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
					{
						drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnCrossWorldAllianceList( int allianceIndex, int partyMemberIndex, MemoryHandler.ReadyCheckStateEnum readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( allianceIndex < 1 || allianceIndex > 5 ) return;
			if( partyMemberIndex < 0 || partyMemberIndex > 7 ) return;
			int allianceNodeIndex = 8 - allianceIndex;
			int partyMemberNodeIndex = 8 - partyMemberIndex;
			int iconNodeIndex = 2;

			//***** TODO: This *occasionally* crashes, and I don't understand why.  Best guess is that the node list is not populated all at once, but grows as the addon is created.*****
			var pAllianceNode = pAllianceList->UldManager.NodeListSize > allianceNodeIndex ? (AtkComponentNode*) pAllianceList->UldManager.NodeList[allianceNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
			if( (IntPtr)pAllianceNode != IntPtr.Zero )
			{
				var pPartyMemberNode = pAllianceNode->Component->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*) pAllianceNode->Component->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) IntPtr.Zero;
				if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
				{
					var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) IntPtr.Zero;
					if( (IntPtr)pIconNode != IntPtr.Zero )
					{
						Vector2 iconOffset = new Vector2( 0, 0 ) * pAllianceList->Scale;
						Vector2 iconSize = new Vector2( pIconNode->Width / 2, pIconNode->Height / 2 ) * pAllianceList->Scale;
						Vector2 iconPos = new Vector2(	pAllianceList->X + pAllianceNode->AtkResNode.X * pAllianceList->Scale + pPartyMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2,
														pAllianceList->Y + pAllianceNode->AtkResNode.Y * pAllianceList->Scale + pPartyMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2 );
						iconPos += iconOffset;

						if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.NotReady )
						{
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
						}
						else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.Ready )
						{
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
						}
						else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
						{
							drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						}
					}
				}
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

		protected HudManager mHudManager;

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
	}
}