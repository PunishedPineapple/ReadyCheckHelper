using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using ReadyCheckHelper.Resources;

namespace ReadyCheckHelper;

internal static class LocalizationHelpers
{
    internal static string ConstructNotReady(List<string> notReadyList, int maxUnreadyToList)
    {
        var trimmedList = new List<string>(notReadyList.Take(maxUnreadyToList));
        var numExtra = Math.Max(0, notReadyList.Count - trimmedList.Count);
        if( numExtra > 0 )
            trimmedList.Add($"{numExtra} {(numExtra > 1 ? Language.NotReadyOthers : Language.NotReadyOther)}");

        var notReadyString = Language.NotReadyBase;

        for(var i = 0; i < trimmedList.Count; i++)
        {
            notReadyString += trimmedList[i];
            if (i + 1 < trimmedList.Count)
                notReadyString += ", ";
        }

        return notReadyString;
    }

    internal static string GetTranslatedChatChannelName(XivChatType channel)
    {
        var logFilterSheet = Plugin.DataManager.GetExcelSheet<LogFilter>();
        var filterRow = logFilterSheet.FirstOrNull(x => x.LogKind == (ushort)channel);

        return filterRow != null
            ? filterRow.Value.Name.ExtractText()
            : Enum.GetName(channel) ?? "Unknown";
    }

    internal static readonly string[] TestNames =
    [
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
    ];
}