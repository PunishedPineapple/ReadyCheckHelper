using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ReadyCheckHelper.Resources;

namespace ReadyCheckHelper.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    public ConfigWindow(Plugin plugin) : base($"{Language.WindowTitleConfig}###Ready Check Helper Settings")
    {
        Plugin = plugin;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.Checkbox($"{Language.ConfigOptionPrintNamesofUnreadyinChat}###List unready names in chat.", ref Plugin.Configuration.mShowReadyCheckResultsInChat);

        if (Plugin.Configuration.ShowReadyCheckResultsInChat)
        {
            using var indent = ImRaii.PushIndent();
            ImGui.Text(Language.ConfigOptionMaxNamesinChat);
            ImGui.SliderInt("##MaxUnreadyNamesToShowInChat", ref Plugin.Configuration.mMaxUnreadyToListInChat, 1, 48);

            ImGuiHelpers.ScaledDummy(2.0f);

            ImGui.Text(Language.ConfigOptionChatMessageChannel);
            Helper.ImGuiHelpMarker(Language.HelpChatMessageChannel.Format(LocalizationHelpers.GetTranslatedChatChannelName(XivChatType.SystemMessage)));
            using (var combo = ImRaii.Combo("###MessageChannelDropdown", LocalizationHelpers.GetTranslatedChatChannelName(Plugin.Configuration.ChatChannelToUseForNotReadyMessage)))
            {
                if (combo.Success)
                {
                    foreach (var entry in Enum.GetValues<XivChatType>())
                    {
                        if (entry.IsUsedByGm() || entry.AppliesRelationKind())
                            continue;

                        if (ImGui.Selectable(LocalizationHelpers.GetTranslatedChatChannelName(entry)))
                            Plugin.Configuration.ChatChannelToUseForNotReadyMessage = entry;
                    }
                }
            }

            ImGuiHelpers.ScaledDummy(5.0f);
        }

        ImGui.Checkbox(Language.ConfigOptionDrawonPartyAllianceLists, ref Plugin.Configuration.mShowReadyCheckOnPartyAllianceList);

        if (Plugin.Configuration.ShowReadyCheckOnPartyAllianceList)
        {
            using var indent = ImRaii.PushIndent();
            ImGui.Text(Language.ConfigOptionClearPartyAllianceListSettings);
            ImGui.Checkbox($"{Language.ConfigOptionClearPartyAllianceListuponEnteringCombat}###Upon entering combat.", ref Plugin.Configuration.mClearReadyCheckOverlayInCombat);
            ImGui.Checkbox($"{Language.ConfigOptionClearPartyAllianceListuponEnteringInstance}###Upon entering instance.", ref Plugin.Configuration.mClearReadyCheckOverlayEnteringInstance);
            ImGui.Checkbox($"{Language.ConfigOptionClearPartyAllianceListuponEnteringingCombatinInstance}###Upon entering combat while in instance.", ref Plugin.Configuration.mClearReadyCheckOverlayInCombatInInstancedCombat);
            ImGui.Checkbox($"{Language.ConfigOptionClearPartyAllianceListafterXSeconds}###After X seconds.", ref Plugin.Configuration.mClearReadyCheckOverlayAfterTime);
            Helper.ImGuiHelpMarker(Language.HelpClearPartyAllianceListafterXSeconds);
            ImGui.DragInt("###TimeUntilClearOverlaySlider", ref Plugin.Configuration.mTimeUntilClearReadyCheckOverlay_Sec, 1.0f, 30, 900, "%d", ImGuiSliderFlags.AlwaysClamp);

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.Text(Language.ConfigSectionIconSizeOffset);
            ImGui.DragFloat2($"{Language.ConfigOptionPartyListIconOffset}###PartyListIconOffset", ref Plugin.Configuration.mPartyListIconOffset, 1f, -100f, 100f);
            ImGui.DragFloat($"{Language.ConfigOptionPartyListIconScale}###PartyListIconScale", ref Plugin.Configuration.mPartyListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp);
            ImGui.DragFloat2($"{Language.ConfigOptionAllianceListIconOffset}###AllianceListIconOffset", ref Plugin.Configuration.mAllianceListIconOffset, 1f, -100f, 100f);
            ImGui.DragFloat($"{Language.ConfigOptionAllianceListIconScale}###AllianceListIconScale", ref Plugin.Configuration.mAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp);
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button(Language.ButtonSaveandClose))
        {
            Plugin.Configuration.Save();
            IsOpen = false;
        }
    }
}