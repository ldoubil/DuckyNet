using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 玩家模型管理器 - 负责管理场景中玩家的GameObject模型
    /// </summary>
    public class PlayerModelManager : IDisposable
    {
        /// <summary>
        /// 当前场景的玩家模型（steamId -> GameObject）
        /// </summary>
        private readonly Dictionary<string, GameObject> _playerModels = new Dictionary<string, GameObject>();

        /// <summary>
        /// 单位管理器引用
        /// </summary>
        private UnitManager? _unitManager;

        /// <summary>
        /// 本地玩家SteamId（用于跳过本地玩家的模型创建）
        /// </summary>
        private string? _localPlayerSteamId;

        public PlayerModelManager()
        {
            Debug.Log("[PlayerModelManager] 玩家模型管理器已创建");
        }

        /// <summary>
        /// 设置单位管理器
        /// </summary>
        public void SetUnitManager(UnitManager unitManager)
        {
            _unitManager = unitManager ?? throw new ArgumentNullException(nameof(unitManager));
        }

        /// <summary>
        /// 设置本地玩家SteamId
        /// </summary>
        public void SetLocalPlayerSteamId(string steamId)
        {
            _localPlayerSteamId = steamId;
        }

        /// <summary>
        /// 创建玩家模型
        /// </summary>
        public bool CreatePlayerModel(PlayerSceneInfo playerSceneInfo)
        {
            if (_unitManager == null || !GameContext.IsInitialized)
            {
                Debug.LogWarning("[PlayerModelManager] 单位管理器未初始化");
                return false;
            }

            try
            {
                // 跳过本地玩家
                if (playerSceneInfo.SteamId == _localPlayerSteamId)
                {
                    Debug.Log($"[PlayerModelManager] 跳过本地玩家模型创建: {playerSceneInfo.SteamId}");
                    return false;
                }

                // 检查是否已存在
                if (_playerModels.ContainsKey(playerSceneInfo.SteamId))
                {
                    Debug.Log($"[PlayerModelManager] 玩家模型已存在: {playerSceneInfo.SteamId}");
                    return true;
                }

                var unitManager = GameContext.Instance.UnitManager;
                var position = Vector3.zero; // TODO: 从服务器同步位置
                var stats = UnitStats.Default;

                var playerObject = unitManager.CreateUnit(
                    playerSceneInfo.PlayerInfo?.SteamName ?? "Unknown",
                    position,
                    0, // 玩家队伍
                    stats,
                    null // TODO: 将外观数据转换为CharacterCustomization
                );

                if (playerObject != null)
                {
                    _playerModels[playerSceneInfo.SteamId] = playerObject;
                    Debug.Log($"[PlayerModelManager] 创建玩家模型: {playerSceneInfo.SteamId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerModelManager] 创建玩家模型失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 销毁玩家模型
        /// </summary>
        public bool DestroyPlayerModel(string steamId)
        {
            if (_playerModels.TryGetValue(steamId, out var playerObject))
            {
                _playerModels.Remove(steamId);

                if (GameContext.IsInitialized && _unitManager != null)
                {
                    _unitManager.DestroyUnit(playerObject);
                }
                else
                {
                    UnityEngine.Object.Destroy(playerObject);
                }

                Debug.Log($"[PlayerModelManager] 销毁玩家模型: {steamId}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取玩家模型
        /// </summary>
        public GameObject? GetPlayerModel(string steamId)
        {
            _playerModels.TryGetValue(steamId, out var model);
            return model;
        }

        /// <summary>
        /// 检查玩家模型是否存在
        /// </summary>
        public bool HasPlayerModel(string steamId)
        {
            return _playerModels.ContainsKey(steamId);
        }

        /// <summary>
        /// 更新玩家外观
        /// </summary>
        public void UpdatePlayerAppearance(string steamId, byte[] appearanceData)
        {
            try
            {
                if (appearanceData == null || appearanceData.Length == 0)
                {
                    Debug.LogWarning($"[PlayerModelManager] 外观数据为空: {steamId}");
                    return;
                }

                if (!_playerModels.TryGetValue(steamId, out var playerObject))
                {
                    Debug.LogWarning($"[PlayerModelManager] 玩家模型不存在，无法更新外观: {steamId}");
                    return;
                }

                if (playerObject == null || !GameContext.IsInitialized)
                {
                    return;
                }

                // 解析外观数据
                var appearanceNetworkData = Shared.Data.CharacterAppearanceData.FromBytes(appearanceData);
                var converter = new Helpers.CharacterAppearanceConverter();
                var customData = converter.ConvertToGameData(appearanceNetworkData);

                if (customData == null)
                {
                    Debug.LogWarning($"[PlayerModelManager] 外观数据转换失败: {steamId}");
                    return;
                }

                // 应用外观
                var customizationManager = GameContext.Instance.CharacterCustomizationManager;
                customizationManager.ApplyToCharacter(playerObject, customData);
                Debug.Log($"[PlayerModelManager] 外观已应用: {steamId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerModelManager] 更新玩家外观失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理所有模型
        /// </summary>
        public void ClearAllModels()
        {
            Debug.Log($"[PlayerModelManager] 清理所有模型，数量: {_playerModels.Count}");

            var steamIds = _playerModels.Keys.ToList();
            foreach (var steamId in steamIds)
            {
                DestroyPlayerModel(steamId);
            }

            _playerModels.Clear();
        }

        /// <summary>
        /// 获取所有玩家SteamId
        /// </summary>
        public IReadOnlyCollection<string> GetAllPlayerIds()
        {
            return _playerModels.Keys;
        }

        public void Dispose()
        {
            ClearAllModels();
        }
    }
}

