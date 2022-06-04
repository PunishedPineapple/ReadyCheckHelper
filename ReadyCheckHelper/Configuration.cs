using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;

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
