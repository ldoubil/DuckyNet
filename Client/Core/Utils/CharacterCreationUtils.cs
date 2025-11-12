using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.Utils
{
    public static class CharacterCreationUtils
    {
        /// <summary>
        /// 缓存反射类型和成员，避免重复查找
        /// </summary>
        private static class CachedReflection
        {
            // 类型缓存
            public static readonly Type? LevelManagerType = AccessTools.TypeByName("LevelManager");
            public static readonly Type? CharacterCreatorType = AccessTools.TypeByName("CharacterCreator");
            public static readonly Type? CharacterRandomPresetType = AccessTools.TypeByName("CharacterRandomPreset");
            public static readonly Type? ItemAssetsCollectionType = AccessTools.TypeByName("ItemStatsSystem.ItemAssetsCollection");
            public static readonly Type? GameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");
            public static readonly Type? TeamsType = AccessTools.TypeByName("Teams");
            public static readonly Type? CharacterIconTypesType = AccessTools.TypeByName("CharacterIconTypes");
            public static readonly Type? MovementType = AccessTools.TypeByName("Movement");
            public static readonly Type? SetActiveByPlayerDistanceType = AccessTools.TypeByName("Duckov.Utilities.SetActiveByPlayerDistance");
            public static readonly Type? HealthBarManagerType = AccessTools.TypeByName("Duckov.UI.HealthBarManager");

            // 属性缓存
            public static readonly PropertyInfo? LevelManagerInstance = AccessTools.Property(LevelManagerType, "Instance");
            public static readonly PropertyInfo? MainCharacter = AccessTools.Property(LevelManagerType, "MainCharacter");
            public static readonly PropertyInfo? CharacterCreatorProp = AccessTools.Property(LevelManagerType, "CharacterCreator");
            public static readonly PropertyInfo? ItemAssets = AccessTools.Property(GameplayDataSettingsType, "ItemAssets");
            public static readonly PropertyInfo? HealthBarManagerInstance = AccessTools.Property(HealthBarManagerType, "Instance");

            // 字段缓存
            public static readonly FieldInfo? CharacterModelField = AccessTools.Field(LevelManagerType, "characterModel");

            // 方法缓存
            public static readonly MethodInfo? InstantiateAsyncMethod = AccessTools.Method(ItemAssetsCollectionType, "InstantiateAsync", new[] { typeof(int) });
            public static readonly MethodInfo? CreateCharacterMethod = AccessTools.Method(CharacterCreatorType, "CreateCharacter");
            public static readonly MethodInfo? UnregisterMethod = SetActiveByPlayerDistanceType != null 
                ? AccessTools.Method(SetActiveByPlayerDistanceType, "Unregister", new[] { typeof(GameObject), typeof(int) }) 
                : null;
            public static readonly MethodInfo? GetActiveHealthBarMethod = AccessTools.Method(HealthBarManagerType, "GetActiveHealthBar");
        }

        public static object? CreateCharacterItem()
        {
            if (CachedReflection.ItemAssets == null || CachedReflection.InstantiateAsyncMethod == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 缺少必要的反射成员");
                return null;
            }

            object? itemAssets = CachedReflection.ItemAssets.GetValue(null);
            var defaultItemTypeProp = AccessTools.Property(itemAssets?.GetType(), "DefaultCharacterItemTypeID");
            int itemTypeID = (int)(defaultItemTypeProp?.GetValue(itemAssets) ?? 0);

            object? instantiateTask = CachedReflection.InstantiateAsyncMethod.Invoke(null, new object[] { itemTypeID });

            return instantiateTask != null ? UniTaskHelper.WaitForUniTaskSync(instantiateTask) : null;
        }

        public static object? GetCharacterModelPrefab()
        {
            if (CachedReflection.LevelManagerInstance == null || CachedReflection.CharacterModelField == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 缺少必要的反射成员");
                return null;
            }

            var levelManager = CachedReflection.LevelManagerInstance.GetValue(null);
            if (levelManager == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] LevelManager.Instance 为 null，无法获取角色模型预制体");
                return null;
            }

            return CachedReflection.CharacterModelField.GetValue(levelManager);
        }

        public static object? CreateCharacterInstance(object characterItem, object modelPrefab, Vector3 position, Quaternion rotation)
        {
            if (CachedReflection.LevelManagerInstance == null || 
                CachedReflection.CharacterCreatorProp == null || 
                CachedReflection.CreateCharacterMethod == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 缺少必要的反射成员");
                return null;
            }

            var levelManager = CachedReflection.LevelManagerInstance.GetValue(null);
            if (levelManager == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] LevelManager.Instance 为 null，无法创建角色实例");
                return null;
            }

            var characterCreator = CachedReflection.CharacterCreatorProp.GetValue(levelManager);
            object? createTask = CachedReflection.CreateCharacterMethod.Invoke(characterCreator, new object[] { 
                characterItem, modelPrefab, position, rotation 
            });

            return createTask != null ? UniTaskHelper.WaitForUniTaskSync(createTask) : null;
        }

        public static void ConfigureCharacter(object character, string name, Vector3 position, int team)
        {
            Component? characterComponent = character as Component;
            if (characterComponent == null) return;

            characterComponent.gameObject.name = name;
            characterComponent.transform.position = position;

            // 设置队伍
            if (CachedReflection.TeamsType != null && team >= 0 && team <= 2)
            {
                string[] teamEnumNames = { "player", "scav", "middle" };
                object teamValue = Enum.Parse(CachedReflection.TeamsType, teamEnumNames[team]);
                var setTeamMethod = AccessTools.Method(character.GetType(), "SetTeam");
                setTeamMethod?.Invoke(character, new object[] { teamValue });
            }

            // 初始化血量
            var healthProp = AccessTools.Property(character.GetType(), "Health");
            object? health = healthProp?.GetValue(character);
            if (health != null)
            {
                var initMethod = AccessTools.Method(health.GetType(), "Init", Type.EmptyTypes);
                initMethod?.Invoke(health, null);
            }
        }

        public static void ConfigureCharacterPreset(object character, string displayName, bool showName = true)
        {
            var charType = character.GetType();
            var characterPresetProp = AccessTools.Property(charType, "characterPreset");
            object? currentPreset = characterPresetProp?.GetValue(character);

            // 如果预设不存在，创建新的
            if (currentPreset == null && CachedReflection.CharacterRandomPresetType != null)
            {
                currentPreset = UnityEngine.ScriptableObject.CreateInstance(CachedReflection.CharacterRandomPresetType);
                characterPresetProp?.SetValue(character, currentPreset);
            }

            if (currentPreset == null) return;

            var presetType = currentPreset.GetType();
            
            // 一次性设置所有字段（无需重复验证）
            AccessTools.Field(presetType, "showHealthBar")?.SetValue(currentPreset, true);
            AccessTools.Field(presetType, "showName")?.SetValue(currentPreset, showName);
            AccessTools.Field(presetType, "nameKey")?.SetValue(currentPreset, displayName);
            
            // 设置图标类型
            if (CachedReflection.CharacterIconTypesType != null)
            {
                AccessTools.Field(presetType, "characterIconType")?.SetValue(
                    currentPreset, 
                    Enum.Parse(CachedReflection.CharacterIconTypesType, "pmc")
                );
            }
        }

        /// <summary>
        /// 标记角色为远程玩家 - 通过名称后缀让 Movement 补丁识别并跳过更新
        /// </summary>
        public static void MarkAsRemotePlayer(object character)
        {
            Component? characterComponent = character as Component;
            if (characterComponent == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 无法标记远程玩家: character 不是 Component");
                return;
            }

            try
            {
                // 使用名称后缀标记（避免 Tag 未定义的错误）
                if (!characterComponent.gameObject.name.Contains("[RemotePlayer]"))
                {
                    characterComponent.gameObject.name += " [RemotePlayer]";
                }
                UnityEngine.Debug.Log($"[CharacterCreationUtils] ✅ 已标记为远程玩家: {characterComponent.gameObject.name}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 标记失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 从距离管理系统中移除角色（防止远程玩家被自动禁用）
        /// </summary>
        public static void UnregisterFromDistanceSystem(object character)
        {
            Component? characterComponent = character as Component;
            if (characterComponent == null) return;

            if (CachedReflection.UnregisterMethod != null)
            {
                try
                {
                    int sceneBuildIndex = characterComponent.gameObject.scene.buildIndex;
                    CachedReflection.UnregisterMethod.Invoke(null, new object[] { characterComponent.gameObject, sceneBuildIndex });
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[CharacterCreationUtils] 从距离系统移除失败: {ex.Message}");
                }
            }
        }

        public static void RequestHealthBar(object character, string displayName, UnityEngine.Sprite? customIcon = null)
        {
            // 先配置预设（HealthBar 会从预设中读取名称）
            ConfigureCharacterPreset(character, displayName, showName: true);

            var healthProp = AccessTools.Property(character.GetType(), "Health");
            object? health = healthProp?.GetValue(character);
            
            if (health != null)
            {
                var showHealthBarProp = AccessTools.Property(health.GetType(), "showHealthBar");
                showHealthBarProp?.SetValue(health, true);

                var requestMethod = AccessTools.Method(health.GetType(), "RequestHealthBar", Type.EmptyTypes);
                requestMethod?.Invoke(health, null);
                
                // 如果需要自定义图标，延迟设置（等待 HealthBar 创建）
                if (customIcon != null && health is UnityEngine.MonoBehaviour mb)
                {
                    mb.StartCoroutine(SetHealthBarIconDelayed(health, customIcon));
                }
            }
        }

        private static System.Collections.IEnumerator SetHealthBarIconDelayed(object health, UnityEngine.Sprite customIcon)
        {
            yield return null; // 等待一帧，让 HealthBar 创建完成
            
            if (CachedReflection.HealthBarManagerInstance == null || CachedReflection.GetActiveHealthBarMethod == null)
            {
                yield break;
            }
            
            object? healthBarManager = CachedReflection.HealthBarManagerInstance.GetValue(null);
            if (healthBarManager == null) yield break;
            
            object? healthBar = CachedReflection.GetActiveHealthBarMethod.Invoke(healthBarManager, new object[] { health });
            if (healthBar != null)
            {
                SetHealthBarIcon(healthBar, customIcon);
            }
        }

        private static void SetHealthBarIcon(object healthBar, UnityEngine.Sprite? customIcon)
        {
            var levelIconField = AccessTools.Field(healthBar.GetType(), "levelIcon");
            object? levelIcon = levelIconField?.GetValue(healthBar);
            
            if (levelIcon == null) return;
            
            // 如果有自定义图标，使用自定义图标
            if (customIcon != null)
            {
                var spriteProp = AccessTools.Property(levelIcon.GetType(), "sprite");
                if (spriteProp != null && spriteProp.CanWrite)
                {
                    spriteProp.SetValue(levelIcon, customIcon);
                    UnityEngine.Debug.Log("[CharacterCreationUtils] 设置自定义图标");
                }
                
                var iconGameObjectProp = AccessTools.Property(levelIcon.GetType(), "gameObject");
                object? iconGameObject = iconGameObjectProp?.GetValue(levelIcon);
                if (iconGameObject != null)
                {
                    var setActiveMethod = AccessTools.Method(iconGameObject.GetType(), "SetActive");
                    setActiveMethod?.Invoke(iconGameObject, new object[] { true });
                    UnityEngine.Debug.Log("[CharacterCreationUtils] 激活 HealthBar.levelIcon");
                }
            }
            else
            {
                // 没有自定义图标，隐藏图标
                var iconGameObjectProp = AccessTools.Property(levelIcon.GetType(), "gameObject");
                object? iconGameObject = iconGameObjectProp?.GetValue(levelIcon);
                if (iconGameObject != null)
                {
                    var setActiveMethod = AccessTools.Method(iconGameObject.GetType(), "SetActive");
                    setActiveMethod?.Invoke(iconGameObject, new object[] { false });
                    UnityEngine.Debug.Log("[CharacterCreationUtils] 隐藏 HealthBar.levelIcon (无自定义图标)");
                }
            }
        }

        public static UnityEngine.Sprite? GetLocalPlayerIcon()
        {
            try
            {
                // 尝试从 GameContext 获取本地玩家的 Steam 头像
                if (GameContext.IsInitialized)
                {
                    var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
                    if (localPlayer?.AvatarTexture != null)
                    {
                        var texture = localPlayer.AvatarTexture;
                        return UnityEngine.Sprite.Create(
                            texture,
                            new UnityEngine.Rect(0, 0, texture.width, texture.height),
                            new UnityEngine.Vector2(0.5f, 0.5f)
                        );
                    }
                }
                
                // 备选：使用本地玩家的角色预设图标
                if (CachedReflection.LevelManagerInstance != null && CachedReflection.MainCharacter != null)
                {
                    var levelManager = CachedReflection.LevelManagerInstance.GetValue(null);
                    if (levelManager != null)
                    {
                        var mainChar = CachedReflection.MainCharacter.GetValue(levelManager);
                        if (mainChar != null)
                        {
                            var preset = AccessTools.Property(mainChar.GetType(), "characterPreset")?.GetValue(mainChar);
                            if (preset != null)
                            {
                                return AccessTools.Method(preset.GetType(), "GetCharacterIcon")?.Invoke(preset, null) as UnityEngine.Sprite;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CharacterCreationUtils] 获取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 统一的角色创建和配置方法
        /// </summary>
        public static object? CreateAndConfigureCharacter(
            Vector3 position, 
            Quaternion rotation, 
            string displayName, 
            int team = 0,
            bool isRemotePlayer = true)
        {
            var characterItem = CreateCharacterItem();
            if (characterItem == null)
            {
                UnityEngine.Debug.LogError("[CharacterCreationUtils] 创建角色物品失败");
                return null;
            }

            var modelPrefab = GetCharacterModelPrefab();
            if (modelPrefab == null)
            {
                UnityEngine.Debug.LogError("[CharacterCreationUtils] 获取角色模型失败");
                return null;
            }

            var character = CreateCharacterInstance(characterItem, modelPrefab, position, rotation);
            if (character == null)
            {
                UnityEngine.Debug.LogError("[CharacterCreationUtils] 创建角色实例失败");
                return null;
            }

            // 按正确顺序配置
            ConfigureCharacterPreset(character, displayName, showName: true);
            ConfigureCharacter(character, displayName, position, team);
            
            if (isRemotePlayer)
            {
                MarkAsRemotePlayer(character);
                UnregisterFromDistanceSystem(character);
            }
            
            RequestHealthBar(character, displayName);

            return character;
        }

        /// <summary>
        /// 应用自定义外观数据到角色
        /// </summary>
        /// <param name="character">角色对象（CharacterMainControl 或类似类型）</param>
        /// <param name="faceData">CustomFaceSettingData 外观数据</param>
        /// <returns>成功返回 true</returns>
        public static bool ApplyCustomFace(object character, object faceData)
        {
            try
            {
                if (character == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCreationUtils] character 为空");
                    return false;
                }

                if (faceData == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCreationUtils] faceData 为空");
                    return false;
                }

                // 通过字段获取 CharacterModel（字段是正确的方式）
                var characterModelField = AccessTools.Field(character.GetType(), "characterModel");
                var characterModel = characterModelField?.GetValue(character);
                
                if (characterModel == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCreationUtils] CharacterModel 为空");
                    return false;
                }

                // 调用 SetFaceFromData 方法应用外观
                var setFaceMethod = AccessTools.Method(characterModel.GetType(), "SetFaceFromData");
                if (setFaceMethod != null)
                {
                    setFaceMethod.Invoke(characterModel, new object[] { faceData });
                    UnityEngine.Debug.Log("[CharacterCreationUtils] 成功应用外观数据");
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 未找到 SetFaceFromData 方法");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 应用外观失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}