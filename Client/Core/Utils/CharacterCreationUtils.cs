using System;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.Utils
{
    public static class CharacterCreationUtils
    {
        public static object? CreateCharacterItem()
        {
            var itemAssetsCollectionType = AccessTools.TypeByName("ItemStatsSystem.ItemAssetsCollection");
            var gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");

            var itemAssetsProp = AccessTools.Property(gameplayDataSettingsType, "ItemAssets");
            object? itemAssets = itemAssetsProp?.GetValue(null);
            var defaultItemTypeProp = AccessTools.Property(itemAssets?.GetType(), "DefaultCharacterItemTypeID");
            int itemTypeID = (int)(defaultItemTypeProp?.GetValue(itemAssets) ?? 0);

            var instantiateMethod = AccessTools.Method(itemAssetsCollectionType, "InstantiateAsync", new[] { typeof(int) });
            object? instantiateTask = instantiateMethod?.Invoke(null, new object[] { itemTypeID });

            return instantiateTask != null ? UniTaskHelper.WaitForUniTaskSync(instantiateTask) : null;
        }

        public static object? GetCharacterModelPrefab()
        {
            var levelManagerType = AccessTools.TypeByName("LevelManager");
            var instanceProp = AccessTools.Property(levelManagerType, "Instance");
            var levelManager = instanceProp?.GetValue(null);

            // 🔥 关键修复：检查 levelManager 是否为 null
            if (levelManager == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] LevelManager.Instance 为 null，无法获取角色模型预制体");
                return null;
            }

            var characterModelField = AccessTools.Field(levelManagerType, "characterModel");
            return characterModelField?.GetValue(levelManager);
        }

        public static object? CreateCharacterInstance(object characterItem, object modelPrefab, Vector3 position, Quaternion rotation)
        {
            var levelManagerType = AccessTools.TypeByName("LevelManager");
            var characterCreatorType = AccessTools.TypeByName("CharacterCreator");

            var instanceProp = AccessTools.Property(levelManagerType, "Instance");
            var levelManager = instanceProp?.GetValue(null);

            // 🔥 关键修复：检查 levelManager 是否为 null
            if (levelManager == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] LevelManager.Instance 为 null，无法创建角色实例");
                return null;
            }

            var creatorProp = AccessTools.Property(levelManagerType, "CharacterCreator");
            var characterCreator = creatorProp?.GetValue(levelManager);

            var createMethod = AccessTools.Method(characterCreatorType, "CreateCharacter");
            object? createTask = createMethod?.Invoke(characterCreator, new object[] { 
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

            var teamsType = AccessTools.TypeByName("Teams");
            string[] teamEnumNames = { "player", "scav", "middle" };
            if (team >= 0 && team < teamEnumNames.Length)
            {
                object teamValue = Enum.Parse(teamsType, teamEnumNames[team]);
                var setTeamMethod = AccessTools.Method(character.GetType(), "SetTeam");
                setTeamMethod?.Invoke(character, new object[] { teamValue });
            }

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

            if (currentPreset == null)
            {
                var presetType = AccessTools.TypeByName("CharacterRandomPreset");
                if (presetType != null)
                {
                    currentPreset = UnityEngine.ScriptableObject.CreateInstance(presetType);
                    if (currentPreset != null && characterPresetProp != null && characterPresetProp.CanWrite)
                    {
                        characterPresetProp.SetValue(character, currentPreset);
                        UnityEngine.Debug.Log("[CharacterCreationUtils] 创建新的 CharacterRandomPreset");
                    }
                }
            }

            if (currentPreset != null)
            {
                var presetType = currentPreset.GetType();
                
                var showHealthBarField = AccessTools.Field(presetType, "showHealthBar");
                if (showHealthBarField != null)
                {
                    showHealthBarField.SetValue(currentPreset, true);
                    UnityEngine.Debug.Log("[CharacterCreationUtils] 设置 showHealthBar = true");
                }
                
                // 🔥 修复：showName 是字段，不是属性
                var showNameField = AccessTools.Field(presetType, "showName");
                if (showNameField != null)
                {
                    showNameField.SetValue(currentPreset, showName);
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 设置 showName (Field) = {showName}");
                }

                var nameKeyField = AccessTools.Field(presetType, "nameKey");
                if (nameKeyField != null)
                {
                    nameKeyField.SetValue(currentPreset, displayName);
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 设置 nameKey = {displayName}");
                }

                var iconTypeField = AccessTools.Field(presetType, "characterIconType");
                if (iconTypeField != null)
                {
                    var iconEnumType = AccessTools.TypeByName("CharacterIconTypes");
                    if (iconEnumType != null)
                    {
                        object iconValue = Enum.Parse(iconEnumType, "pmc");
                        iconTypeField.SetValue(currentPreset, iconValue);
                        UnityEngine.Debug.Log("[CharacterCreationUtils] 设置 characterIconType = pmc");
                    }
                }
                
                // 验证设置
                var displayNameProp = AccessTools.Property(presetType, "DisplayName");
                if (displayNameProp != null)
                {
                    object? actualDisplayName = displayNameProp.GetValue(currentPreset);
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 验证 DisplayName = {actualDisplayName}");
                }
                
                // 🔥 验证 showName 字段
                var verifyShowName = showNameField?.GetValue(currentPreset);
                UnityEngine.Debug.Log($"[CharacterCreationUtils] 验证 showName (Field) = {verifyShowName}");
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
        /// 禁用角色的移动脚本 - 防止角色掉落和移动
        /// ⚠️ 已废弃：现在使用 MarkAsRemotePlayer() + Movement 补丁实现
        /// 参见：Client/Patches/MovementPatch.cs
        /// </summary>
        [System.Obsolete("已废弃：现在使用 MarkAsRemotePlayer() + Movement 补丁实现")]
        public static void DisableMovement(object character)
        {
            Component? characterComponent = character as Component;
            if (characterComponent == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 无法禁用移动: character 不是 Component");
                return;
            }

            // 1. 禁用 Movement 组件
            var movementType = AccessTools.TypeByName("Movement");
            if (movementType != null)
            {
                var movement = characterComponent.GetComponent(movementType);
                if (movement != null && movement is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 已禁用 Movement 组件");
                }
            }

            // 2. 禁用 CharacterMovement (ECM2 组件)
            var characterMovementType = AccessTools.TypeByName("ECM2.CharacterMovement");
            if (characterMovementType != null)
            {
                var characterMovement = characterComponent.GetComponentInChildren(characterMovementType);
                if (characterMovement != null && characterMovement is Behaviour ecmBehaviour)
                {
                    ecmBehaviour.enabled = false;
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 已禁用 ECM2.CharacterMovement 组件");
                }
            }

            // 3. 禁用 CharacterMainControl 组件（可能控制角色整体行为）
            var characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
            if (characterMainControlType != null)
            {
                var mainControl = characterComponent.GetComponent(characterMainControlType);
                if (mainControl != null && mainControl is Behaviour mainControlBehaviour)
                {
                    mainControlBehaviour.enabled = false;
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 已禁用 CharacterMainControl 组件");
                }
            }

           
            UnityEngine.Debug.Log($"[CharacterCreationUtils] 已完成禁用角色移动脚本，角色应保持静止");
        }

        /// <summary>
        /// 从距离管理系统中移除角色（防止远程玩家被自动禁用）
        /// </summary>
        public static void UnregisterFromDistanceSystem(object character)
        {
            Component? characterComponent = character as Component;
            if (characterComponent == null) return;

            try
            {
                var setActiveByPlayerDistanceType = AccessTools.TypeByName("Duckov.Utilities.SetActiveByPlayerDistance");
                if (setActiveByPlayerDistanceType != null)
                {
                    var unregisterMethod = AccessTools.Method(setActiveByPlayerDistanceType, "Unregister",
                        new[] { typeof(GameObject), typeof(int) });

                    if (unregisterMethod != null)
                    {
                        int sceneBuildIndex = characterComponent.gameObject.scene.buildIndex;
                        unregisterMethod.Invoke(null, new object[] { characterComponent.gameObject, sceneBuildIndex });
                        UnityEngine.Debug.Log($"[CharacterCreationUtils] ✅ 已从距离管理系统移除角色 (场景索引: {sceneBuildIndex})");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 未找到 SetActiveByPlayerDistance.Unregister 方法");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 未找到 SetActiveByPlayerDistance 类型");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCreationUtils] 从距离系统移除失败: {ex.Message}");
            }
        }

        public static void RequestHealthBar(object character, string displayName, UnityEngine.Sprite? customIcon = null)
        {
            var healthProp = AccessTools.Property(character.GetType(), "Health");
            object? health = healthProp?.GetValue(character);
            
            if (health != null)
            {
                var showHealthBarProp = AccessTools.Property(health.GetType(), "showHealthBar");
                if (showHealthBarProp != null && showHealthBarProp.CanWrite)
                {
                    showHealthBarProp.SetValue(health, true);
                }

                var requestMethod = AccessTools.Method(health.GetType(), "RequestHealthBar", Type.EmptyTypes);
                requestMethod?.Invoke(health, null);
                
                // 延迟设置名称文本，等待 HealthBar 创建完成
                if (health is UnityEngine.MonoBehaviour mb)
                {
                    mb.StartCoroutine(SetHealthBarNameDelayed(health, displayName, customIcon));
                }
            }
        }

        private static System.Collections.IEnumerator SetHealthBarNameDelayed(object health, string displayName, UnityEngine.Sprite? customIcon)
        {
            yield return null; // 等待一帧，让 HealthBar 创建完成
            
            var healthBarManagerType = AccessTools.TypeByName("Duckov.UI.HealthBarManager");
            if (healthBarManagerType == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] 未找到 HealthBarManager 类型");
                yield break;
            }
            
            var instanceProp = AccessTools.Property(healthBarManagerType, "Instance");
            object? healthBarManager = instanceProp?.GetValue(null);
            
            if (healthBarManager == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationUtils] HealthBarManager.Instance 为空");
                yield break;
            }
            
            var getActiveHealthBarMethod = AccessTools.Method(healthBarManagerType, "GetActiveHealthBar");
            
            // 🔥 持续设置 10 秒，每 0.2 秒设置一次
            // 这样可以覆盖任何因事件触发的 RefreshCharacterIcon()
            float duration = 10f;
            float interval = 0.2f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                object? healthBar = getActiveHealthBarMethod?.Invoke(healthBarManager, new object[] { health });
                
                if (healthBar != null)
                {
                    // 🔥 强制刷新血条图标和名字（调用 RefreshCharacterIcon）
                    var refreshIconMethod = AccessTools.Method(healthBar.GetType(), "RefreshCharacterIcon");
                    if (refreshIconMethod != null)
                    {
                        refreshIconMethod.Invoke(healthBar, null);
                    }

                    // 直接设置 nameText（双重保险）
                    var nameTextField = AccessTools.Field(healthBar.GetType(), "nameText");
                    object? nameText = nameTextField?.GetValue(healthBar);
                    
                    if (nameText != null)
                    {
                        var textProp = AccessTools.Property(nameText.GetType(), "text");
                        if (textProp != null && textProp.CanWrite)
                        {
                            string currentText = textProp.GetValue(nameText)?.ToString() ?? "";
                            
                            // 只有当文本被改变时才重新设置
                            if (currentText != displayName)
                            {
                                textProp.SetValue(nameText, displayName);
                                UnityEngine.Debug.Log($"[CharacterCreationUtils] 🔄 重新设置 HealthBar.nameText = {displayName}");
                            }
                        }
                        
                        // 强制激活名字显示
                        var gameObjectProp = AccessTools.Property(nameText.GetType(), "gameObject");
                        object? gameObject = gameObjectProp?.GetValue(nameText);
                        if (gameObject != null)
                        {
                            var setActiveMethod = AccessTools.Method(gameObject.GetType(), "SetActive");
                            setActiveMethod?.Invoke(gameObject, new object[] { true });
                        }
                    }
                    
                    // 首次设置图标（之后不重复设置）
                    if (elapsed < interval)
                    {
                        SetHealthBarIcon(healthBar, customIcon);
                        UnityEngine.Debug.Log($"[CharacterCreationUtils] 🎨 初始设置 HealthBar 名字 = {displayName}");
                    }
                }
                
                yield return new UnityEngine.WaitForSeconds(interval);
                elapsed += interval;
            }
            
            UnityEngine.Debug.Log($"[CharacterCreationUtils] ✅ HealthBar 名字持续设置完成 ({duration}秒)");
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
                        // 将 Texture2D 转换为 Sprite
                        var texture = localPlayer.AvatarTexture;
                        var sprite = UnityEngine.Sprite.Create(
                            texture,
                            new UnityEngine.Rect(0, 0, texture.width, texture.height),
                            new UnityEngine.Vector2(0.5f, 0.5f)
                        );
                        
                        UnityEngine.Debug.Log("[CharacterCreationUtils] 使用 Steam 头像作为图标");
                        return sprite;
                    }
                }
                
                // 如果 Steam 头像不可用，使用本地玩家的角色预设图标
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                var levelManager = instanceProp?.GetValue(null);
                
                if (levelManager != null)
                {
                    var mainCharProp = AccessTools.Property(levelManagerType, "MainCharacter");
                    var mainChar = mainCharProp?.GetValue(levelManager);
                    
                    if (mainChar != null)
                    {
                        var characterPresetProp = AccessTools.Property(mainChar.GetType(), "characterPreset");
                        var preset = characterPresetProp?.GetValue(mainChar);
                        
                        if (preset != null)
                        {
                            var getIconMethod = AccessTools.Method(preset.GetType(), "GetCharacterIcon");
                            var icon = getIconMethod?.Invoke(preset, null);
                            
                            if (icon is UnityEngine.Sprite sprite)
                            {
                                UnityEngine.Debug.Log("[CharacterCreationUtils] 使用本地玩家角色预设图标");
                                return sprite;
                            }
                        }
                    }
                }
                
                // 如果都不可用，使用宠物图标作为默认图标
                var gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");
                if (gameplayDataSettingsType != null)
                {
                    var uiStyleProp = AccessTools.Property(gameplayDataSettingsType, "UIStyle");
                    var uiStyle = uiStyleProp?.GetValue(null);
                    
                    if (uiStyle != null)
                    {
                        var petIconProp = AccessTools.Property(uiStyle.GetType(), "PetCharacterIcon");
                        var petIcon = petIconProp?.GetValue(uiStyle);
                        
                        if (petIcon is UnityEngine.Sprite sprite)
                        {
                            UnityEngine.Debug.Log("[CharacterCreationUtils] 使用默认宠物图标");
                            return sprite;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CharacterCreationUtils] 获取本地玩家图标失败: {ex.Message}");
            }
            
            return null;
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