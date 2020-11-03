﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MultiplayerARPG.MMO.GuildWar;
using LiteNetLibManager;
using LiteNetLib;

namespace MultiplayerARPG
{
    public abstract partial class BaseGameNetworkManager
    {
        [Header("Guild War")]
        public ushort guildWarStatusMsgType = 50;

        public bool GuildWarRunning { get; private set; }
        public System.DateTime LastOccupyTime { get; private set; }
        public int DefenderGuildId { get; private set; }
        public string DefenderGuildName { get; private set; }

        [DevExtMethods("RegisterClientMessages")]
        public void RegisterClientMessages_GuildWar()
        {
            RegisterClientMessage(guildWarStatusMsgType, HandleGuildWarStatusAtClient);
        }

        [DevExtMethods("OnStartServer")]
        public void OnStartServer_GuildWar()
        {
            CancelInvoke(nameof(Update_GuildWar));
            InvokeRepeating(nameof(Update_GuildWar), 1, 1);
        }

        [DevExtMethods("OnPeerConnected")]
        public void OnPeerConnected_GuildWar(long connectionId)
        {
            SendGuildWarStatus(connectionId);
        }

        [DevExtMethods("Clean")]
        public void Clean_GuildWar()
        {
            CancelInvoke(nameof(Update_GuildWar));
        }

        public void SendGuildWarStatus()
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                SendGuildWarStatus(connectionId);
            }
        }

        public void SendGuildWarStatus(long connectionId)
        {
            if (!IsServer)
                return;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, guildWarStatusMsgType, (writer) =>
            {
                writer.Put(GuildWarRunning);
                writer.Put(DefenderGuildId);
                writer.Put(DefenderGuildName);
            });
        }

        private void HandleGuildWarStatusAtClient(MessageHandlerData messageHandler)
        {
            if (IsServer)
                return;
            GuildWarRunning = messageHandler.Reader.GetBool();
            DefenderGuildId = messageHandler.Reader.GetInt();
            DefenderGuildName = messageHandler.Reader.GetString();
        }

        public void Update_GuildWar()
        {
            if (!IsServer || CurrentMapInfo == null || !(CurrentMapInfo is GuildWarMapInfo))
                return;

            GuildWarMapInfo mapInfo = CurrentMapInfo as GuildWarMapInfo;
            if (!GuildWarRunning && mapInfo.IsOn)
            {
                SendSystemAnnounce(mapInfo.eventStartedMessage);
                DefenderGuildId = 0;
                DefenderGuildName = string.Empty;
                ExpelLoserGuilds(DefenderGuildId);
                GuildWarRunning = true;
                SendGuildWarStatus();
            }

            if (GuildWarRunning && !mapInfo.IsOn)
            {
                SendSystemAnnounce(mapInfo.eventEndedMessage);
                GuildWarRunning = false;
                if (DefenderGuildId > 0)
                {
                    SendSystemAnnounce(string.Format(mapInfo.defenderWinMessage, DefenderGuildName));
                    GiveGuildBattleRewardTo(DefenderGuildId);
                    ExpelLoserGuilds(DefenderGuildId);
                }
                SendGuildWarStatus();
            }

            if (GuildWarRunning)
            {
                if ((System.DateTime.Now - LastOccupyTime).TotalMinutes >= mapInfo.battleDuration)
                {
                    SendSystemAnnounce(mapInfo.roundEndedMessage);
                    LastOccupyTime = System.DateTime.Now;
                    if (DefenderGuildId > 0)
                    {
                        SendSystemAnnounce(string.Format(mapInfo.defenderWinMessage, DefenderGuildName));
                        GiveGuildBattleRewardTo(DefenderGuildId);
                        ExpelLoserGuilds(DefenderGuildId);
                    }
                    SendGuildWarStatus();
                }
            }
        }

        public void CastleOccupied(int attackerGuildId)
        {
            GuildWarMapInfo mapInfo = CurrentMapInfo as GuildWarMapInfo;
            LastOccupyTime = System.DateTime.Now;
            if (attackerGuildId > 0)
            {
                DefenderGuildId = attackerGuildId;
                DefenderGuildName = guilds[attackerGuildId].guildName;
                SendSystemAnnounce(string.Format(mapInfo.attackerWinMessage, DefenderGuildName));
                GiveGuildBattleRewardTo(DefenderGuildId);
                ExpelLoserGuilds(DefenderGuildId);
            }
            SendGuildWarStatus();
        }

        private void ExpelLoserGuilds(int winnerGuildId)
        {
            // Teleport other guild characters to other map (for now, teleport to respawn position)
            List<BasePlayerCharacterEntity> otherGuildCharacters = new List<BasePlayerCharacterEntity>(GetPlayerCharacters());
            for (int i = 0; i < otherGuildCharacters.Count; ++i)
            {
                if (otherGuildCharacters[i].GuildId <= 0 ||
                    otherGuildCharacters[i].GuildId != winnerGuildId)
                {
                    WarpCharacter(WarpPortalType.Default,
                        otherGuildCharacters[i],
                        otherGuildCharacters[i].RespawnMapName,
                        otherGuildCharacters[i].RespawnPosition,
                        false, Vector3.zero);
                }
            }
        }

        private void GiveGuildBattleRewardTo(int guildId)
        {
            // TODO: May send reward to player's mail box which is not implemented yet.
        }
    }
}
