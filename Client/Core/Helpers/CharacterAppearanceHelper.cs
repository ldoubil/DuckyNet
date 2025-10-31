using System;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using DuckyNet.Shared.Data;
using DuckyNet.Client.RPC;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using CharacterServiceClientProxy = DuckyNet.Shared.Services.Generated.CharacterServiceClientProxy;
using DuckyNet.Client.Core;


namespace DuckyNet.Client.Core.Helpers
{
    /// <summary>
    /// 角色外观助手 - 提供外观上传和下载功能
    /// </summary>
    public static class CharacterAppearanceHelper
    {
        // 静态事件订阅管理器
        private static EventSubscriberHelper? _eventSubscriber;
        /// <summary>
        /// 上传当前角色的外观数据到服务器
        /// </summary>
        public static async Task<bool> UploadCurrentAppearanceAsync()
        {
            if (!GameContext.IsInitialized)
            {
                Debug.LogError("[CharacterAppearanceHelper] GameContext 未初始化");
                return false;
            }

            try
            {
                var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
                var rpcClient = GameContext.Instance.RpcClient;
                var customizationManager = GameContext.Instance.CharacterCustomizationManager;

                // 检查是否已连接到服务器
                if (!rpcClient.IsConnected)
                {
                    Debug.Log("[CharacterAppearanceHelper] 未连接到服务器，跳过自动上传（这是正常的）");
                    return false;
                }

                // 获取本地玩家角色对象
                var localCharacter = customizationManager.GetLocalPlayerCharacter();
                if (localCharacter == null)
                {
                    Debug.LogWarning("[CharacterAppearanceHelper] 无法获取本地玩家角色对象");
                    return false;
                }

                // 从角色对象提取外观数据
                var customData = customizationManager.GetCustomizationFromCharacter(localCharacter);
                if (customData == null)
                {
                    Debug.LogWarning("[CharacterAppearanceHelper] 无法从角色提取外观数据");
                    return false;
                }

                // 转换为网络数据格式
                var converter = new Helpers.CharacterAppearanceConverter();
                var networkData = converter.ConvertToNetworkData(customData);
                
                if (networkData == null)
                {
                    Debug.LogError("[CharacterAppearanceHelper] 外观数据转换失败");
                    return false;
                }

                // 压缩为字节数组
                byte[] appearanceBytes = networkData.ToBytes();

                // 上传到服务器（使用前面已声明的 rpcClient）
                var context = new ClientServerContext(rpcClient);
                var proxy = new CharacterServiceClientProxy(context);
                
                bool success = await proxy.UpdateAppearanceAsync(appearanceBytes);
                
                if (success)
                {
                    Debug.Log($"[CharacterAppearanceHelper] ✅ 外观上传成功 ({appearanceBytes.Length} bytes)");
                    
                    // 同时标记角色已创建并更新本地状态
                    localPlayer.Info.HasCharacter = true;
                    localPlayer.Info.AppearanceData = appearanceBytes;
                }
                else
                {
                    Debug.LogError("[CharacterAppearanceHelper] 外观上传失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceHelper] 上传外观异常: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// 从服务器下载指定玩家的外观数据
        /// </summary>
        public static async Task<CharacterAppearanceData?> DownloadAppearanceAsync(string steamId)
        {
            if (!GameContext.IsInitialized)
            {
                Debug.LogError("[CharacterAppearanceHelper] GameContext 未初始化");
                return null;
            }

            try
            {
                var rpcClient = GameContext.Instance.RpcClient;
                var context = new ClientServerContext(rpcClient);
                var proxy = new CharacterServiceClientProxy(context);
                
                byte[]? appearanceBytes = await proxy.GetAppearanceAsync(steamId);
                
                if (appearanceBytes == null || appearanceBytes.Length == 0)
                {
                    Debug.LogWarning($"[CharacterAppearanceHelper] 玩家 {steamId} 没有外观数据");
                    return null;
                }

                Debug.Log($"[CharacterAppearanceHelper] 下载外观数据: {steamId} ({appearanceBytes.Length} bytes)");
                
                var appearanceData = CharacterAppearanceData.FromBytes(appearanceBytes);
                return appearanceData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceHelper] 下载外观异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 应用外观数据到角色对象
        /// </summary>
        public static bool ApplyAppearance(GameObject character, CharacterAppearanceData appearanceData)
        {
            if (!GameContext.IsInitialized)
            {
                Debug.LogError("[CharacterAppearanceHelper] GameContext 未初始化");
                return false;
            }

            try
            {
                var converter = new Helpers.CharacterAppearanceConverter();
                var customData = converter.ConvertToGameData(appearanceData);
                
                if (customData == null)
                {
                    Debug.LogError("[CharacterAppearanceHelper] 外观数据转换失败");
                    return false;
                }

                var customizationManager = GameContext.Instance.CharacterCustomizationManager;
                customizationManager.ApplyToCharacter(character, customData);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceHelper] 应用外观异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 监听并自动上传角色外观（通过 EventSubscriberHelper 订阅）
        /// </summary>
        public static void StartAutoUpload()
        {
            if (!GameContext.IsInitialized)
            {
                Debug.LogError("[CharacterAppearanceHelper] GameContext 未初始化");
                return;
            }

            try
            {
                // 创建或重用事件订阅管理器
                if (_eventSubscriber == null)
                {
                    _eventSubscriber = new EventSubscriberHelper();
                }

                // 订阅主角色创建完成事件（使用 EventSubscriberHelper）
                _eventSubscriber.EnsureInitializedAndSubscribe();
                _eventSubscriber.Subscribe<MainCharacterCreatedEvent>(OnMainCharacterCreated);
                Debug.Log("[CharacterAppearanceHelper] 已订阅主角色创建事件（EventSubscriberHelper）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceHelper] 启动自动上传失败: {ex.Message}");
            }
        }

        // 防抖机制：避免重复上传
        private static DateTime _lastUploadTime = DateTime.MinValue;
        private static readonly TimeSpan _uploadDebounceInterval = TimeSpan.FromSeconds(2); // 2秒内只上传一次
        private static bool _isUploading = false;

        /// <summary>
        /// 主角色创建完成事件处理
        /// </summary>
        private static async void OnMainCharacterCreated(MainCharacterCreatedEvent evt)
        {
            try
            {
                if (!GameContext.IsInitialized) return;
                
                // 防抖：检查距离上次上传的时间间隔
                var now = DateTime.Now;
                if (now - _lastUploadTime < _uploadDebounceInterval)
                {
                    Debug.Log($"[CharacterAppearanceHelper] 忽略重复上传请求（距离上次 {((now - _lastUploadTime).TotalSeconds):F1}秒）");
                    return;
                }
                
                // 防止并发上传
                if (_isUploading)
                {
                    Debug.Log("[CharacterAppearanceHelper] 已有上传任务正在进行，跳过");
                    return;
                }
                
                _isUploading = true;
                
                // 多次重试，等待角色完全初始化
                int maxRetries = 10;
                int[] retryDelays = { 500, 500, 1000, 1000, 2000, 2000, 3000, 3000, 5000, 5000 };
                
                for (int i = 0; i < maxRetries; i++)
                {
                    // 延迟等待
                    await System.Threading.Tasks.Task.Delay(retryDelays[i]);
                    
                    // 如果未连接到服务器，直接停止重试
                    if (GameContext.IsInitialized && !GameContext.Instance.RpcClient.IsConnected)
                    {
                        _isUploading = false;
                        return;
                    }
                    
                    bool success = await UploadCurrentAppearanceAsync();
                    
                    if (success)
                    {
                        _lastUploadTime = DateTime.Now;
                        _isUploading = false;
                        Debug.Log($"[CharacterAppearanceHelper] ✅ 自动上传外观成功 (尝试 {i + 1} 次)");
                        return; // 成功后立即返回
                    }
                }
                
                _isUploading = false;
                Debug.LogWarning("[CharacterAppearanceHelper] ⚠️ 自动上传外观失败（可能未连接服务器）");
            }
            catch (Exception ex)
            {
                _isUploading = false;
                Debug.LogError($"[CharacterAppearanceHelper] 自动上传外观异常: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 停止自动上传监听
        /// </summary>
        public static void StopAutoUpload()
        {
            try
            {
                // 使用 EventSubscriberHelper 自动管理取消订阅
                _eventSubscriber?.Dispose();
                _eventSubscriber = null;
                Debug.Log("[CharacterAppearanceHelper] 已取消订阅主角色创建事件（EventSubscriberHelper）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceHelper] 停止自动上传失败: {ex.Message}");
            }
        }
    }
}

