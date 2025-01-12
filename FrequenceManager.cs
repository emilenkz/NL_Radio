using Life;
using Life.CheckpointSystem;
using Life.DB;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Utils;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ModKit.Helper.TextFormattingHelper;
using static NL_Radio.NL_Radio;
using mk = ModKit.Helper.TextFormattingHelper;

namespace NL_Radio
{
    public class FrequenceManager
    {
        [Ignore] public ModKit.ModKit Context { get; set; }

        public FrequenceManager(ModKit.ModKit context)
        {
            Context = context;
        }

        public void FrequenceMainPanel(Player player)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Radio - Menu Channels", UIPanel.PanelType.TabPrice, player, () => FrequenceMainPanel(player));

            //Corps
            panel.AddTabLine(Color("Recherche", Colors.Info), Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1134), (ui) => FindChannelPanel(player));
            panel.AddTabLine(Color("Channels publics", Colors.Success), Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1816), (ui) => PublicChannelsListe(player));
            panel.AddTabLine(Color("Channels métiers", Colors.Orange), Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1463), (ui) =>
            {
                if (player.biz != null || player.setup.isAdminService) BizChannelsPanel(player);
                else
                {
                    player.Notify("Radio", "L'accès à ce menu nécéssite votre présence au sein d'une société.", NotificationManager.Type.Error);
                    panel.Refresh();
                }
            });
            panel.AddTabLine(Color("Channels Customs", Colors.Purple), Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1761), (ui) => CustomChannelsListe(player));
            if (player.IsAdmin && player.serviceAdmin)
            {
                panel.AddTabLine($"{Color($"Channels STAFF", Colors.Error)}", Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1298), (ui) => StaffFrequencePanel(player));
                panel.AddTabLine($"{Color($"Staff - Liste channels", Colors.Error)}", Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1298), (ui) => ListAllChannels(player));
                panel.AddTabLine($"{Color($"Staff - Créer un channel", Colors.Error)}", Color("Voir", Colors.Orange), ItemUtils.GetIconIdByItemId(1298), async (ui) =>
                {
                    RadioChannels newChannel = new RadioChannels();
                    await newChannel.Save();
                    CreateChannel(player, newChannel.Id, false);
                });
            }

            //Boutons
            panel.NextButton($"{Color("Selectionner", Colors.Success)}", () => panel.SelectTab());
            panel.PreviousButton(Color("Retour", Colors.Info));
            panel.CloseButton(Color("Fermer", Colors.Error));

            //Affichage
            panel.Display();
        }
        public void TryToConnectPlayer(Player player, RadioChannels channel)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create(Color("Radio - Code", Colors.Info), UIPanel.PanelType.Input, player, () => TryToConnectPlayer(player, channel));

            //Corps
            panel.TextLines.Add($"Veuillez entrer le code d'accès du channel : {Color(channel.Name, Colors.Info)}");

            //Boutons
            panel.AddButton(Color("Valider", Colors.Success), (ui) =>
            {
                if (string.IsNullOrWhiteSpace(panel.inputText))
                {
                    player.Notify("Erreur", "Veuillez entrer un nom valide.", Life.NotificationManager.Type.Error);
                    panel.Refresh();
                    return;
                }
                if (channel.Code == panel.inputText)
                {
                    RadioManager.ConnectPlayerToFrequency(player, channel.Id);
                    panel.Previous();
                }
                else
                {
                    player.Notify("Radio", "Mot de passe incorrecte, veuillez réessayer", NotificationManager.Type.Error);
                    panel.Refresh();
                }
            });
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public async void PublicChannelsListe(Player player)
        {
            //Query
            List<RadioChannels> elements = await RadioChannels.Query(x => x.RadioType == RadioChannels.RadioTypeEnum.Public);
            List<RadioChannels> sortedElements = elements.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Liste channels publics", UIPanel.PanelType.TabPrice, player, () => PublicChannelsListe(player));

            //Corps
            if (sortedElements.Count > 0)
            {
                foreach (RadioChannels element in sortedElements)
                {
                    int playersConnected = NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id);
                    panel.AddTabLine($"{Color($"{element.Name}", Colors.Info)}",
                        $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{playersConnected} / {element.MaxSlot} ", Colors.Info)}", 14)}",
                        ItemUtils.GetIconIdByItemId(1816), (ui) =>
                        {
                            RadioState state = NL_Radio.RadioManager.GetRadioState(player);
                            if (state.Frequency == element.Id)
                            {
                                player.Notify("Radio", "Vous êtes déjà connecté à ce channel.", NotificationManager.Type.Error);
                                panel.Refresh();
                                return;
                            }
                            if (playersConnected >= element.MaxSlot && !player.setup.isAdminService)
                            {
                                player.Notify("Radio", "Le nombre maximal de joueurs a été atteint.", NotificationManager.Type.Error);
                                panel.Refresh();
                                return;
                            }
                            if (element.IsPrivate && !player.setup.isAdminService)
                                TryToConnectPlayer(player, element);
                            else
                            {
                                NL_Radio.RadioManager.ConnectPlayerToFrequency(player, element.Id);
                                panel.Refresh();
                            }
                        });
                }
            }
            else panel.AddTabLine($"{Color($"Aucun channel public trouvé...", Colors.Orange)}", _ => { });


            //Boutons
            panel.AddButton(Color("Connexion", Colors.Success), (ui) => panel.SelectTab());
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public async void BizChannelsPanel(Player player)
        {
            //Query
            List<RadioChannels> elements = await RadioChannels.Query(x => x.RadioType == RadioChannels.RadioTypeEnum.Biz);
            List<RadioChannels> sortedElements = elements.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Liste channels métiers", UIPanel.PanelType.TabPrice, player, () => BizChannelsPanel(player));

            //Corps
            if (sortedElements.Count > 0)
            {
                foreach (RadioChannels element in sortedElements)
                {
                    int playersConnected = NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id);

                    panel.AddTabLine($"{Color($"{element.Name}", Colors.Info)}",
                    $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{playersConnected} / {element.MaxSlot} ", Colors.Info)}", 14)}",
                    ItemUtils.GetIconIdByItemId(1816), (ui) =>
                    {
                        RadioState state = NL_Radio.RadioManager.GetRadioState(player);
                        if (!element.BizIdAllowed.Contains(player.biz.Id) && !player.setup.isAdminService)
                        {
                            player.Notify("Radio", "Vous ne possèdez pas l'autorisation de rejoindre ce channel.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }
                        if (state.Frequency == element.Id)
                        {
                            player.Notify("Radio", "Vous êtes déjà connecté sur ce channel.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }
                        if (playersConnected >= element.MaxSlot && !player.setup.isAdminService)
                        {
                            player.Notify("Radio", "Le nombre maximal de joueurs a été atteint.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }
                        if (element.IsPrivate && !player.setup.isAdminService)
                            TryToConnectPlayer(player, element);
                        else
                        {
                            NL_Radio.RadioManager.ConnectPlayerToFrequency(player, element.Id);
                            panel.Refresh();
                        }
                    });
                }
            }
            else panel.AddTabLine($"{Color($"Aucun channel trouvé d'entreprises trouvé...", Colors.Orange)}", _ => { });


            //Boutons
            panel.AddButton(Color("Valider", Colors.Success), (ui) => panel.SelectTab());
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public async void CustomChannelsListe(Player player)
        {
            //Query
            List<RadioChannels> elements = await RadioChannels.Query(x => x.RadioType == RadioChannels.RadioTypeEnum.Custom);
            List<RadioChannels> sortedElements = elements.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Liste channels customs", UIPanel.PanelType.TabPrice, player, () => CustomChannelsListe(player));

            //Corps
            if (sortedElements.Count > 0)
            {
                foreach (RadioChannels element in sortedElements)
                {
                    int playersConnected = NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id);
                    panel.AddTabLine($"{Color($"{element.Name}", Colors.Purple)}",
                        $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{playersConnected} / {element.MaxSlot} ", Colors.Info)}", 14)}",
                        ItemUtils.GetIconIdByItemId(1816), (ui) =>
                        {
                            RadioState state = NL_Radio.RadioManager.GetRadioState(player);
                            if (state.Frequency == element.Id)
                            {
                                player.Notify("Radio", "Vous êtes déjà connecté sur ce channel.", NotificationManager.Type.Error);
                                panel.Refresh();
                                return;
                            }
                            if (playersConnected >= element.MaxSlot && !player.setup.isAdminService)
                            {
                                player.Notify("Radio", "Le nombre maximal de joueurs a été atteint.", NotificationManager.Type.Error);
                                panel.Refresh();
                                return;
                            }
                            if (element.IsPrivate && !player.setup.isAdminService)
                                TryToConnectPlayer(player, element);
                            else
                            {
                                NL_Radio.RadioManager.ConnectPlayerToFrequency(player, element.Id);
                                panel.Refresh();
                            }
                        });
                }
            }
            else panel.AddTabLine($"{Color($"Aucun channel customs trouvé...", Colors.Orange)}", _ => { });

            //Boutons
            panel.AddButton($"{Color("Connexion", Colors.Success)}", (ui) => panel.SelectTab());
            panel.NextButton($"{Color("Creation", Colors.Purple)}", async () =>
            {
                RadioChannels newChannel = new RadioChannels();
                await newChannel.Save();
                CreateChannel(player, newChannel.Id, true);
            });
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public async void StaffFrequencePanel(Player player)
        {
            //Query
            List<RadioChannels> elements = await RadioChannels.Query(x => x.RadioType == RadioChannels.RadioTypeEnum.Staff);
            List<RadioChannels> sortedElements = elements.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Liste channels STAFF", UIPanel.PanelType.TabPrice, player, () => StaffFrequencePanel(player));

            //Corps
            if (sortedElements.Count > 0)
            {
                foreach (RadioChannels element in sortedElements)
                {
                    int playersConnected = RadioManager.GetPlayerCountInFrequency(element.Id);
                    panel.AddTabLine($"{Color($"{element.Name}", Colors.Info)}", $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{playersConnected} / {element.MaxSlot} ", Colors.Info)}", 14)}", ItemUtils.GetIconIdByItemId(0), (ui) =>
                    {
                        RadioState state = NL_Radio.RadioManager.GetRadioState(player);
                        if (state.Frequency == element.Id)
                        {
                            player.Notify("Radio", "Vous êtes déjà connecté sur ce channel.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }
                        if (playersConnected >= element.MaxSlot)
                        {
                            player.Notify("Radio", "Le nombre maximal de joueurs a été atteint.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }

                        if (element.IsPrivate)
                            TryToConnectPlayer(player, element);
                        else
                        {
                            NL_Radio.RadioManager.ConnectPlayerToFrequency(player, element.Id);
                            panel.Refresh();
                        }
                    });
                }
            }
            else panel.AddTabLine($"{Color($"Aucun channel staff trouvé...", Colors.Orange)}", _ => { });

            //Boutons
            panel.AddButton($"{Color("Connexion", Colors.Success)}", (ui) => panel.SelectTab());
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public async void FindChannelPanel(Player player)
        {
            //Query
            RadioState state = NL_Radio.RadioManager.GetRadioState(player);

            //Déclaration
            Panel panel = Context.PanelHelper.Create(Color("Radio - Menu Recherche", Colors.Info), UIPanel.PanelType.Input, player, () => FindChannelPanel(player));

            //Corps
            panel.TextLines.Add($"Channel actuelle : {Color((state.Frequency == 0 ? "Aucun" : await RadioManager.GetNameOfFrequency(state.Frequency)), Colors.Info)}");
            panel.TextLines.Add("Veuillez indiquer le NOM du channel recherché :");
            panel.SetInputPlaceholder("Exemple : Samu");

            //Boutons
            panel.AddButton($"{Color("Recherche", Colors.Success)}", (ui) =>
            {
                if (string.IsNullOrWhiteSpace(panel.inputText))
                {
                    player.Notify("Erreur", "Veuillez entrer un nom valide.", Life.NotificationManager.Type.Error);
                    panel.Refresh();
                }
                else
                {
                    FindResultPanel(player, panel.inputText);
                }
            });
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public async void FindResultPanel(Player player, string input)
        {
            //Query
            List<RadioChannels> queriedElements = await RadioChannels.Query(elmt => elmt.Name.ToLower().Contains(input.ToLower()));

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Radio - Resultat Recherche", UIPanel.PanelType.TabPrice, player, () => FindResultPanel(player, input));

            //Corps
            if (queriedElements.Count > 0)
            {
                foreach (RadioChannels element in queriedElements)
                {
                    int playersConnected = RadioManager.GetPlayerCountInFrequency(element.Id);

                    panel.AddTabLine($"{Color($"{ReturnTextWithOccurence(element.Name, input, Colors.Orange)}", Colors.Info)}", $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id)} / {element.MaxSlot} ", Colors.Info)}", 14)}", ItemUtils.GetIconIdByItemId(1816), (ui) =>
                    {
                        RadioState state = RadioManager.GetRadioState(player);
                        if (state.Frequency == element.Id)
                        {
                            player.Notify("Radio", "Vous êtes déjà connecté sur ce channel.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }
                        if (RadioManager.GetPlayerCountInFrequency(element.Id) >= element.MaxSlot)
                        {
                            player.Notify("Radio", "Le nombre maximal de joueurs a été atteint.", NotificationManager.Type.Error);
                            panel.Refresh();
                            return;
                        }

                        if (element.IsPrivate) //&& !player.IsAdmin
                        {
                            TryToConnectPlayer(player, element);
                        }
                        else
                        {
                            NL_Radio.RadioManager.ConnectPlayerToFrequency(player, element.Id);
                            panel.Refresh();
                        }
                    });

                }
            }
            else panel.AddTabLine($"{Color($"Aucun channel trouvé pour \"{input}\"...", Colors.Orange)}", _ => { });

            //Boutons
            panel.AddButton($"{Color("Connexion", Colors.Success)}", (ui) => panel.SelectTab());
            panel.AddButton(Color("Retour", Colors.Info), (ui) => FrequenceMainPanel(player));

            //Affichage
            panel.Display();
        }

        #region CreateChannel
        public async void CreateChannel(Player player, int channelId, bool isCustom)
        {
            //Query
            RadioChannels channel = (await RadioChannels.Query(o => o.Id == channelId))?[0];
            if (isCustom) channel.RadioType = RadioChannels.RadioTypeEnum.Custom;
            await channel.Save();

            //Déclaration
            Panel panel = Context.PanelHelper.Create($"Radio - Création channel", UIPanel.PanelType.TabPrice, player, () => CreateChannel(player, channelId, isCustom));

            //Corps
            panel.AddTabLine($"{Color("Nom :", Colors.Info)} {(channel.Name != null ? $"{channel.Name}" : mk.Italic("A définir"))}", _ => { SetName(player, channel); });
            panel.AddTabLine($"{Color("Type :", Colors.Info)} {(channel.RadioType != RadioChannels.RadioTypeEnum.None ? $"{channel.RadioType}" : mk.Italic("A définir"))}", _ =>
            {
                SetRadioType(player, channel, isCustom);
            });
            if (channel.RadioType == RadioChannels.RadioTypeEnum.Biz && !isCustom) panel.AddTabLine($"{Color("Entreprises autorisés :", Colors.Info)}", _ => SetBizAllowed(player, channel));
            panel.AddTabLine($"{Color("MaxSlot :", Colors.Info)} {(channel.MaxSlot > 0 ? $"{channel.MaxSlot}" : mk.Italic("A définir"))}", _ => { SetSlot(player, channel); });
            panel.AddTabLine($"{Color("Privée à code :", Colors.Info)} {(channel.IsPrivate ? "Oui" : "Non")}", async _ =>
            {
                channel.IsPrivate = !channel.IsPrivate;
                if (await channel.Save()) player.Notify("Radio", $"Statut modifié : {(channel.IsPrivate ? "Privé" : "Public")}", NotificationManager.Type.Success);
                else player.Notify("Radio", "Échec de la modification du statut.", NotificationManager.Type.Error);
                panel.Refresh();
            });
            if (channel.IsPrivate) panel.AddTabLine($"{Color("Code secret :", Colors.Info)} {(channel.Code != null ? channel.Code.ToString() : mk.Italic("A définir"))}", _ => { SetCode(player, channel); });

            //Boutons
            panel.AddButton($"{Color("Modifier", Colors.Orange)}", _ => panel.SelectTab());
            panel.PreviousButtonWithAction(Color("Valider", Colors.Success), async () =>
            {
                if (string.IsNullOrEmpty(channel.Name) || channel.RadioType == RadioChannels.RadioTypeEnum.None || channel.Id <= 0 || (channel.IsPrivate && channel.Code == null))
                {
                    player.Notify("Radio", "Veuillez remplir toute les données.", NotificationManager.Type.Error);
                    return false;
                }
                if (channel.RadioType == RadioChannels.RadioTypeEnum.Biz && !channel.BizIdAllowed.Any())
                {
                    player.Notify("Radio", "Veuillez rajouter au minimum une entreprise autorisé pour un channel d'entreprise.", NotificationManager.Type.Error);
                    return false;
                }

                bool result = await channel.Save();
                if (result)
                {
                    player.Notify("Radio", "Channel crée avec succès.", NotificationManager.Type.Success);
                    return true;
                }
                else
                {
                    player.Notify("Radio", "Erreur système, veuillez réessayez.", NotificationManager.Type.Error);
                    return false;
                }
            });
            panel.PreviousButtonWithAction(Color("Annuler", Colors.Error), async () =>
            {
                if (channel.Id > 0)
                {
                    if (await channel.Delete()) return true;
                    else
                    {
                        player.Notify("Radio", "Échec de la suppression", NotificationManager.Type.Error);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Radio", "Ce channel ne peut pas être supprimé.", NotificationManager.Type.Warning);
                    return false;
                }
            });

            //Affichage
            panel.Display();
        }
        public void SetName(Player player, RadioChannels channel)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Channel - Nom", UIPanel.PanelType.Input, player, () => SetName(player, channel));

            //Corps
            panel.TextLines.Add($"Définir le nom du channel :");
            panel.inputPlaceholder = $"Exemple: SAMU";

            //Boutons
            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (panel.inputText.Length < 3)
                {
                    player.Notify("Radio", "3 lettres minimum", NotificationManager.Type.Warning);
                    return false;
                }
                if ((await RadioChannels.Query(o => o.Name == panel.inputText)).Any())
                {
                    player.Notify("Erreur", "Un channel avec ce nom existe déjà, veuillez réessayer.", NotificationManager.Type.Error);
                    return false;
                }

                channel.Name = panel.inputText;
                bool saved = await channel.Save();

                player.Notify("Radio", saved ? "Modification enregistrée" : "Nous n'avons pas pu enregistrer cette modification", saved ? NotificationManager.Type.Success : NotificationManager.Type.Error);
                return saved;
            });
            panel.PreviousButton(Color("Retour", Colors.Error));
            
            //Affichage
            panel.Display();
        }

        public void SetSlot(Player player, RadioChannels channel)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Channel - MaxSlot", UIPanel.PanelType.Input, player, () => SetSlot(player, channel));

            //Corps
            panel.TextLines.Add($"Définir le nombre de slots du channel :");
            panel.inputPlaceholder = $"Exemple: 15";

            //Boutons
            panel.PreviousButtonWithAction($"{Color("Valider", Colors.Info)}", async () =>
            {
                if (!int.TryParse(panel.inputText, out int slot) && slot <= 1)
                {
                    player.Notify("Erreur", "Veuillez choisir une fréquence valide.");
                    return false;
                }

                channel.MaxSlot = slot;
                if (await channel.Save())
                {
                    player.Notify("Radio", $"Modification enregistrée", NotificationManager.Type.Success);
                    return true;
                }
                else
                {
                    player.Notify("Radio", $"Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                    return false;
                }
            });
            panel.PreviousButton(Color("Retour", Colors.Error));

            //Affichage
            panel.Display();
        }
        public void SetRadioType(Player player, RadioChannels channel, bool isCustom)
        {
            // Déclaration
            Panel panel = Context.PanelHelper.Create("Channel - Type", UIPanel.PanelType.Tab, player, () => SetRadioType(player, channel, isCustom));

            // Corps
            foreach (RadioChannels.RadioTypeEnum radioType in Enum.GetValues(typeof(RadioChannels.RadioTypeEnum)))
            {
                if (radioType == RadioChannels.RadioTypeEnum.None) continue;
                if (isCustom && radioType != RadioChannels.RadioTypeEnum.Custom) continue;

                Colors color = (channel.RadioType == radioType) ? Colors.Success : Colors.Error;

                panel.AddTabLine($"{Color(radioType.ToString(), color)}", async (ui) =>
                {
                    channel.RadioType = radioType;
                    if (await channel.Save())
                    {
                        player.Notify("Radio", $"Modification enregistrée", NotificationManager.Type.Success);
                        panel.Previous();
                    }
                    else
                    {
                        player.Notify("Radio", $"Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                        panel.Refresh();
                    }
                });
            }

            // Boutons
            panel.AddButton("Valider", (ui) => panel.SelectTab());
            panel.PreviousButton(Color("Retour", Colors.Error));

            // Affichage
            panel.Display();
        }
        public void SetCode(Player player, RadioChannels channel)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Channel - Code", UIPanel.PanelType.Input, player, () => SetCode(player, channel));

            //Corps
            panel.TextLines.Add($"Définir le code du channel :");
            panel.inputPlaceholder = $"Exemple: 1234 ou ExEmPlE";

            //Boutons
            panel.PreviousButtonWithAction($"{Color("Valider", Colors.Info)}", async () =>
            {
                channel.Code = panel.inputText;
                if (await channel.Save())
                {
                    player.Notify("Radio", $"Modification enregistrée", NotificationManager.Type.Success);
                    return true;
                }
                else
                {
                    player.Notify("Radio", $"Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                    return false;
                }
            });
            panel.PreviousButton(Color("Retour", Colors.Error));

            //Affichage
            panel.Display();
        }
        public void SetBizAllowed(Player player, RadioChannels channel)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Channel - BizAllowed", UIPanel.PanelType.Tab, player, () => SetBizAllowed(player, channel));

            //Corps
            foreach (Bizs biz in Nova.biz.bizs)
            {
                panel.AddTabLine($"{(channel.BizIdAllowed.Contains(biz.Id) ? $"{Color($"{biz.BizName}", Colors.Success)}" : $"{Color($"{biz.BizName}", Colors.Error)}")}", async ui =>
                {
                    if (channel.BizIdAllowed.Contains(biz.Id)) channel.BizIdAllowed.Remove(biz.Id);
                    else channel.BizIdAllowed.Add(biz.Id);
                    await channel.Save();
                    panel.Refresh();
                });
            }

            //Boutons
            panel.AddButton(Color("Modifier", Colors.Info), ui => panel.SelectTab());
            panel.PreviousButton(Color("Valider", Colors.Success));

            //Affichage
            player.ShowPanelUI(panel);
        }
        #endregion
        #region InfosChannel
        public async void ListAllChannels(Player player)
        {
            //Query
            List<RadioChannels> elements = await RadioChannels.QueryAll();
            List<RadioChannels> sortedElements = elements.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Liste Channels", UIPanel.PanelType.TabPrice, player, () => ListAllChannels(player));

            //Corps
            foreach (RadioChannels element in sortedElements)
            {
                panel.AddTabLine($"{Color($"{element.Name}", Colors.Info)}", $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id)} / {element.MaxSlot} ", Colors.Info)}", 14)}", ItemUtils.GetIconIdByItemId(0), (ui) =>
                {
                    ChannelInfos(player, element);
                });
            }

            //Boutons
            panel.AddButton($"{Color("Voir", Colors.Orange)}", (ui) => panel.SelectTab());
            panel.AddButton($"{Color("Recherche", Colors.Info)}", (ui) => StaffFindChannelPanel(player));

            panel.PreviousButton(Color("Retour", Colors.Error));


            //Affichage
            panel.Display();
        }
        public void StaffFindChannelPanel(Player player)
        {
            //Query
            RadioState state = RadioManager.GetRadioState(player);

            //Déclaration
            Panel panel = Context.PanelHelper.Create(Color("Menu Staff - Recherche", Colors.Info), UIPanel.PanelType.Input, player, () => StaffFindChannelPanel(player));

            //Corps
            panel.TextLines.Add($"Veuillez indiquer le NOM du channel recherché :");
            panel.SetInputPlaceholder("Exemple : Samu");

            //Boutons
            panel.AddButton($"{Color("Recherche", Colors.Info)}", (ui) =>
            {
                if (string.IsNullOrWhiteSpace(panel.inputText))
                {
                    player.Notify("Erreur", "Veuillez entrer un nom valide.", Life.NotificationManager.Type.Error);
                    panel.Refresh();
                }
                else
                {
                    StaffFindResultPanel(player, panel.inputText);
                }
            });
            panel.PreviousButton(Color("Retour", Colors.Error));


            //Affichage
            panel.Display();
        }
        public async void StaffFindResultPanel(Player player, string input)
        {
            //Query
            List<RadioChannels> queriedElements = await RadioChannels.Query(elmt => elmt.Name.ToLower().Contains(input.ToLower()));

            //Déclaration
            Panel panel = Context.PanelHelper.Create(Color("Menu Staff - Resultat", Colors.Info), UIPanel.PanelType.TabPrice, player, () => StaffFindResultPanel(player, input));

            //Corps
            if (queriedElements.Count > 0)
            {
                foreach (RadioChannels element in queriedElements)
                {
                    int playersConnected = NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id);

                    panel.AddTabLine($"{Color($"{ReturnTextWithOccurence(element.Name, input, Colors.Orange)}", Colors.Info)}", $"{mk.Size($"{(element.IsPrivate ? "Privé" : "Public")}<br>{Color($"{NL_Radio.RadioManager.GetPlayerCountInFrequency(element.Id)} / {element.MaxSlot} ", Colors.Info)}", 14)}", ItemUtils.GetIconIdByItemId(1816), (ui) =>
                    {
                        ChannelInfos(player, element);
                    });

                }
            }
            else panel.AddTabLine($"{Color($"Aucun channel trouvé pour \"{input}\"...", Colors.Orange)}", _ => { });

            //Boutons
            panel.AddButton($"{Color("Voir", Colors.Orange)}", (ui) => panel.SelectTab());
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        public void ChannelInfos(Player player, RadioChannels channel)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Infos Channel", UIPanel.PanelType.TabPrice, player, () => ChannelInfos(player, channel));

            //Corps
            panel.AddTabLine($"Nom : {channel.Name}", (ui) => { SetName(player, channel); });
            panel.AddTabLine($"MaxSlot : {channel.MaxSlot}", (ui) => { SetSlot(player, channel); });
            panel.AddTabLine($"RadioType : {channel.RadioType}", (ui) => { SetRadioType(player, channel, false); });
            panel.AddTabLine($"BizIdAllowed :", (ui) => { SetBizAllowed(player, channel); });
            panel.AddTabLine($"IsPrivate : {(channel.IsPrivate ? "Oui" : "Non")}", (ui) =>
            {
                channel.IsPrivate = !channel.IsPrivate;
                channel.Save();
            });
            if (channel.IsPrivate) panel.AddTabLine($"Code : {channel.Code}", (ui) => { });

            //Boutons
            panel.AddButton(Color("Modifier", Colors.Success), (ui) => panel.SelectTab());
            panel.PreviousButtonWithAction($"{Color("SUPPRIMER", Colors.Error)}", async () =>
            {
                NL_Radio.RadioManager.DisconnectAllPlayersFromFrequency(channel.Id);
                if (await channel.Delete())
                {
                    player.Notify("Radio", "Channel supprimé avec succès !", NotificationManager.Type.Success);
                    return true;
                }
                else
                {
                    player.Notify("Radio", "Erreur lors de la suppression !", NotificationManager.Type.Error);
                    return false;
                }
            });
            panel.AddButton($"{Color("Se connecter", Colors.Orange)}", (ui) => NL_Radio.RadioManager.ConnectPlayerToFrequency(player, channel.Id));
            panel.PreviousButton(Color("Retour", Colors.Info));

            //Affichage
            panel.Display();
        }
        #endregion
    }

}
