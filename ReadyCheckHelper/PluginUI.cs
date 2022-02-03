using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Linq;

using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Data;


namespace ReadyCheckHelper
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Configuration configuration, DataManager dataManager )
		{
			mConfiguration = configuration;
			mDataManager = dataManager;
		}

		//	Destruction
		public void Dispose()
		{
		}

		public void Initialize()
		{
			ExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob> classJobSheet = mDataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>();
			foreach( ClassJob job in classJobSheet.ToList() )
			{
				JobDict.Add( job.RowId, job.Abbreviation );
			}
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawMainWindow();
			DrawSettingsWindow();
			DrawDebugWindow();
			DrawDebugRawWindow();
		}

		protected void DrawMainWindow()
		{
			if( !MainWindowVisible )
			{
				return;
			}
		}

		protected void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 430, 140 ) * ImGui.GetIO().FontGlobalScale );
			if( ImGui.Begin( "Ready Check Helper Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Text( "Maximum number of names to show in chat:" );
				ImGui.SliderInt( "##MaxUnreadyNamesToShowInChat", ref mConfiguration.mMaxUnreadyToListInChat, 1, 48 );

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
						if( ImGui.Button( "Show/Hide Raw Readycheck Data" ) ) DebugRawWindowVisible = !DebugRawWindowVisible;
						ImGui.NextColumn();
						ImGui.Text( "Ready Check Data:" );
						for( int i = 0; i < readyCheckdata.Length; ++i )
						{
							ImGui.Text( $"State: {readyCheckdata[i].ReadyFlag}, ID: {readyCheckdata[i].ID.ToString( "X16" )}" );
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
								ImGui.Text( "Party member returned as null pointer." );
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
		public void SetCurrentTerritoryTypeID( UInt16 ID )
		{
			if( ID != CurrentTerritoryTypeID )
			{
				InvalidateReadyCheck();
			}

			CurrentTerritoryTypeID = ID;
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

		protected Configuration mConfiguration;

		protected DataManager mDataManager;

		protected Dictionary<uint, string> JobDict { get; set; } = new Dictionary<uint, string>();

		protected bool ReadyCheckValid { get; set; }

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mMainWindowVisible = false;
		public bool MainWindowVisible
		{
			get { return mMainWindowVisible; }
			set { mMainWindowVisible = value; }
		}

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

		protected UInt16 CurrentTerritoryTypeID { get; set; }
	}
}