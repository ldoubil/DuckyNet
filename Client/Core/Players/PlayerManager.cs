using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using System.Collections.Generic;

namespace DuckyNet.Client.Core.Players
{
    public class PlayerManager : IDisposable
    {
        private readonly List<RemotePlayer> _remotePlayers = new List<RemotePlayer>();
        public LocalPlayer LocalPlayer { get; private set; }
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        public PlayerManager()
        {
            LocalPlayer = new LocalPlayer(new PlayerInfo());
            _eventSubscriber.EnsureInitializedAndSubscribe();
            _eventSubscriber.Subscribe<PlayerJoinedRoomEvent>(OnPlayerJoinedRoom);
            _eventSubscriber.Subscribe<PlayerLeftRoomEvent>(OnPlayerLeftRoom);
        }

        private void OnPlayerLeftRoom(PlayerLeftRoomEvent @event)
        {
            // 排除本地玩家
            if (@event.Player.SteamId == LocalPlayer.Info.SteamId)
            {
                return;
            }
            // 检查是否已存在相同 SteamId 的远程玩家，存在则删除，否则不处理
            var existing = _remotePlayers.Find(p => p.Info.SteamId == @event.Player.SteamId);
            if (existing != null)
            {
                _remotePlayers.Remove(existing);
            }
        }

        private void OnPlayerJoinedRoom(PlayerJoinedRoomEvent @event)
        {
            // 排除本地玩家
            if (@event.Player.SteamId == LocalPlayer.Info.SteamId)
            {
                return;
            }
            // 检查是否已存在相同 SteamId 的远程玩家，存在则更新信息，否则添加
            var existing = _remotePlayers.Find(p => p.Info.SteamId == @event.Player.SteamId);
            if (existing != null)
            {
                existing.Info = @event.Player; // 更新信息
            }
            else
            {
                var remotePlayer = new RemotePlayer(@event.Player);
                _remotePlayers.Add(remotePlayer);
            }
        }

        public void Dispose()
        {
            LocalPlayer.Dispose();
            foreach (var remotePlayer in _remotePlayers)
            {
                remotePlayer.Dispose();
            }
        }
    }
}