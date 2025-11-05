using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.Utils;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 玩家基类 - 管理玩家信息和关联的角色对象
    /// </summary>
    public abstract class BasePlayer: IDisposable
    {
        /// <summary>
        /// 玩家信息
        /// </summary>
        public PlayerInfo Info { get; set; }

        /// <summary>
        /// Steam 头像纹理（如果已加载）
        /// </summary>
        public Texture2D? AvatarTexture { get; set; }

        /// <summary>
        /// 角色游戏对象
        /// </summary>
        public GameObject? CharacterObject { get; protected set; }

        /// <summary>
        /// 角色是否已创建
        /// </summary>
        public bool IsCharacterCreated => CharacterObject != null;

        public BasePlayer(PlayerInfo info)
        {
            Info = info;
        }

        /// <summary>
        /// 销毁角色对象
        /// </summary>
        public virtual void DestroyCharacter()
        {
            if (CharacterObject != null)
            {
                Log($"[{GetType().Name}] 销毁角色: {Info.SteamName}");
                UnityEngine.Object.Destroy(CharacterObject);
                CharacterObject = null;
            }
        }

        /// <summary>
        /// 设置角色位置
        /// </summary>
        public void SetCharacterPosition(Vector3 position)
        {
            if (CharacterObject != null)
            {
                CharacterObject.transform.position = position;
            }
        }

        /// <summary>
        /// 获取角色位置
        /// </summary>
        public Vector3 GetCharacterPosition()
        {
            return CharacterObject != null ? CharacterObject.transform.position : Vector3.zero;
        }

        public abstract void SetAvatarTexture(Texture2D texture);

        public virtual void Dispose()
        {
            // 销毁角色对象
            DestroyCharacter();
        }
    }
}