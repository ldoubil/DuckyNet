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
                
                var showNameProp = AccessTools.Property(presetType, "showName");
                if (showNameProp != null && showNameProp.CanWrite)
                {
                    showNameProp.SetValue(currentPreset, showName);
                    UnityEngine.Debug.Log($"[CharacterCreationUtils] 设置 showName = {showName}");
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
            }
        }

        /// <summary>
        /// 禁用角色的移动脚本 - 防止角色掉落和移动
        /// Movement 脚本会在每帧手动添加重力，即使 Rigidbody.isKinematic = true 也无法阻止
        /// 必须禁用 Movement 组件才能完全停止角色的移动和下落
        /// </summary>
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
            
            // 查找对应的 HealthBar
            var healthBarManagerType = AccessTools.TypeByName("Duckov.UI.HealthBarManager");
            if (healthBarManagerType != null)
            {
                var instanceProp = AccessTools.Property(healthBarManagerType, "Instance");
                object? healthBarManager = instanceProp?.GetValue(null);
                
                if (healthBarManager != null)
                {
                    var getActiveHealthBarMethod = AccessTools.Method(healthBarManagerType, "GetActiveHealthBar");
                    object? healthBar = getActiveHealthBarMethod?.Invoke(healthBarManager, new object[] { health });
                    
                    if (healthBar != null)
                    {
                        // 直接设置 nameText
                        var nameTextField = AccessTools.Field(healthBar.GetType(), "nameText");
                        object? nameText = nameTextField?.GetValue(healthBar);
                        
                        if (nameText != null)
                        {
                            var textProp = AccessTools.Property(nameText.GetType(), "text");
                            if (textProp != null && textProp.CanWrite)
                            {
                                textProp.SetValue(nameText, displayName); // 🔥 使用传入的 displayName
                                UnityEngine.Debug.Log($"[CharacterCreationUtils] 直接设置 HealthBar.nameText = {displayName}");
                            }
                            
                            var gameObjectProp = AccessTools.Property(nameText.GetType(), "gameObject");
                            object? gameObject = gameObjectProp?.GetValue(nameText);
                            if (gameObject != null)
                            {
                                var setActiveMethod = AccessTools.Method(gameObject.GetType(), "SetActive");
                                setActiveMethod?.Invoke(gameObject, new object[] { true });
                            }
                        }
                        
                        // 设置自定义图标
                        SetHealthBarIcon(healthBar, customIcon);
                    }
                }
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
    }
}