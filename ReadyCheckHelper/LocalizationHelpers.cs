using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dalamud;
using Dalamud.Data;
using Dalamud.Game.Text;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace ReadyCheckHelper
{
	internal static class LocalizationHelpers
	{
		//***** TODO: Requires severe proofreading.  Inferred from Google Translate, could be totally butchered. *****
		internal static string ConstructNotReadyString_ja( List<string> notReadyList, int maxUnreadyToList )
		{
			var trimmedList = new List<string>( notReadyList.Take( maxUnreadyToList ) );
			int numExtra = Math.Max( 0, notReadyList.Count - trimmedList.Count );
			if( numExtra > 1 )
			{
				trimmedList.Add( $"その他{numExtra}人" );
			}
			else if( numExtra == 1 )
			{
				trimmedList.Add( $"その他" );
			}

			string notReadyString = "×：";

			for( int i = 0; i < trimmedList.Count; ++i )
			{
				//	If there's only one person, just put their name.
				if( trimmedList.Count == 1 )
				{
					notReadyString += trimmedList[i];
				}
				//	Otherwise, if we're the first item of a two-item list that's two names, put a joiner.
				else if( trimmedList.Count == 2 && notReadyList.Count == 2 && maxUnreadyToList >= 2 && i == 0 )
				{
					notReadyString += $"{trimmedList[i]}と";
				}
				//	Otherwise, if we're at the final item, no comma after
				else if( i == trimmedList.Count - 1 )
				{
					notReadyString += trimmedList[i];
				}
				//	Otherwise, comma separate the list.
				else
				{
					notReadyString += $"{trimmedList[i]}、";
				}
			}

			return notReadyString;
		}

		internal static string ConstructNotReadyString_en( List<string> notReadyList, int maxUnreadyToList )
		{
			var trimmedList = new List<string>( notReadyList.Take( maxUnreadyToList ) );
			int numExtra = Math.Max( 0, notReadyList.Count - trimmedList.Count );
			if( numExtra > 0 )
			{
				trimmedList.Add( $"{numExtra} {( numExtra > 1 ? "others" : "other" )}" );
			}

			string notReadyString = "Not Ready: ";

			for( int i = 0; i < trimmedList.Count; ++i )
			{
				//	If there's only one person, just put their name.
				if( trimmedList.Count == 1 )
				{
					notReadyString += trimmedList[i];
				}
				//	Otherwise, if we're the first item of a two-item list, no comma separation.
				else if( trimmedList.Count == 2 && i == 0 )
				{
					notReadyString += $"{trimmedList[i]} ";
				}
				//	Otherwise, if we're at the final item, put the joiner before it.
				else if( i == trimmedList.Count - 1 )
				{
					notReadyString += $"and {trimmedList[i]}";
				}
				//	Otherwise, comma separate the list.
				else
				{
					notReadyString += $"{trimmedList[i]}, ";
				}
			}

			return notReadyString;
		}

		//***** TODO: Requires proofreading. *****
		internal static string ConstructNotReadyString_de( List<string> notReadyList, int maxUnreadyToList )
		{
			var trimmedList = new List<string>( notReadyList.Take( maxUnreadyToList ) );
			int numExtra = Math.Max( 0, notReadyList.Count - trimmedList.Count );
			if( numExtra > 0 )
			{
				trimmedList.Add( $"{numExtra} andere" );
			}

			string notReadyString = "Nicht bereit: ";

			for( int i = 0; i < trimmedList.Count; ++i )
			{
				//	If there's only one person, just put their name.
				if( trimmedList.Count == 1 )
				{
					notReadyString += trimmedList[i];
				}
				//	Otherwise, if we've reached the second to last item, no Oxford comma in this language
				else if( i == trimmedList.Count - 2 )
				{
					notReadyString += $"{trimmedList[i]} ";
				}
				//	Otherwise, if we're at the final item, put the joiner before it.
				else if( i == trimmedList.Count - 1 )
				{
					notReadyString += $"und {trimmedList[i]}";
				}
				//	Otherwise, comma separate the list.
				else
				{
					notReadyString += $"{trimmedList[i]}, ";
				}
			}

			return notReadyString;
		}

		internal static string ConstructNotReadyString_fr( List<string> notReadyList, int maxUnreadyToList )
		{
			var trimmedList = new List<string>( notReadyList.Take( maxUnreadyToList ) );
			int numExtra = Math.Max( 0, notReadyList.Count - trimmedList.Count );
			if( numExtra > 0 )
			{
				trimmedList.Add( $"{numExtra} {( numExtra > 1 ? "autres" : "autre" )}" );
			}

			string notReadyString = "Non prêts : ";

			for( int i = 0; i < trimmedList.Count; ++i )
			{
				//	If there's only one person, just put their name.
				if( trimmedList.Count == 1 )
				{
					notReadyString += trimmedList[i];
				}
				//	Otherwise, if we've reached the second to last item, no Oxford comma in this language
				else if( i == trimmedList.Count - 2 )
				{
					notReadyString += $"{trimmedList[i]} ";
				}
				//	Otherwise, if we're at the final item, put the joiner before it.
				else if( i == trimmedList.Count - 1 )
				{
					notReadyString += $"et {trimmedList[i]}";
				}
				//	Otherwise, comma separate the list.
				else
				{
					notReadyString += $"{trimmedList[i]}, ";
				}
			}

			return notReadyString;
		}

		//***** TODO: Requires proofreading.  Adapted changes from Dalamud KR fork, but could be incomplete. *****
		internal static string ConstructNotReadyString_ko( List<string> notReadyList, int maxUnreadyToList )
		{
			var trimmedList = new List<string>( notReadyList.Take( maxUnreadyToList ) );
			int numExtra = Math.Max( 0, notReadyList.Count - trimmedList.Count );
			if( numExtra > 0 )
			{
				trimmedList.Add( $"외 {numExtra} 명" );
			}

			string notReadyString = "준비 안됨: ";

			for( int i = 0; i < trimmedList.Count; ++i )
			{
				//	If it's the last entry in the list, just put it.
				if( i == trimmedList.Count - 1 )
				{
					notReadyString += trimmedList[i];
				}
				//	Otherwise, comma separate the list.
				else
				{
					notReadyString += $"{trimmedList[i]}, ";
				}
			}

			return notReadyString;
		}

		//***** TODO *****
		internal static string ConstructNotReadyString_zh( List<string> notReadyList, int maxUnreadyToList )
		{
			return ConstructNotReadyString_en( notReadyList, maxUnreadyToList );
		}

		internal static void Init( DataManager dataManager )
		{
			mLogFilterSheet = dataManager.GetExcelSheet<LogFilter>();
		}

		internal static void Uninit()
		{
			mLogFilterSheet = null;
		}

		internal static string GetTranslatedChatChannelName( XivChatType channel )
		{
			var exdRow = mLogFilterSheet?.FirstOrDefault( x => { return x.LogKind == ((byte)channel & 0x7F ); } );
			return exdRow != null ? exdRow.Name.ToString() : Enum.GetName( typeof( XivChatType ), channel );
		}

		private static ExcelSheet<LogFilter> mLogFilterSheet;

		internal static readonly string[] TestNames = new string[]
		{
			"Cloud Strife",
			"Terra Branford",
			"Zidane Tribal",
			"Celes Chere",
			"Faris Scherwiz",
			"Sazh Katzroy",
			"Claire Farron",
			"Locke Cole",

			"Setzer Gabbiani",
			"Tifa Lockhart",
			"Edgar Figaro",
			"Aerith Gainsborough",
			"Cid Highwind",
			"Barret Wallace",
			"Rinoa Heartilly",
			"Squall Leonhart",

			"Freya Crescent",
			"Adelbert Steiner",
			"Quina Quen",
			"Vivi Ornitier",
			"Eiko Carol",
			"Cecil Harvey",
			"Kain Highwind",
			"Cid Pollendina"
		};
	}
}
