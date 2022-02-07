using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Globalization;

using ImGuiNET;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;


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
			mNotReadyIconTexture?.Dispose();
			mReadyIconTexture?.Dispose();
			mHudManager?.Dispose();
			mReadyCheckIconTexture = null;
			mUnknownStatusIconTexture = null;
			mNotReadyIconTexture = null;
			mReadyIconTexture = null;
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
			mUnknownStatusIconTexture	??= mDataManager.GetImGuiTextureIcon( 061504 );
			mNotReadyIconTexture		??= mDataManager.GetImGuiTextureIcon( 061504 );
			mReadyIconTexture			??= mDataManager.GetImGuiTextureIcon( 061504 );
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawSettingsWindow();
			DrawDebugWindow();
			DrawDebugRawWindow();

			//	Draw other UI stuff.
			if( mConfiguration.ShowReadyCheckOnPartyAllianceList )
			{
				DrawOnPartyAllianceLists();
			}
		}

		protected void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 430, 170 ) * ImGui.GetIO().FontGlobalScale );
			if( ImGui.Begin( "Ready Check Helper Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Text( "Maximum number of names to show in chat:" );
				ImGui.SliderInt( "##MaxUnreadyNamesToShowInChat", ref mConfiguration.mMaxUnreadyToListInChat, 1, 48 );
				ImGui.Checkbox( "Draw ready check on party/alliance lists.", ref mConfiguration.mShowReadyCheckOnPartyAllianceList );

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				if( ImGui.Button( "Save and Close" ) )
				{
					mConfiguration.Save();
					SettingsWindowVisible = false;
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
			if( ImGui.Begin( "Ready Check and Alliance Debug Data", ref mDebugWindowVisible ) )
			{
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
						if( ImGui.Button( "Show/Hide Raw Readycheck Data" ) ) DebugRawWindowVisible = !DebugRawWindowVisible;
						ImGui.Checkbox( "Debug Drawing on Party List", ref mDEBUG_DrawPlaceholderData );
						ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
						ImGui.Text( "Ready Check Object Address:" );
						ImGuiHelpMarker( "(DO NOT TOUCH THIS UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING AND WHY; THE ABSOLUTE BEST CASE IS A PLUGIN CRASH)" );
						ImGui.InputText( "##ObjectAddressSetInputBox", ref mDEBUG_ReadyCheckObjectAddressInputString, 16 );
						if( ImGui.Button( "Set Ready Check Object Address" ) )
						{
							IntPtr ptr;
							bool isValidPointer = IntPtr.TryParse( mDEBUG_ReadyCheckObjectAddressInputString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ptr );
							if( isValidPointer ) MemoryHandler.DEBUG_SetReadyCheckObjectAddress( ptr );
						}
						ImGui.PopStyleColor(); ;
						ImGui.NextColumn();
						ImGui.Text( "Ready Check Data:" );
						for( int i = 0; i < readyCheckdata.Length; ++i )
						{
							ImGui.Text( $"ID: {readyCheckdata[i].ID.ToString( "X16" )}, State: {readyCheckdata[i].ReadyFlag}" );
						}
						ImGui.NextColumn();
						ImGui.Text( "Party Data:" );
						for( int i = 0; i < 8; ++i )
						{
							var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetPartyMemberByIndex( i );
							if( (IntPtr)pGroupMember != IntPtr.Zero )
							{
								string name = System.Text.Encoding.UTF8.GetString( pGroupMember->Name, 64 );    //***** TODO: How to get fixed buffer lenghth instead of magic numbering it here? *****
								name = name.Substring( 0, name.IndexOf( '\0' ) );

								string classJobAbbr = JobDict.TryGetValue( pGroupMember->ClassJob, out classJobAbbr ) ? classJobAbbr : "ERR";
								ImGui.Text( $"Job: {classJobAbbr}, OID: {pGroupMember->ObjectID.ToString( "X8" )}, CID: {pGroupMember->ContentID.ToString( "X16" )}, Name: {name}" );
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
								string name = System.Text.Encoding.UTF8.GetString( pGroupMember->Name, 64 );    //***** TODO: How to get fixed buffer lenghth instead of magic numbering it here? *****
								name = name.Substring( 0, name.IndexOf( '\0' ) );

								string classJobAbbr = JobDict.TryGetValue( pGroupMember->ClassJob, out classJobAbbr ) ? classJobAbbr : "ERR";
								ImGui.Text( $"Job: {classJobAbbr}, OID: {pGroupMember->ObjectID.ToString( "X8" )}, CID: {pGroupMember->ContentID.ToString( "X16" )}, Name: {name}" );
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
									string name = System.Text.Encoding.UTF8.GetString( pGroupMember->Name, 30 );	//***** TODO: How to get fixed buffer lenghth instead of magic numbering it here? *****
									name = name.Substring( 0, name.IndexOf( '\0' ) );
									ImGui.Text( $"Group: {pGroupMember->GroupIndex}, OID: {pGroupMember->ObjectId.ToString( "X8" )}, CID: {pGroupMember->ContentId.ToString( "X16" )}, Name: {name}" );
								}
							}
						}
						ImGui.Columns();
					}
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
			if( ImGui.Begin( "Raw Ready Check Data", ref mDebugRawWindowVisible ) )
			{
				ImGui.Text( "Early object bytes:" );
				byte[] readyCheckObjectBytes = null;
				if( MemoryHandler.DEBUG_GetRawReadyCheckObjectStuff( out readyCheckObjectBytes ) )
				{
					string str = "";
					for( int i = 0; i < readyCheckObjectBytes.Length; ++i )
					{
						str += readyCheckObjectBytes[i].ToString( "X2" );
						if( (i + 1) % 8 == 0 )
						{
							ImGui.Text( str );
							str = " ";
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
				IntPtr[] rawData = null;
				if( MemoryHandler.DEBUG_GetRawReadyCheckData( out rawData ) )
				{
					for( int i = 0; i < rawData.Length; ++i )
					{
						if( i % 8 > 0 ) ImGui.SameLine();
						ImGui.Text( $"{rawData[i].ToString( "X16" )} " );
						
					}
				}
				else
				{
					ImGui.Text( "Raw ready check data is unavailable, most likely due to not yet having located the ready check object." );
				}
			}

			//	We're done.
			ImGui.End();
		}

		unsafe protected void DrawOnPartyAllianceLists()
		{
			if( mGameGui != null )
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
						//***** TODO: For finding out order in the party list, use HudManager for regular parties/alliances.  Cross-world seem to be just the order that the proxy has them indexed, but verify this as much as possible. *****
						if( (IntPtr)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() != IntPtr.Zero )
						{
							if( (IntPtr)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() != IntPtr.Zero )
							{
								var data = mPlugin.GetProcessedReadyCheckData();
								if( data != null )
								{
									//	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
									//	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
									if( FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm > 0 &&
										FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount < 1 )
									{
										foreach( var result in data )
										{
											if( result.GroupIndex == 0 && (IntPtr)pPartyList != IntPtr.Zero && pPartyList->IsVisible )
											{
												DrawOnPartyList( result.MemberIndex, result.ReadyState, pPartyList, ImGui.GetWindowDrawList() );
											}
											else if( result.GroupIndex >= 1 && (IntPtr)pCrossWorldAllianceList != IntPtr.Zero && pCrossWorldAllianceList->IsVisible )
											{
												DrawOnCrossWorldAllianceList( result.GroupIndex, result.MemberIndex, result.ReadyState, pAlliance1List, ImGui.GetWindowDrawList() );
											}
										}
									}
									else
									{

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
			int nodeIndex = 21 - listIndex;

			var pPartyMemberNode = (AtkComponentNode*) pPartyList->UldManager.NodeList[nodeIndex];
			if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
			{
				var pIconNode = pPartyMemberNode->Component->UldManager.NodeList[4];
				if( (IntPtr)pIconNode != IntPtr.Zero )
				{
					//***** TODO: Handle scaled party lists; just testing for now. *****
					Vector2 iconOffset = new Vector2( -7, -5 );
					Vector2 iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 );
					Vector2 iconPos = new Vector2( pPartyList->X + pPartyMemberNode->AtkResNode.X + pIconNode->X + pIconNode->Width / 2, pPartyList->Y + pPartyMemberNode->AtkResNode.Y + pIconNode->Y + pIconNode->Height / 2 );
					iconPos += iconOffset;

					if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.NotReady )
					{
						//drawList.AddImage( mNotReadyIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.Ready )
					{ 
						//drawList.AddImage( mReadyIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState != MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
					{
						drawList.AddImage( mUnknownStatusIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnAllianceList( int listIndex, MemoryHandler.ReadyCheckStateEnum readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			int nodeIndex = 9 - listIndex;

			var pAllianceMemberNode = (AtkComponentNode*) pAllianceList->UldManager.NodeList[nodeIndex];
			if( (IntPtr)pAllianceMemberNode != IntPtr.Zero )
			{
				var pIconNode = pAllianceMemberNode->Component->UldManager.NodeList[5];
				if( (IntPtr)pIconNode != IntPtr.Zero )
				{
					//***** TODO: Handle scaled party lists; just testing for now. *****
					Vector2 iconOffset = new Vector2( -7, -5 );
					Vector2 iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 );
					Vector2 iconPos = new Vector2( pAllianceList->X + pAllianceMemberNode->AtkResNode.X + pIconNode->X + pIconNode->Width / 2, pAllianceList->Y + pAllianceMemberNode->AtkResNode.Y + pIconNode->Y + pIconNode->Height / 2 );
					iconPos += iconOffset;

					if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.NotReady )
					{
						//drawList.AddImage( mNotReadyIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.Ready )
					{
						//drawList.AddImage( mReadyIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState != MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
					{
						drawList.AddImage( mUnknownStatusIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnCrossWorldAllianceList( int allianceIndex, int partyMemberIndex, MemoryHandler.ReadyCheckStateEnum readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( allianceIndex < 1 || allianceIndex > 5 ) return;
			if( partyMemberIndex < 0 || partyMemberIndex > 7 ) return;
			int allianceNodeIndex = 8 - allianceIndex;
			int partyNodeIndex = 8 - partyMemberIndex;

			var pAllianceNode = (AtkComponentNode*) pAllianceList->UldManager.NodeList[allianceNodeIndex];
			if( (IntPtr)pAllianceNode != IntPtr.Zero )
			{
				var pPartyMemberNode = (AtkComponentNode*) pAllianceNode->Component->UldManager.NodeList[partyNodeIndex];
				if( (IntPtr)pPartyMemberNode != IntPtr.Zero )
				{
					var pIconNode = pPartyMemberNode->Component->UldManager.NodeList[2];
					if( (IntPtr)pIconNode != IntPtr.Zero )
					{
						//***** TODO: Handle scaled party lists; just testing for now. *****
						Vector2 iconOffset = new Vector2( 0, 0 );
						Vector2 iconSize = new Vector2( pIconNode->Width / 2, pIconNode->Height / 2 );
						Vector2 iconPos = new Vector2(	pAllianceList->X + pAllianceNode->AtkResNode.X + pPartyMemberNode->AtkResNode.X + pIconNode->X + pIconNode->Width / 2,
														pAllianceList->Y + pAllianceNode->AtkResNode.Y + pPartyMemberNode->AtkResNode.Y + pIconNode->Y + pIconNode->Height / 2 );
						iconPos += iconOffset;

						if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.NotReady )
						{
							//drawList.AddImage( mNotReadyIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
						}
						else if( readyCheckState == MemoryHandler.ReadyCheckStateEnum.Ready )
						{
							//drawList.AddImage( mReadyIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
						}
						else if( readyCheckState != MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
						{
							drawList.AddImage( mUnknownStatusIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
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

		protected void InvalidateReadyCheck()
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
		protected TextureWrap mNotReadyIconTexture = null;
		protected TextureWrap mReadyIconTexture = null;

		protected bool ReadyCheckValid { get; set; }
		protected bool mDEBUG_DrawPlaceholderData = false;
		protected string mDEBUG_ReadyCheckObjectAddressInputString = "";

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mSettingsWindowVisible = false;
		public bool SettingsWindowVisible
		{
			get { return mSettingsWindowVisible; }
			set { mSettingsWindowVisible = value; }
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
	}
}