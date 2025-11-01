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

            var characterModelField = AccessTools.Field(levelManagerType, "characterModel");
            return characterModelField?.GetValue(levelManager);
        }

        public static object? CreateCharacterInstance(object characterItem, object modelPrefab, Vector3 position, Quaternion rotation)
        {
            var levelManagerType = AccessTools.TypeByName("LevelManager");
            var characterCreatorType = AccessTools.TypeByName("CharacterCreator");

            var instanceProp = AccessTools.Property(levelManagerType, "Instance");
            var levelManager = instanceProp?.GetValue(null);

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

        public static void RequestHealthBar(object character)
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
                UnityEngine.MonoBehaviour mb = health as UnityEngine.MonoBehaviour;
                if (mb != null)
                {
                    mb.StartCoroutine(SetHealthBarNameDelayed(health));
                }
            }
        }

        private static System.Collections.IEnumerator SetHealthBarNameDelayed(object health)
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
                                textProp.SetValue(nameText, "测试名字");
                                UnityEngine.Debug.Log("[CharacterCreationUtils] 直接设置 HealthBar.nameText = 测试名字");
                            }
                            
                            var gameObjectProp = AccessTools.Property(nameText.GetType(), "gameObject");
                            object? gameObject = gameObjectProp?.GetValue(nameText);
                            if (gameObject != null)
                            {
                                var setActiveMethod = AccessTools.Method(gameObject.GetType(), "SetActive");
                                setActiveMethod?.Invoke(gameObject, new object[] { true });
                            }
                        }
                    }
                }
            }
        }
    }
}