using System;
using CheapLoc;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace ReadyCheckHelper.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    public ConfigWindow(Plugin plugin) : base($"{Loc.Localize("Window Title: Config", "Ready Check Helper Settings")}###Ready Check Helper Settings")
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
        ImGui.Checkbox($"{Loc.Localize("Config Option: Print Names of Unready in Chat", "Show the names of those not ready in the chat window.")}###List unready names in chat.", ref Plugin.Configuration.mShowReadyCheckResultsInChat);

        if (Plugin.Configuration.ShowReadyCheckResultsInChat)
        {
            ImGui.Spacing();

            ImGui.Indent();
            ImGui.Text(Loc.Localize("Config Option: Max Names in Chat", "Maximum number of names to show in chat:"));
            ImGui.SliderInt("##MaxUnreadyNamesToShowInChat", ref Plugin.Configuration.mMaxUnreadyToListInChat, 1, 48);

            ImGui.Spacing();

            ImGui.Text(Loc.Localize("Config Option: Chat Message Channel", "Chat Log Channel:"));
            Helper.ImGuiHelpMarker(string.Format(Loc.Localize("Help: Chat Message Channel", "Sets the channel in which this chat message is shown.  Leave this set to the default value ({0}) unless it causes problems with your chat configuration.  This only affects the unready players message; all other messages from this plugin respect your choice of chat channel in Dalamud settings."), LocalizationHelpers.GetTranslatedChatChannelName(Dalamud.Game.Text.XivChatType.SystemMessage)));
            using var combo = ImRaii.Combo("###NotReadyMessageChatChannelDropdown", LocalizationHelpers.GetTranslatedChatChannelName(Plugin.Configuration.ChatChannelToUseForNotReadyMessage));
            if (combo)
            {
                foreach (Dalamud.Game.Text.XivChatType entry in Enum.GetValues(typeof(Dalamud.Game.Text.XivChatType)))
                    if (ImGui.Selectable(LocalizationHelpers.GetTranslatedChatChannelName(entry)))
                        Plugin.Configuration.ChatChannelToUseForNotReadyMessage = entry;
            }

            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
        }

        ImGui.Checkbox($"{Loc.Localize("Config Option: Draw on Party Alliance Lists", "Draw ready check on party/alliance lists.")}###Draw ready check on party/alliance lists.", ref Plugin.Configuration.mShowReadyCheckOnPartyAllianceList);

        if (Plugin.Configuration.ShowReadyCheckOnPartyAllianceList)
        {
            ImGui.Spacing();

            ImGui.Indent();
            ImGui.Text(Loc.Localize("Config Option: Clear Party Alliance List Settings", "Clear ready check from party/alliance lists:"));
            ImGui.Checkbox($"{Loc.Localize("Config Option: Clear Party Alliance List upon Entering Combat", "Upon entering combat.")}###Upon entering combat.", ref Plugin.Configuration.mClearReadyCheckOverlayInCombat);
            ImGui.Checkbox($"{Loc.Localize("Config Option: Clear Party Alliance List upon Entering Instance", "Upon entering instance.")}###Upon entering instance.", ref Plugin.Configuration.mClearReadyCheckOverlayEnteringInstance);
            ImGui.Checkbox($"{Loc.Localize("Config Option: Clear Party Alliance List upon Enteringing Combat in Instance", "Upon entering combat while in instance.")}###Upon entering combat while in instance.", ref Plugin.Configuration.mClearReadyCheckOverlayInCombatInInstancedCombat);
            ImGui.Checkbox($"{Loc.Localize("Config Option: Clear Party Alliance List after X Seconds:", "After a certain number of seconds:")}###After X seconds.", ref Plugin.Configuration.mClearReadyCheckOverlayAfterTime);
            Helper.ImGuiHelpMarker(Loc.Localize("Help: Clear Party Alliance List after X Seconds", "Changes to this setting will not take effect until the next ready check concludes."));
            ImGui.DragInt("###TimeUntilClearOverlaySlider", ref Plugin.Configuration.mTimeUntilClearReadyCheckOverlay_Sec, 1.0f, 30, 900, "%d", ImGuiSliderFlags.AlwaysClamp);
            ImGui.Spacing();
            ImGui.Text(Loc.Localize("Config Section: Icon Size/Offset", "Party and Alliance List Icon Size/Offset:"));
            ImGui.DragFloat2($"{Loc.Localize("Config Option: Party List Icon Offset", "Party List Icon Offset")}###PartyListIconOffset", ref Plugin.Configuration.mPartyListIconOffset, 1f, -100f, 100f);
            ImGui.DragFloat($"{Loc.Localize("Config Option: Party List Icon Scale", "Party List Icon Scale")}###PartyListIconScale", ref Plugin.Configuration.mPartyListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp);
            ImGui.DragFloat2($"{Loc.Localize("Config Option: Alliance List Icon Offset", "Alliance List Icon Offset")}###AllianceListIconOffset", ref Plugin.Configuration.mAllianceListIconOffset, 1f, -100f, 100f);
            ImGui.DragFloat($"{Loc.Localize("Config Option: Alliance List Icon Scale", "Alliance List Icon Scale")}###AllianceListIconScale", ref Plugin.Configuration.mAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp);
            //ImGui.DragFloat2( Loc.Localize( "Config Option: Cross-World Alliance List Icon Offset", "Cross-World Alliance List Icon Offset" ) + "###CrossWorldAllianceListIconOffset", ref Configuration.mCrossWorldAllianceListIconOffset, 1f, -100f, 100f );
            //ImGui.DragFloat( Loc.Localize( "Config Option: Cross-World Alliance List Icon Scale", "Cross-World Alliance List Icon Scale" ) + "###CrossWorldAllianceListIconScale", ref Configuration.mCrossWorldAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%d", ImGuiSliderFlags.AlwaysClamp );
            ImGui.Unindent();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.Button($"{Loc.Localize("Button: Save and Close", "Save and Close")}"))
        {
            Plugin.Configuration.Save();
            IsOpen = false;
        }
    }
}