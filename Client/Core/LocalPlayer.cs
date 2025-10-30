using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 本地玩家管理器
    /// 负责管理本地玩家信息，包括从 Steam API 获取玩家数据
    /// </summary>
    public class LocalPlayer
    {
        private readonly EventSubscriberHelper _eventSub = new EventSubscriberHelper();

        /// <summary>
        /// 当前本地玩家信息（从 Steam API 获取，只读）
        /// </summary>
        public PlayerInfo Info { get; set; }

        /// <summary>
        /// Steam 头像纹理（如果已加载）
        /// </summary>
        public Texture2D? AvatarTexture { get; private set; }

        /// <summary>
        /// 是否已成功从 Steam 初始化
        /// </summary>
        public bool IsInitializedFromSteam { get; private set; }

        public LocalPlayer()
        {
            Info = new PlayerInfo();
            Initialize();
        }

        /// <summary>
        /// 从 Steam API 初始化玩家信息
        /// </summary>
        private void Initialize()
        {
            try
            {
                if (!SteamManager.Initialized)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] Steam 未初始化，使用默认玩家信息");
                    InitializeWithDefaultInfo();
                    return;
                }

                // 从 Steam 获取玩家信息
                CSteamID steamId = SteamUser.GetSteamID();
                string steamUsername = SteamFriends.GetPersonaName();
                string avatarUrl = GetSteamAvatarUrl(steamId);

                Info = new PlayerInfo
                {
                    SteamId = steamId.ToString(),
                    SteamName = steamUsername,
                    AvatarUrl = avatarUrl,
                };

                IsInitializedFromSteam = true;

                UnityEngine.Debug.Log($"[LocalPlayer] 玩家信息已初始化");
                UnityEngine.Debug.Log($"  - Steam ID: {Info.SteamId}");
                UnityEngine.Debug.Log($"  - 玩家名称: {Info.SteamName}");
                UnityEngine.Debug.Log($"  - 头像URL: {Info.AvatarUrl}");

                // 异步加载头像纹理
                LoadAvatarTexture(steamId);

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 初始化失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                InitializeWithDefaultInfo();
            }

        }



        /// <summary>
        /// 使用默认信息初始化（Steam不可用时）
        /// </summary>
        private void InitializeWithDefaultInfo()
        {
            Info = new PlayerInfo
            {
                SteamId = "default_" + Guid.NewGuid().ToString().Substring(0, 8),
                SteamName = "Player_" + UnityEngine.Random.Range(1000, 9999),
                AvatarUrl = string.Empty,
            };

            IsInitializedFromSteam = false;
            UnityEngine.Debug.LogWarning($"[LocalPlayer] 使用默认信息: ID={Info.SteamId}, Name={Info.SteamName}");
        }

        /// <summary>
        /// 获取 Steam 头像 URL
        /// </summary>
        private string GetSteamAvatarUrl(CSteamID steamId)
        {
            try
            {
                // 获取中等尺寸头像
                int avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);

                if (avatarHandle == -1 || avatarHandle == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法获取头像句柄");
                    return string.Empty;
                }
                string steamId64 = steamId.ToString();
                return $"https://steamcommunity.com/profiles/{steamId64}/";
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalPlayer] 获取头像 URL 失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 加载 Steam 头像纹理
        /// </summary>
        private void LoadAvatarTexture(CSteamID steamId)
        {
            try
            {
                // 获取中等尺寸头像句柄
                int avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);

                if (avatarHandle == -1 || avatarHandle == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无效的头像句柄");
                    return;
                }

                // 获取头像尺寸
                bool success = SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height);
                if (!success || width == 0 || height == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法获取头像尺寸");
                    return;
                }

                // 创建纹理
                byte[] imageData = new byte[width * height * 4]; // RGBA
                success = SteamUtils.GetImageRGBA(avatarHandle, imageData, (int)(width * height * 4));

                if (!success)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法获取头像数据");
                    return;
                }

                // 创建 Unity 纹理
                AvatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                AvatarTexture.LoadRawTextureData(imageData);
                AvatarTexture.Apply();

                // 垂直翻转（Steam 图像是上下颠倒的）
                FlipTextureVertically(AvatarTexture);

                UnityEngine.Debug.Log($"[LocalPlayer] 头像纹理已加载: {width}x{height}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalPlayer] 加载头像纹理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 垂直翻转纹理
        /// </summary>
        private void FlipTextureVertically(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            Color[] flipped = new Color[pixels.Length];

            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flipped[x + y * width] = pixels[x + (height - y - 1) * width];
                }
            }

            texture.SetPixels(flipped);
            texture.Apply();
        }


        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (AvatarTexture != null)
            {
                UnityEngine.Object.Destroy(AvatarTexture);
                AvatarTexture = null;
            }
        }
    }
}

