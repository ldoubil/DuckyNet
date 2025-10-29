using System;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 角色创建工具 - 处理角色创建相关逻辑
    /// </summary>
    public static class CharacterCreationUtils
    {
        /// <summary>
        /// 创建角色数据项
        /// </summary>
        public static object? CreateCharacterItem()
        {
            try
            {
                var itemAssetsCollectionType = AccessTools.TypeByName("ItemStatsSystem.ItemAssetsCollection");
                var gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");

                if (itemAssetsCollectionType == null || gameplayDataSettingsType == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] 类型未初始化");
                    return null;
                }

                // 获取默认角色物品类型ID
                var itemAssetsProp = AccessTools.Property(gameplayDataSettingsType, "ItemAssets");
                object? itemAssets = itemAssetsProp?.GetValue(null);
                var defaultItemTypeProp = AccessTools.Property(itemAssets?.GetType(), "DefaultCharacterItemTypeID");
                int itemTypeID = (int)(defaultItemTypeProp?.GetValue(itemAssets) ?? 0);

                // 调用 InstantiateAsync
                var instantiateMethod = AccessTools.Method(itemAssetsCollectionType, "InstantiateAsync", new[] { typeof(int) });
                object? instantiateTask = instantiateMethod?.Invoke(null, new object[] { itemTypeID });
                
                if (instantiateTask == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] InstantiateAsync 返回 null");
                    return null;
                }

                // 同步等待 UniTask 完成
                var characterItem = UniTaskHelper.WaitForUniTaskSync(instantiateTask);
                return characterItem;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 创建角色数据项失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取角色模型预制体
        /// </summary>
        public static object? GetCharacterModelPrefab()
        {
            try
            {
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                var gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");
                
                if (levelManagerType == null || gameplayDataSettingsType == null)
                {
                    return null;
                }

                var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                var levelManager = instanceProp?.GetValue(null);
                if (levelManager == null) return null;

                // 尝试从 LevelManager 获取
                var characterModelField = AccessTools.Field(levelManagerType, "characterModel");
                object? modelPrefab = characterModelField?.GetValue(levelManager);
                
                if (modelPrefab != null)
                {
                    return modelPrefab;
                }

                // 如果 LevelManager 中没有，尝试从 GameplayDataSettings.Prefabs 获取
                var prefabsProp = AccessTools.Property(gameplayDataSettingsType, "Prefabs");
                object? prefabs = prefabsProp?.GetValue(null);
                if (prefabs != null)
                {
                    var characterModelProp = AccessTools.Property(prefabs.GetType(), "CharacterModel");
                    modelPrefab = characterModelProp?.GetValue(prefabs);
                }

                return modelPrefab;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 获取角色模型失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建角色实例
        /// </summary>
        public static object? CreateCharacterInstance(object characterItem, object modelPrefab, Vector3 position, Quaternion rotation)
        {
            try
            {
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                var characterCreatorType = AccessTools.TypeByName("CharacterCreator");
                if (levelManagerType == null || characterCreatorType == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] 类型未初始化");
                    return null;
                }

                var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                var levelManager = instanceProp?.GetValue(null);
                if (levelManager == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] LevelManager 未初始化");
                    return null;
                }

                var creatorProp = AccessTools.Property(levelManagerType, "CharacterCreator");
                var characterCreator = creatorProp?.GetValue(levelManager);
                if (characterCreator == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] CharacterCreator 未找到");
                    return null;
                }

                // 调用 CreateCharacter
                var createMethod = AccessTools.Method(characterCreatorType, "CreateCharacter");
                object? createTask = createMethod?.Invoke(characterCreator, new object[] { 
                    characterItem, modelPrefab, position, rotation 
                });

                if (createTask == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] CreateCharacter 返回 null");
                    return null;
                }

                // 同步等待 UniTask 完成
                var newCharacter = UniTaskHelper.WaitForUniTaskSync(createTask);
                return newCharacter;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 创建角色实例失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 配置角色（设置名称、位置、队伍、血量等）
        /// </summary>
        public static bool ConfigureCharacter(object character, string name, Vector3 position, int team)
        {
            try
            {
                Component? characterComponent = character as Component;
                if (characterComponent == null)
                {
                    UnityEngine.Debug.LogError("[CharacterCreationUtils] 角色不是Component类型");
                    return false;
                }

                GameObject unitObject = characterComponent.gameObject;
                unitObject.name = name;
                characterComponent.transform.position = position;

                // 设置队伍
                var teamsType = AccessTools.TypeByName("Teams");
                if (teamsType != null)
                {
                    string[] teamEnumNames = { "player", "scav", "middle" };
                    if (team >= 0 && team < teamEnumNames.Length)
                    {
                        object teamValue = Enum.Parse(teamsType, teamEnumNames[team]);
                        var setTeamMethod = AccessTools.Method(character.GetType(), "SetTeam");
                        setTeamMethod?.Invoke(character, new object[] { teamValue });
                    }
                }

                // 初始化血量
                var healthProp = AccessTools.Property(character.GetType(), "Health");
                object? health = healthProp?.GetValue(character);
                if (health != null)
                {
                    var initMethod = AccessTools.Method(health.GetType(), "Init", Type.EmptyTypes);
                    initMethod?.Invoke(health, null);
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 配置角色失败: {ex.Message}");
                return false;
            }
        }
    }
}

