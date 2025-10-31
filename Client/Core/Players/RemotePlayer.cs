using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 本地玩家管理器
    /// 负责管理本地玩家信息，包括从 Steam API 获取玩家数据
    /// </summary>
    public class RemotePlayer : BasePlayer
    {
        public RemotePlayer(PlayerInfo info) : base(info)
        {
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override void SetAvatarTexture(Texture2D texture)
        {
            throw new NotImplementedException();
        }
    }
}