using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using ReadyCheckHelper.Resources;
using ReadyCheckHelper.Windows;

namespace ReadyCheckHelper
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static IChatGui Chat { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IDataManager DataManager { get; private set; } = null!;
        [PluginService] public static ITextureProvider Texture { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;

        private const string TextCommandName = "/pready";
        private readonly DalamudLinkPayload OpenReadyCheckWindowLink;

        public Configuration Configuration { get; init; }

        public readonly WindowSystem WindowSystem = new("ReadyCheckHelper");
        public ConfigWindow ConfigWindow { get; init; }
        public ResultWindow ResultWindow { get; init; }
        public DebugWindow DebugWindow { get; init; }
        public ProcessedWindow ProcessedWindow { get; init; }
        public PartyListOverlay PartyListOverlay { get; init; }

        private readonly List<uint> InstancedTerritories = [];
        private List<CorrelatedReadyCheckEntry> ProcessedReadyCheckData = [];
        private CancellationTokenSource? TimedOverlayCancellationSource;
        public bool ReadyCheckActive { get; private set; }

        public readonly MemoryHandler MemoryHandler;

        public Plugin()
        {
            //	Configuration
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            //	Localization and Command Initialization
            LanguageChanged(PluginInterface.UiLanguage);
            OpenReadyCheckWindowLink = Chat.AddChatLinkHandler(1001, (i, m) => { ShowBestAvailableReadyCheckWindow(); });

            MemoryHandler = new MemoryHandler();

            //	UI Initialization
            ConfigWindow = new ConfigWindow(this);
            ResultWindow = new ResultWindow(this);
            DebugWindow = new DebugWindow(this);
            ProcessedWindow = new ProcessedWindow(this);
            PartyListOverlay = new PartyListOverlay(this);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(ResultWindow);
            WindowSystem.AddWindow(DebugWindow);
            WindowSystem.AddWindow(ProcessedWindow);
            WindowSystem.AddWindow(PartyListOverlay);
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            //	Misc.
            PopulateInstancedTerritoriesList();

            //	Event Subscription
            PluginInterface.LanguageChanged += LanguageChanged;
            Condition.ConditionChange += OnConditionChanged;
            ClientState.TerritoryChanged += OnTerritoryChanged;
            ClientState.Logout += OnLogout;
            Framework.Update += OnGameFrameworkUpdate;
            MemoryHandler.ReadyCheckInitiatedEvent += OnReadyCheckInitiated;
            MemoryHandler.ReadyCheckCompleteEvent += OnReadyCheckCompleted;
        }

        public void Dispose()
        {
            MemoryHandler.ReadyCheckInitiatedEvent -= OnReadyCheckInitiated;
            MemoryHandler.ReadyCheckCompleteEvent -= OnReadyCheckCompleted;
            Framework.Update -= OnGameFrameworkUpdate;
            ClientState.Logout -= OnLogout;
            ClientState.TerritoryChanged -= OnTerritoryChanged;
            Condition.ConditionChange -= OnConditionChanged;

            MemoryHandler.Dispose();

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            PluginInterface.LanguageChanged -= LanguageChanged;
            Chat.RemoveChatLinkHandler();
            CommandManager.RemoveHandler(TextCommandName);

            WindowSystem.RemoveAllWindows();
            ConfigWindow.Dispose();
            ResultWindow.Dispose();
            DebugWindow.Dispose();
            ProcessedWindow.Dispose();
            PartyListOverlay.Dispose();

            InstancedTerritories.Clear();
            TimedOverlayCancellationSource?.Dispose();
        }

        public void LanguageChanged(string langCode)
        {
            Language.Culture = new CultureInfo(langCode);

            //	Set up the command handler with the current language.
            if (CommandManager.Commands.ContainsKey(TextCommandName))
                CommandManager.RemoveHandler(TextCommandName);

            CommandManager.AddHandler(TextCommandName, new CommandInfo(ProcessTextCommand)
            {
                HelpMessage = Language.PluginTextCommandDescription.Format("'pready config'"),
            });
        }

        private void ProcessTextCommand(string command, string args)
        {
            //*****TODO: Don't split, just substring off of the first space so that other stuff is preserved verbatim.
            //	Seperate into sub-command and paramters.
            var subCommand = "";
            var subCommandArgs = "";
            var argsArray = args.Split(' ');
            if (argsArray.Length > 0)
                subCommand = argsArray[0];

            if (argsArray.Length > 1)
            {
                //	Recombine because there might be spaces in JSON or something, that would make splitting it bad.
                for (var i = 1; i < argsArray.Length; ++i)
                {
                    subCommandArgs += argsArray[i] + ' ';
                }

                subCommandArgs = subCommandArgs.Trim();
            }

            //	Process the commands.
            var suppressResponse = Configuration.SuppressCommandLineResponses;
            var commandResponse = "";
            if (subCommand.Length == 0)
            {
                ConfigWindow.Toggle();
            }
            else if (subCommand.Equals("config", StringComparison.CurrentCultureIgnoreCase))
            {
                ConfigWindow.Toggle();
            }
            else if (subCommand.Equals("debug", StringComparison.CurrentCultureIgnoreCase))
            {
                DebugWindow.Toggle();
            }
            else if (subCommand.Equals("results", StringComparison.CurrentCultureIgnoreCase))
            {
                ResultWindow.Toggle();
            }
            else if (subCommand.Equals("clear", StringComparison.CurrentCultureIgnoreCase))
            {
                PartyListOverlay.InvalidateReadyCheck();
            }
            else if (subCommand.Equals("help", StringComparison.CurrentCultureIgnoreCase) || subCommand.Equals("?", StringComparison.CurrentCultureIgnoreCase))
            {
                commandResponse = ProcessTextCommand_Help(subCommandArgs);
                suppressResponse = false;
            }
            else
            {
                commandResponse = ProcessTextCommand_Help(subCommandArgs);
            }

            //	Send any feedback to the user.
            if (commandResponse.Length > 0 && !suppressResponse)
                Chat.Print(commandResponse);
        }

        private string ProcessTextCommand_Help(string args)
        {
            return args.ToLower() switch
            {
                "config" => Language.ConfigSubcommandHelpMessage,
                "results" => Language.ResultsSubcommandHelpMessage,
                "clear" => Language.ClearSubcommandHelpMessage,
                "debug" => Language.DebugSubcommandHelpMessage,
                _ => Language.BasicHelpMessage.Format("'config'", "'results'", "'clear'", "/pready help"),
            };
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        private void DrawConfigUI()
        {
            ConfigWindow.Toggle();
        }

        private void OnGameFrameworkUpdate(IFramework framework)
        {
            if (ClientState.IsLoggedIn && ReadyCheckActive)
                ProcessReadyCheckResults();
        }

        private void OnReadyCheckInitiated(object? _, EventArgs __)
        {
            //	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
            if (!ClientState.IsLoggedIn)
                return;

            //	Flag that we should start processing the data every frame.
            ReadyCheckActive = true;
            PartyListOverlay.ShowReadyCheckOverlay();
            TimedOverlayCancellationSource?.Cancel();
        }

        private void OnReadyCheckCompleted(object? _, EventArgs __)
        {
            //	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
            if (!ClientState.IsLoggedIn)
                return;

            //	Flag that we don't need to keep updating.
            ReadyCheckActive = false;
            PartyListOverlay.ShowReadyCheckOverlay();

            //	Process the data one last time to ensure that we have the latest results.
            ProcessReadyCheckResults();

            //	Construct a list of who's not ready.
            var notReadyList = new List<string>();
            foreach (var person in ProcessedReadyCheckData)
                if (person.ReadyState is ReadyCheckStatus.NotReady or ReadyCheckStatus.MemberNotPresent)
                    notReadyList.Add(person.Name);

            //	Print it to chat in the desired format.
            if (Configuration.ShowReadyCheckResultsInChat)
                ListUnreadyPlayersInChat(notReadyList);

            //	Start a task to clean up the icons on the party chat after the configured amount of time.
            if (Configuration.ClearReadyCheckOverlayAfterTime)
            {
                TimedOverlayCancellationSource = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    var delaySec = Math.Max(0, Math.Min(Configuration.TimeUntilClearReadyCheckOverlay_Sec, 900)); //	Just to be safe...
                    try
                    {
                        await Task.Delay(delaySec * 1000, TimedOverlayCancellationSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    finally
                    {
                        TimedOverlayCancellationSource?.Dispose();
                        TimedOverlayCancellationSource = null;
                    }

                    if (!ReadyCheckActive)
                        PartyListOverlay.InvalidateReadyCheck();
                });
            }
        }

        private unsafe void ProcessReadyCheckResults()
        {
            var infoProxy = InfoProxyCrossRealm.Instance();
            var groupManager = GroupManager.Instance();
            if ((nint)infoProxy == nint.Zero)
                return;

            if ((nint)groupManager == nint.Zero)
                return;

            //	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
            //	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
            if (infoProxy->IsCrossRealm && groupManager->MainGroup.MemberCount < 1)
                ProcessReadyCheckResults_CrossWorld();
            else
                ProcessReadyCheckResults_Regular();
        }

        private unsafe void ProcessReadyCheckResults_Regular()
        {
            var groupManager = GroupManager.Instance();
            try
            {
                var readyCheckData = AgentReadyCheck.Instance()->ReadyCheckEntries;
                var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();
                var foundSelf = false;

                //	Grab all the alliance members here to make lookups easier since there's no function in client structs to get an alliance member by object ID.
                var allianceMemberDict = new Dictionary<uint, Tuple<ulong, string, byte, byte>>();
                for (var j = 0; j < 2; ++j)
                {
                    for (var i = 0; i < 8; ++i)
                    {
                        var pGroupMember = groupManager->MainGroup.GetAllianceMemberByGroupAndIndex(j, i);
                        if ((nint)pGroupMember != nint.Zero)
                        {
                            var name = pGroupMember->NameString;
                            allianceMemberDict.TryAdd(pGroupMember->EntityId, Tuple.Create(pGroupMember->ContentId, name, (byte)(j + 1), (byte)i));
                        }
                    }
                }

                //	Correlate all the ready check entries with party/alliance members.
                for (var i = 0; i < readyCheckData.Length; ++i)
                {
                    //	For our party, we need to do the correlation based on party data.
                    if (i < groupManager->MainGroup.MemberCount)
                    {
                        //	For your immediate, local party, ready check data seems to be correlated with the party index, but with you always first in the list (anyone with an index below yours will be offset by one).
                        var pFoundPartyMember = groupManager->MainGroup.GetPartyMemberByIndex(i);
                        if ((nint)pFoundPartyMember != nint.Zero)
                        {
                            var name = pFoundPartyMember->NameString;

                            //	If it's us, we need to use the first entry in the ready check data.
                            if (pFoundPartyMember->EntityId == ObjectTable.LocalPlayer?.EntityId)
                            {
                                readyCheckProcessedList.Insert(0, new CorrelatedReadyCheckEntry(name, (ulong)pFoundPartyMember->ContentId, pFoundPartyMember->EntityId, readyCheckData[0].Status, 0, 0));
                                foundSelf = true;
                            }
                            //	If it's before we've found ourselves, look ahead by one in the ready check data.
                            else if (!foundSelf)
                            {
                                readyCheckProcessedList.Add(new CorrelatedReadyCheckEntry(name, (ulong)pFoundPartyMember->ContentId, pFoundPartyMember->EntityId, readyCheckData[i + 1].Status, 0, (byte)(i + 1)));
                            }
                            //	Otherwise, use the same index in the ready check data.
                            else
                            {
                                readyCheckProcessedList.Add(new CorrelatedReadyCheckEntry(name, (ulong)pFoundPartyMember->ContentId, pFoundPartyMember->EntityId, readyCheckData[i].Status, 0, (byte)i));
                            }
                        }
                    }
                    //	For the alliance members, there should be object IDs to make matching easy.
                    else if (readyCheckData[i].ContentId > 0 && (readyCheckData[i].ContentId & 0xFFFFFFFF) != 0xE0000000)
                    {
                        if (allianceMemberDict.TryGetValue((uint)readyCheckData[i].ContentId, out var temp))
                            readyCheckProcessedList.Add(new CorrelatedReadyCheckEntry(temp.Item2, temp.Item1, (uint)readyCheckData[i].ContentId, readyCheckData[i].Status, temp.Item3, temp.Item4));
                    }
                    //***** TODO: How do things work if you're a non-cross-world alliance without people in the same zone? *****
                    //This isn't possible through PF; is it still possible in the open world?
                }

                //	Assign to the persistent list if we've gotten through this without any problems.
                ProcessedReadyCheckData = readyCheckProcessedList;
            }
            catch (Exception e)
            {
                Log.Debug($"Exception caught in \"ProcessReadyCheckResults_Regular()\": {e}.");
            }
        }

        private unsafe void ProcessReadyCheckResults_CrossWorld()
        {
            try
            {
                var readyCheckData = AgentReadyCheck.Instance()->ReadyCheckEntries;
                var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();

                foreach (var readyCheckEntry in readyCheckData)
                {
                    CrossRealmMember* pFoundPartyMember;
                    if (readyCheckEntry.ContentId > uint.MaxValue)
                        pFoundPartyMember = InfoProxyCrossRealm.GetMemberByContentId(readyCheckEntry.ContentId);
                    else
                        pFoundPartyMember = InfoProxyCrossRealm.GetMemberByEntityId((uint)readyCheckEntry.ContentId);

                    if ((nint)pFoundPartyMember != nint.Zero)
                    {
                        var name = pFoundPartyMember->NameString;
                        readyCheckProcessedList.Add(new CorrelatedReadyCheckEntry(name, pFoundPartyMember->ContentId, pFoundPartyMember->EntityId, readyCheckEntry.Status, pFoundPartyMember->GroupIndex, pFoundPartyMember->MemberIndex));
                    }
                }

                //	Assign to the persistent list if we've gotten through this without any problems.
                ProcessedReadyCheckData = readyCheckProcessedList;
            }
            catch (Exception e)
            {
                Log.Debug($"Exception caught in \"ProcessReadyCheckResults_CrossWorld()\": {e}.");
            }
        }

        public void ListUnreadyPlayersInChat(List<string> notReadyList)
        {
            if (notReadyList.Count > 0)
            {
                var notReadyString = LocalizationHelpers.ConstructNotReady(notReadyList, Configuration.MaxUnreadyToListInChat);

                //	If we don't delay the actual printing to chat, sometimes it comes out before the system message in the chat log.  I don't understand why it's an issue, but this is an easy kludge to make it work right consistently.
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    var chatEntry = new XivChatEntry
                    {
                        Type = Configuration.ChatChannelToUseForNotReadyMessage,
                        Message = new SeStringBuilder().Add(OpenReadyCheckWindowLink).AddText(notReadyString).Add(RawPayload.LinkTerminator).BuiltString,
                    };

                    Chat.Print(chatEntry);
                });
            }
        }

        private void ShowBestAvailableReadyCheckWindow()
        {
            ResultWindow.IsOpen = true;
        }

        private void OnConditionChanged(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.InCombat)
            {
                if (value)
                {
                    if (Configuration.ClearReadyCheckOverlayInCombat)
                        PartyListOverlay.InvalidateReadyCheck();
                    else if (Configuration.ClearReadyCheckOverlayInCombatInInstancedCombat && InstancedTerritories.Contains(ClientState.TerritoryType))
                        PartyListOverlay.InvalidateReadyCheck();
                }
            }
        }

        private void OnTerritoryChanged(uint id)
        {
            if (Configuration.ClearReadyCheckOverlayEnteringInstance && InstancedTerritories.Contains(id))
                PartyListOverlay.InvalidateReadyCheck();
        }

        private void OnLogout(int _, int __)
        {
            ReadyCheckActive = false;
            TimedOverlayCancellationSource?.Cancel();
            PartyListOverlay.InvalidateReadyCheck();
            ProcessedReadyCheckData.Clear();
        }

        internal List<CorrelatedReadyCheckEntry> GetProcessedReadyCheckData()
        {
            return ProcessedReadyCheckData;
        }

        private void PopulateInstancedTerritoriesList()
        {
            var contentFinderSheet = DataManager.GetExcelSheet<ContentFinderCondition>()!;
            foreach (var zone in contentFinderSheet)
                InstancedTerritories.Add(zone.TerritoryType.RowId);
        }
    }
}