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

        public PlayerManager()
        {
            LocalPlayer = new LocalPlayer(new PlayerInfo());
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