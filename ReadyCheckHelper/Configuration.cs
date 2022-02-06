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

		public int mMaxUnreadyToListInChat = 3;
		public int MaxUnreadyToListInChat
		{
			get { return mMaxUnreadyToListInChat; }
			set { mMaxUnreadyToListInChat = value; }
		}

		public bool mShowReadyCheckOnPartyAllianceList = false;
		public bool ShowReadyCheckOnPartyAllianceList
		{
			get { return mShowReadyCheckOnPartyAllianceList; }
			set { mShowReadyCheckOnPartyAllianceList = value; }
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
