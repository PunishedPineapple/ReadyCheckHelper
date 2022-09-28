using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Numerics;

namespace ReadyCheckHelper
{
	[Serializable]
	public class Configuration : IPluginConfiguration
	{
		public Configuration()
		{
		}

		//  Our own configuration options and data.

		//	Need a real backing field on the properties for use with ImGui.
		public bool mSuppressCommandLineResponses = false;
		public bool SuppressCommandLineResponses
		{
			get { return mSuppressCommandLineResponses; }
			set { mSuppressCommandLineResponses = value; }
		}

		public bool mShowReadyCheckResultsInChat = true;
		public bool ShowReadyCheckResultsInChat
		{
			get { return mShowReadyCheckResultsInChat; }
			set { mShowReadyCheckResultsInChat = value; }
		}

		public int mMaxUnreadyToListInChat = 3;
		public int MaxUnreadyToListInChat
		{
			get { return mMaxUnreadyToListInChat; }
			set { mMaxUnreadyToListInChat = value; }
		}

		public bool mShowReadyCheckOnPartyAllianceList = true;
		public bool ShowReadyCheckOnPartyAllianceList
		{
			get { return mShowReadyCheckOnPartyAllianceList; }
			set { mShowReadyCheckOnPartyAllianceList = value; }
		}

		public bool mUseImGuiForPartyAllianceIcons = true;
		public bool UseImGuiForPartyAllianceIcons
		{
			get { return mUseImGuiForPartyAllianceIcons; }
			set { mUseImGuiForPartyAllianceIcons = value; }
		}

		public bool mClearReadyCheckOverlayEnteringInstance = true;
		public bool ClearReadyCheckOverlayEnteringInstance
		{
			get { return mClearReadyCheckOverlayEnteringInstance; }
			set { mClearReadyCheckOverlayEnteringInstance = value; }
		}

		public bool mClearReadyCheckOverlayInCombat = false;
		public bool ClearReadyCheckOverlayInCombat
		{
			get { return mClearReadyCheckOverlayInCombat; }
			set { mClearReadyCheckOverlayInCombat = value; }
		}

		public bool mClearReadyCheckOverlayInCombatInInstancedCombat = true;
		public bool ClearReadyCheckOverlayInCombatInInstancedCombat
		{
			get { return mClearReadyCheckOverlayInCombatInInstancedCombat; }
			set { mClearReadyCheckOverlayInCombatInInstancedCombat = value; }
		}

		public bool mClearReadyCheckOverlayAfterTime = false;
		public bool ClearReadyCheckOverlayAfterTime
		{
			get { return mClearReadyCheckOverlayAfterTime; }
			set { mClearReadyCheckOverlayAfterTime = value; }
		}

		public int mTimeUntilClearReadyCheckOverlay_Sec = 60;
		public int TimeUntilClearReadyCheckOverlay_Sec
		{
			get { return mTimeUntilClearReadyCheckOverlay_Sec; }
			set { mTimeUntilClearReadyCheckOverlay_Sec = value; }
		}

		public Vector2 mPartyListIconOffset = Vector2.Zero;
		public Vector2 PartyListIconOffset
		{
			get { return mPartyListIconOffset; }
			set { mPartyListIconOffset = value; }
		}

		public Vector2 mAllianceListIconOffset = Vector2.Zero;
		public Vector2 AllianceListIconOffset
		{
			get { return mAllianceListIconOffset; }
			set { mAllianceListIconOffset = value; }
		}

		public Vector2 mCrossWorldAllianceListIconOffset = Vector2.Zero;
		public Vector2 CrossWorldAllianceListIconOffset
		{
			get { return mCrossWorldAllianceListIconOffset; }
			set { mCrossWorldAllianceListIconOffset = value; }
		}

		public float mPartyListIconScale = 1f;
		public float PartyListIconScale
		{
			get { return mPartyListIconScale; }
			set { mPartyListIconScale = value; }
		}

		public float mAllianceListIconScale = 1f;
		public float AllianceListIconScale
		{
			get { return mAllianceListIconScale; }
			set { mAllianceListIconScale = value; }
		}

		public float mCrossWorldAllianceListIconScale = 1f;
		public float CrossWorldAllianceListIconScale
		{
			get { return mCrossWorldAllianceListIconScale; }
			set { mCrossWorldAllianceListIconScale = value; }
		}

		//	Backing field as an int to work with ImGui.
		public int mChatChannelToUseForNotReadyMessage = (int)Dalamud.Game.Text.XivChatType.SystemMessage;
		public Dalamud.Game.Text.XivChatType ChatChannelToUseForNotReadyMessage
		{
			get { return (Dalamud.Game.Text.XivChatType)mChatChannelToUseForNotReadyMessage; }
			set { mChatChannelToUseForNotReadyMessage = (int)value; }
		}

		//  Plugin framework and related convenience functions below.
		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			mPluginInterface = pluginInterface;
		}

		public void Save()
		{
			mPluginInterface.SavePluginConfig( this );
		}

		[NonSerialized]
		protected DalamudPluginInterface mPluginInterface;

		public int Version { get; set; } = 0;
	}
}
