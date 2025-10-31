using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core.Players
{
    public abstract class BasePlayer: IDisposable
    {
        public PlayerInfo Info { get; set; }

        /// <summary>
        /// Steam 头像纹理（如果已加载）
        /// </summary>
        public Texture2D? AvatarTexture { get; set; }

        public BasePlayer(PlayerInfo info)
        {
            Info = info;
        }

        public abstract void SetAvatarTexture(Texture2D texture);

        public abstract void Dispose();
    }
}