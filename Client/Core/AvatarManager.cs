using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 头像管理器
    /// 统一管理所有玩家的 Steam 头像加载和缓存
    /// </summary>
    public class AvatarManager : IDisposable
    {
        private readonly Dictionary<string, Texture2D> _avatarCache;

        public AvatarManager()
        {
            _avatarCache = new Dictionary<string, Texture2D>();
        }

        /// <summary>
        /// 获取玩家头像（从缓存或加载）
        /// </summary>
        public Texture2D? GetAvatar(string steamId)
        {
            // 检查缓存
            if (_avatarCache.TryGetValue(steamId, out var cachedAvatar))
            {
                return cachedAvatar;
            }

            // 检查是否是本地玩家
            if (GameContext.IsInitialized)
            {
                var localPlayer = GameContext.Instance.LocalPlayer;
                if (localPlayer.Info.SteamId == steamId && localPlayer.AvatarTexture != null)
                {
                    // 缓存本地玩家头像
                    _avatarCache[steamId] = localPlayer.AvatarTexture;
                    return localPlayer.AvatarTexture;
                }
            }

            // 尝试加载其他玩家的头像
            var avatar = LoadAvatarFromSteam(steamId);
            if (avatar != null)
            {
                _avatarCache[steamId] = avatar;
            }

            return avatar;
        }

        /// <summary>
        /// 从 Steam API 加载头像
        /// </summary>
        private Texture2D? LoadAvatarFromSteam(string steamId)
        {
            try
            {
                if (!SteamManager.Initialized)
                {
                    return null;
                }

                if (!ulong.TryParse(steamId, out ulong steamIdValue))
                {
                    return null;
                }

                CSteamID cSteamId = new CSteamID(steamIdValue);
                int avatarHandle = SteamFriends.GetMediumFriendAvatar(cSteamId);
                
                if (avatarHandle <= 0)
                {
                    return null;
                }

                // 获取头像尺寸
                bool success = SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height);
                if (!success || width == 0 || height == 0)
                {
                    return null;
                }

                // 获取 RGBA 数据
                byte[] imageData = new byte[width * height * 4];
                success = SteamUtils.GetImageRGBA(avatarHandle, imageData, (int)(width * height * 4));
                
                if (!success)
                {
                    return null;
                }

                // 创建纹理
                Texture2D avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                avatarTexture.LoadRawTextureData(imageData);
                avatarTexture.Apply();
                
                // 垂直翻转（Steam 图像是上下颠倒的）
                FlipTextureVertically(avatarTexture);
                
                Debug.Log($"[AvatarManager] 已加载 Steam 头像: {steamId} ({width}x{height})");
                return avatarTexture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AvatarManager] 加载头像失败 {steamId}: {ex.Message}");
                return null;
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
        /// 预加载指定玩家的头像
        /// </summary>
        public void PreloadAvatar(string steamId)
        {
            if (!_avatarCache.ContainsKey(steamId))
            {
                GetAvatar(steamId);
            }
        }

        /// <summary>
        /// 清除指定玩家的头像缓存
        /// </summary>
        public void ClearAvatar(string steamId)
        {
            if (_avatarCache.TryGetValue(steamId, out var avatar))
            {
                // 不要销毁本地玩家的头像（由 LocalPlayer 管理）
                if (GameContext.IsInitialized && 
                    steamId != GameContext.Instance.LocalPlayer.Info.SteamId)
                {
                    UnityEngine.Object.Destroy(avatar);
                }
                _avatarCache.Remove(steamId);
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _avatarCache)
            {
                // 不要销毁本地玩家的头像
                if (GameContext.IsInitialized && 
                    kvp.Key != GameContext.Instance.LocalPlayer.Info.SteamId)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            _avatarCache.Clear();
            Debug.Log("[AvatarManager] 头像缓存已清除");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            ClearAll();
        }
    }
}

