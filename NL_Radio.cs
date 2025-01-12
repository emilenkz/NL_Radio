using Life;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using static ModKit.Helper.TextFormattingHelper;
using _menu = AAMenu.Menu;

namespace NL_Radio
{
    public class NL_Radio : ModKit.ModKit
    {
        private FrequenceManager FrequenceManager;

        public NL_Radio(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Emile_Nkz");
            FrequenceManager = new FrequenceManager(this);
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            Orm.RegisterTable<RadioChannels>();
            InitPlugin();
            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }
        public void InitPlugin()
        {
            //AAMenu
            _menu.AddInteractionTabLine(PluginInformations, "Radio", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                OpenMainRadioPanel(player);
            });

            //Command
            SChatCommand command = new SChatCommand("/radio", "Ouvre le menu principal de votre radio", "/radio", (player, arg) =>
            {
                OpenMainRadioPanel(player);
            });
            command.Register();
        }
        public override void OnPlayerInput(Player player, KeyCode keyCode, bool onUI)
        {
            base.OnPlayerInput(player, keyCode, onUI);
            switch (keyCode)
            {
                case KeyCode.X:
                    HandleRadioCommunication(player);
                    break;
            }
        }

        private static void HandleRadioCommunication(Player player)
        {
            RadioState state = RadioManager.GetRadioState(player);
            if (state.IsSpeak && state.IsActive)
            {
                List<Player> players = RadioManager.GetPlayersInFrequency(state.Frequency);
                foreach (Player p in players)
                {
                    p.setup.TargetPlayClaironById(0.1f, 1);
                    p.Notify("Radio", $"{Color(player.GetFullName(), Colors.Success)} est en train de communiquer à la radio.", NotificationManager.Type.Success, 3);
                }
                RadioManager.ResyncRadioPlayerStates(state.Frequency);
            }
        }
        public async void OpenMainRadioPanel(Player player)
        {
            // Query
            RadioState state = RadioManager.GetRadioState(player);

            // Déclaration
            Panel panel = PanelHelper.Create("Accueil Radio", UIPanel.PanelType.TabPrice, player, () => OpenMainRadioPanel(player));

            // Corps
            panel.AddTabLine($"{(state.IsActive ? Color("Statut : Activé", Colors.Success) : Color("Statut : Désactivé", Colors.Error))}", "", ItemUtils.GetIconIdByItemId(1336), (ui) =>
            {
                RadioManager.GetPlayersInFrequency(state.Frequency)
                    .Where(p => p != player)
                    .ToList()
                    .ForEach(p => p.Notify("Radio", $"{player.GetFullName()} vient de quitter la  {state.Frequency}.", NotificationManager.Type.Success));

                state.IsActive = false;
                state.Frequency = 0;
                RadioManager.SetRadioState(player, state);
                player.setup.voice.TargetClearPhone();

                player.Notify("Radio", "Radio désactivée !");
                panel.Refresh();
            });
            if (!state.IsActive)
            {
                panel.AddButton(Color("Activer", Colors.Success), (ui) =>
                {
                    state.IsActive = true;
                    state.Frequency = 0;
                    RadioManager.SetRadioState(player, state);
                    player.Notify("Radio", "Radio activée !");
                    panel.Refresh();
                });
            }
            else
            {
                if (state.Frequency > 0)
                {
                    panel.AddTabLine($"{(state.IsSpeak ? Color("Mute : Desactivé", Colors.Error) : Color("Mute : Activé", Colors.Success))}", "", ItemUtils.GetIconIdByItemId(1768), (ui) =>
                    {
                        if (state.Frequency == 0)
                        {
                            player.Notify("Radio", "Veuillez vous connecter à une fréquence.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }

                        bool wasSpeaking = state.IsSpeak;
                        state.IsSpeak = !state.IsSpeak;
                        RadioManager.SetRadioState(player, state);

                        List<Player> players = RadioManager.GetPlayersInFrequency(state.Frequency);
                        if (state.IsSpeak && !wasSpeaking) RadioManager.StartTalkingToFrequency(players, player);
                        else if (!state.IsSpeak && wasSpeaking) RadioManager.StopTalkingToFrequency(players, player);

                        panel.Refresh();
                    });
                }
                panel.AddTabLine($"Channel : {(state.Frequency == 0 ? "Aucun" : await RadioManager.GetNameOfFrequency(state.Frequency))}", Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1816), (ui) => FrequenceManager.FrequenceMainPanel(player));
                
                // Boutons
                panel.AddButton(Color("Selectionner", Colors.Success), (ui) => panel.SelectTab());
                if (state.Frequency > 0)
                {
                    panel.AddButton(Color("Deconnexion", Colors.Error), (ui) =>
                    {
                        RadioManager.GetPlayersInFrequency(state.Frequency)
                            .ToList()
                            .ForEach(p => p.Notify("Radio", $"{Color(player.GetFullName(), Colors.Error)} vient de quitter la fréquence {state.Frequency}.", NotificationManager.Type.Error));

                        state.Frequency = 0;
                        RadioManager.SetRadioState(player, state);

                        VoiceUtils.DisconnectAllPlayerVoices(player);
                        panel.Refresh();
                    });
                }
            }
            panel.CloseButton(Color("Fermer", Colors.Error));

            // Affichage
            panel.Display();
        }

        public static class RadioManager
        {
            private static Dictionary<Player, RadioState> radioStates = new Dictionary<Player, RadioState>();

            public static RadioState GetRadioState(Player player)
            {
                if (!radioStates.ContainsKey(player))
                {
                    radioStates[player] = new RadioState
                    {
                        IsActive = false,
                        Frequency = 0
                    };
                }
                return radioStates[player];
            }
            public static void SetRadioState(Player player, RadioState state)
            {
                radioStates[player] = state;
            }
            public static List<Player> GetPlayersInFrequency(int frequency)
            {
                return radioStates
                    .Where(x => x.Value.IsActive && x.Value.Frequency == frequency && x.Key != null)
                    .Select(x => x.Key)
                    .ToList();
            }
            public static int GetPlayerCountInFrequency(int frequency)
            {
                return radioStates.Count(x => x.Value.IsActive && x.Value.Frequency == frequency && x.Key != null);
            }
            public async static Task<string> GetNameOfFrequency(int frequency)
            {
                List<RadioChannels> channels = await RadioChannels.Query(x => x.Id == frequency);
                if (channels.Count >= 1) return channels.FirstOrDefault().Name;
                else return "Indisponible";
            }

            public static void StartTalkingToFrequency(List<Player> players, Player player)
            {
                foreach (Player p in players)
                {
                    if (p != player) VoiceUtils.ConnectPlayerVoice(p, player);
                    p.Notify("Radio", $"{Color(player.GetFullName(), Colors.Info)} a unmute son micro.", NotificationManager.Type.Info);
                }
            }
            public static void StopTalkingToFrequency(List<Player> players, Player player)
            {
                foreach (Player p in players)
                {
                    if (p != player) VoiceUtils.DisconnectPlayerVoice(p, player);
                    p.Notify("Radio", $"{Color(player.GetFullName(), Colors.Info)} a mute son micro.", NotificationManager.Type.Info);
                }
            }
            public async static void ConnectPlayerToFrequency(Player player, int frequency)
            {
                RadioState state = GetRadioState(player);
                if (state.Frequency > 0)
                {
                    GetPlayersInFrequency(state.Frequency)
                            .Where(p => p != player)
                            .ToList()
                            .ForEach(p => p.Notify("Radio", $"{Color(player.GetFullName(), Colors.Error)} vient de quitter votre channel.", NotificationManager.Type.Error));
                }
                state.Frequency = frequency;
                SetRadioState(player, state);

                foreach (Player p in GetPlayersInFrequency(state.Frequency))
                {
                    if (p != player)
                    {
                        RadioState pState = GetRadioState(p);
                        p.Notify("Radio", $"{Color(player.GetFullName(), Colors.Success)} vient de rejoindre le channel.", NotificationManager.Type.Success);

                        if (pState.IsSpeak == true) VoiceUtils.ConnectPlayerVoice(player, p);
                    }
                }
                player.Notify("Radio", $"Vous êtes maintenant connecté au channel {Color(await RadioManager.GetNameOfFrequency(frequency), Colors.Success)}.", NotificationManager.Type.Success);
            }
            public static void DisconnectAllPlayersFromFrequency(int frequency)
            {
                foreach (Player player in GetPlayersInFrequency(frequency))
                {
                    RadioState state = GetRadioState(player);

                    if (state.IsActive && state.Frequency == frequency)
                    {
                        state.Frequency = 0;
                        SetRadioState(player, state);
                        VoiceUtils.DisconnectAllPlayerVoices(player);
                        player.Notify("Radio", $"Vous avez été déconnecté de la fréquence {frequency}.", NotificationManager.Type.Warning);
                    }
                }
            }
            public static void ResyncRadioPlayerStates(int frequency)
            {
                List<Player> playersInFrequency = RadioManager.GetPlayersInFrequency(frequency);
                
                foreach (Player player in playersInFrequency.Where(p => RadioManager.GetRadioState(p).IsSpeak))
                {
                    foreach (Player otherPlayer in playersInFrequency.Where(p => p != player))
                    {
                        VoiceUtils.ConnectPlayerVoice(otherPlayer, player);
                    }
                }
            }
        }
        public class RadioState
        {
            public bool IsActive { get; set; }
            public bool IsSpeak { get; set; }
            public int Frequency { get; set; }
        }
    }
}
