using System;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 角色自定义管理器 - 管理角色外观定制（捏脸、部位、颜色等）
    /// </summary>
    public class CharacterCustomizationManager : IDisposable
    {
        private Type? _customFaceSettingDataType;
        private Type? _customFaceHeadSettingType;
        private Type? _customFacePartInfoType;
        private Type? _customFacePartTypesType;
        private bool _initialized = false;

        public CharacterCustomizationManager()
        {
            InitializeTypes();
        }

        private void InitializeTypes()
        {
            try
            {
                _customFaceSettingDataType = AccessTools.TypeByName("CustomFaceSettingData");
                _customFaceHeadSettingType = AccessTools.TypeByName("CustomFaceHeadSetting");
                _customFacePartInfoType = AccessTools.TypeByName("CustomFacePartInfo");
                _customFacePartTypesType = AccessTools.TypeByName("CustomFacePartTypes");

                _initialized = _customFaceSettingDataType != null 
                    && _customFaceHeadSettingType != null 
                    && _customFacePartInfoType != null;

                if (_initialized)
                {
                    UnityEngine.Debug.Log("[CharacterCustomizationManager] 类型初始化成功");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] 类型初始化失败");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建默认自定义数据（从游戏默认预设获取）
        /// </summary>
        public object CreateDefaultCustomization()
        {
            if (!_initialized || _customFaceSettingDataType == null)
            {
                UnityEngine.Debug.LogError("[CharacterCustomizationManager] 未初始化");
                return null!;
            }

            try
            {
                // 尝试从游戏的默认预设获取
                var gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");
                if (gameplayDataSettingsType != null)
                {
                    var customFaceDataProp = AccessTools.Property(gameplayDataSettingsType, "CustomFaceData");
                    object? customFaceData = customFaceDataProp?.GetValue(null);
                    
                    if (customFaceData != null)
                    {
                        var defaultPresetProp = AccessTools.Property(customFaceData.GetType(), "DefaultPreset");
                        object? defaultPreset = defaultPresetProp?.GetValue(customFaceData);
                        
                        if (defaultPreset != null)
                        {
                            var settingsField = AccessTools.Field(defaultPreset.GetType(), "settings");
                            object? settings = settingsField?.GetValue(defaultPreset);
                            
                            if (settings != null)
                            {
                                return settings;
                            }
                        }
                    }
                }

                // 如果无法获取默认预设，创建基础默认数据
                object customData = Activator.CreateInstance(_customFaceSettingDataType);

                // 设置默认头部设置
                object headSetting = CreateDefaultHeadSetting();
                var headSettingField = AccessTools.Field(_customFaceSettingDataType, "headSetting");
                headSettingField?.SetValue(customData, headSetting);

                return customData;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] 创建失败: {ex.Message}");
                return null!;
            }
        }

        /// <summary>
        /// 创建自定义数据
        /// </summary>
        public object CreateCustomization(CharacterCustomization config)
        {
            if (!_initialized || _customFaceSettingDataType == null)
            {
                UnityEngine.Debug.LogError("[CharacterCustomizationManager] 未初始化");
                return null!;
            }

            try
            {
                object customData = Activator.CreateInstance(_customFaceSettingDataType);

                // 设置头部
                object headSetting = CreateHeadSetting(
                    config.MainColor,
                    config.HeadScaleOffset,
                    config.ForeheadHeight,
                    config.ForeheadRound
                );
                var headSettingField = AccessTools.Field(_customFaceSettingDataType, "headSetting");
                headSettingField?.SetValue(customData, headSetting);

                // 设置各个部位
                SetPart(customData, "hairID", "hairInfo", config.HairID, config.HairInfo);
                SetPart(customData, "eyeID", "eyeInfo", config.EyeID, config.EyeInfo);
                SetPart(customData, "eyebrowID", "eyebrowInfo", config.EyebrowID, config.EyebrowInfo);
                SetPart(customData, "mouthID", "mouthInfo", config.MouthID, config.MouthInfo);
                SetPart(customData, "tailID", "tailInfo", config.TailID, config.TailInfo);
                SetPart(customData, "footID", "footInfo", config.FootID, config.FootInfo);
                SetPart(customData, "wingID", "wingInfo", config.WingID, config.WingInfo);

                return customData;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] 创建失败: {ex.Message}");
                return null!;
            }
        }

        /// <summary>
        /// 应用自定义到角色模型
        /// </summary>
        public void ApplyToCharacter(object character, object customData)
        {
            try
            {
                var component = character as Component;
                if (component == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] 角色不是Component类型");
                    return;
                }

                // 获取 CharacterModel（注意：是字段不是属性）
                var characterModelField = AccessTools.Field(character.GetType(), "characterModel");
                object? characterModel = characterModelField?.GetValue(character);

                if (characterModel == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] CharacterModel 字段为空，角色可能未完全初始化");
                    return;
                }

                // 调用 SetFaceFromData
                var setFaceMethod = AccessTools.Method(characterModel.GetType(), "SetFaceFromData", new[] { _customFaceSettingDataType });
                if (setFaceMethod != null)
                {
                    setFaceMethod.Invoke(characterModel, new[] { customData });
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] SetFaceFromData 方法未找到");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] 应用失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 从JSON加载自定义数据
        /// </summary>
        public object? LoadFromJson(string json)
        {
            try
            {
                var jsonToDataMethod = AccessTools.Method(_customFaceSettingDataType, "JsonToData");
                if (jsonToDataMethod != null)
                {
                    object[] parameters = new object[] { json, null! };
                    bool success = (bool)jsonToDataMethod.Invoke(null, parameters);
                    if (success)
                    {
                        return parameters[1];
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] JSON 解析失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 转换为JSON
        /// </summary>
        public string? ToJson(object customData)
        {
            try
            {
                var toJsonMethod = AccessTools.Method(customData.GetType(), "DataToJson");
                if (toJsonMethod != null)
                {
                    return (string)toJsonMethod.Invoke(customData, null);
                }
                
                UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] DataToJson 方法未找到");
                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] JSON 序列化失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从角色对象获取自定义数据
        /// </summary>
        public object? GetCustomizationFromCharacter(object character)
        {
            if (!_initialized || _customFaceSettingDataType == null)
            {
                UnityEngine.Debug.LogError("[CharacterCustomizationManager] 未初始化");
                return null;
            }

            try
            {
                var component = character as Component;
                if (component == null)
                {
                    // 尝试作为GameObject处理
                    var gameObject = character as GameObject;
                    if (gameObject != null)
                    {
                        // 查找 CharacterMainControl 组件（这是游戏中主要的角色控制类）
                        var characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
                        if (characterMainControlType != null)
                        {
                            component = gameObject.GetComponent(characterMainControlType) as Component;
                            
                            if (component == null)
                            {
                                UnityEngine.Debug.LogWarning($"[CharacterCustomizationManager] GameObject 上没有 CharacterMainControl 组件");
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.LogError("[CharacterCustomizationManager] 找不到 CharacterMainControl 类型");
                        }
                    }

                    if (component == null)
                    {
                        UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] 无效的角色对象类型");
                        return null;
                    }
                }

                // 获取 CharacterModel 字段
                var characterModelField = AccessTools.Field(component.GetType(), "characterModel");
                object? characterModel = characterModelField?.GetValue(component);

                if (characterModel == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] CharacterModel 字段为空，角色可能还未完全初始化");
                    return null;
                }

                // 获取 CustomFaceInstance (customFace 字段)
                var customFaceField = AccessTools.Field(characterModel.GetType(), "customFace");
                object? customFace = customFaceField?.GetValue(characterModel);

                if (customFace == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] customFace 字段为空");
                    return null;
                }

                // 调用 ConvertToSaveData() 方法获取外观数据
                var convertToSaveDataMethod = AccessTools.Method(customFace.GetType(), "ConvertToSaveData");
                if (convertToSaveDataMethod != null)
                {
                    object? faceData = convertToSaveDataMethod.Invoke(customFace, null);
                    if (faceData != null)
                    {
                        return faceData;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] ConvertToSaveData() 返回 null");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] ConvertToSaveData 方法未找到");
                }

                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] 获取外观数据失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                return null;
            }
        }

        /// <summary>
        /// 尝试获取本地玩家角色对象
        /// </summary>
        public GameObject? GetLocalPlayerCharacter()
        {
            try
            {
                // 通过 LevelManager.Instance.MainCharacter 获取本地玩家角色
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                if (levelManagerType == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] 找不到 LevelManager 类型");
                    return null;
                }

                var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                object? levelManager = instanceProp?.GetValue(null);

                if (levelManager == null)
                {
                    // LevelManager.Instance 为 null 在角色创建前是正常的，不需要警告日志
                    // （例如：在大厅、主菜单、关卡加载中等情况）
                    return null;
                }

                // ✅ 正确的属性名是 MainCharacter，而不是 LocalCharacter
                var mainCharacterProp = AccessTools.Property(levelManagerType, "MainCharacter");
                if (mainCharacterProp == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] 找不到 MainCharacter 属性");
                    return null;
                }

                object? mainCharacter = mainCharacterProp.GetValue(levelManager);

                if (mainCharacter == null)
                {
                    UnityEngine.Debug.LogWarning("[CharacterCustomizationManager] MainCharacter 为 null（角色可能还未加载）");
                    return null;
                }

                // MainCharacter 是 Character 组件
                var component = mainCharacter as Component;
                if (component != null)
                {
                    return component.gameObject;
                }

                // 如果不是 Component，尝试作为 GameObject
                var gameObject = mainCharacter as GameObject;
                if (gameObject != null)
                {
                    return gameObject;
                }

                UnityEngine.Debug.LogWarning($"[CharacterCustomizationManager] MainCharacter 类型不是 Component 或 GameObject: {mainCharacter.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterCustomizationManager] 获取本地玩家角色失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                return null;
            }
        }

        /// <summary>
        /// 创建头部设置
        /// </summary>
        private object CreateHeadSetting(Color mainColor, float headScaleOffset, float foreheadHeight, float foreheadRound)
        {
            object headSetting = Activator.CreateInstance(_customFaceHeadSettingType);
            
            var mainColorField = AccessTools.Field(_customFaceHeadSettingType, "mainColor");
            var headScaleOffsetField = AccessTools.Field(_customFaceHeadSettingType, "headScaleOffset");
            var foreheadHeightField = AccessTools.Field(_customFaceHeadSettingType, "foreheadHeight");
            var foreheadRoundField = AccessTools.Field(_customFaceHeadSettingType, "foreheadRound");

            mainColorField?.SetValue(headSetting, mainColor);
            headScaleOffsetField?.SetValue(headSetting, headScaleOffset);
            foreheadHeightField?.SetValue(headSetting, foreheadHeight);
            foreheadRoundField?.SetValue(headSetting, foreheadRound);

            return headSetting;
        }

        /// <summary>
        /// 创建默认头部设置
        /// </summary>
        private object CreateDefaultHeadSetting()
        {
            // 使用中间值作为默认
            return CreateHeadSetting(Color.white, 1.0f, 0.3f, 0.675f);
        }

        /// <summary>
        /// 创建部位信息
        /// </summary>
        private object CreatePartInfo(PartInfo info)
        {
            object partInfo = Activator.CreateInstance(_customFacePartInfoType);

            AccessTools.Field(_customFacePartInfoType, "radius")?.SetValue(partInfo, info.Radius);
            AccessTools.Field(_customFacePartInfoType, "color")?.SetValue(partInfo, info.Color);
            AccessTools.Field(_customFacePartInfoType, "height")?.SetValue(partInfo, info.Height);
            AccessTools.Field(_customFacePartInfoType, "heightOffset")?.SetValue(partInfo, info.HeightOffset);
            AccessTools.Field(_customFacePartInfoType, "scale")?.SetValue(partInfo, info.Scale);
            AccessTools.Field(_customFacePartInfoType, "twist")?.SetValue(partInfo, info.Twist);
            AccessTools.Field(_customFacePartInfoType, "distanceAngle")?.SetValue(partInfo, info.DistanceAngle);
            AccessTools.Field(_customFacePartInfoType, "leftRightAngle")?.SetValue(partInfo, info.LeftRightAngle);

            return partInfo;
        }

        /// <summary>
        /// 设置部位
        /// </summary>
        private void SetPart(object customData, string idFieldName, string infoFieldName, int id, PartInfo? info)
        {
            var idField = AccessTools.Field(_customFaceSettingDataType, idFieldName);
            idField?.SetValue(customData, id);
            
            // 只有明确提供了 PartInfo 时才设置
            // 否则让游戏使用默认的部位位置配置
            if (info.HasValue)
            {
                var infoField = AccessTools.Field(_customFaceSettingDataType, infoFieldName);
                object partInfo = CreatePartInfo(info.Value);
                infoField?.SetValue(customData, partInfo);
            }
            // 不设置 infoField 时，它会保持结构体的默认值，这通常是合理的
        }

        public void Dispose()
        {
            // 清理资源
        }
    }

    /// <summary>
    /// 角色自定义配置
    /// </summary>
    public struct CharacterCustomization
    {
        // 头部设置
        public Color MainColor;
        public float HeadScaleOffset;
        public float ForeheadHeight;
        public float ForeheadRound;

        // 部位 ID 和信息
        public int HairID;
        public PartInfo? HairInfo;

        public int EyeID;
        public PartInfo? EyeInfo;

        public int EyebrowID;
        public PartInfo? EyebrowInfo;

        public int MouthID;
        public PartInfo? MouthInfo;

        public int TailID;
        public PartInfo? TailInfo;

        public int FootID;
        public PartInfo? FootInfo;

        public int WingID;
        public PartInfo? WingInfo;

        /// <summary>
        /// 默认配置
        /// </summary>
        public static CharacterCustomization Default => new CharacterCustomization
        {
            MainColor = Color.white,
            HeadScaleOffset = 1.0f,      // 中间值
            ForeheadHeight = 0.3f,       // 中间值
            ForeheadRound = 0.675f,      // 中间值
            HairID = 0,
            EyeID = 0,
            EyebrowID = 0,
            MouthID = 0,
            TailID = 0,
            FootID = 0,
            WingID = 0
        };

        /// <summary>
        /// 随机配置
        /// </summary>
        public static CharacterCustomization Random
        {
            get
            {
                return new CharacterCustomization
                {
                    MainColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f),
                    HeadScaleOffset = UnityEngine.Random.Range(0.6f, 1.4f),
                    ForeheadHeight = UnityEngine.Random.Range(0f, 0.6f),
                    ForeheadRound = UnityEngine.Random.Range(0.35f, 1.0f),
                    HairID = UnityEngine.Random.Range(0, 5),
                    HairInfo = PartInfo.RandomColor(),
                    EyeID = UnityEngine.Random.Range(0, 5),
                    EyeInfo = PartInfo.RandomColor(),
                    EyebrowID = UnityEngine.Random.Range(0, 3),
                    EyebrowInfo = PartInfo.RandomColor(),
                    MouthID = UnityEngine.Random.Range(0, 3),
                    MouthInfo = PartInfo.RandomColor(),
                    TailID = UnityEngine.Random.Range(0, 3),
                    TailInfo = PartInfo.RandomColor(),
                    FootID = UnityEngine.Random.Range(0, 2),
                    FootInfo = PartInfo.RandomColor(),
                    WingID = UnityEngine.Random.Range(0, 2),
                    WingInfo = PartInfo.RandomColor()
                };
            }
        }
    }

    /// <summary>
    /// 部位信息
    /// </summary>
    public struct PartInfo
    {
        public float Radius;
        public Color Color;
        public float Height;
        public float HeightOffset;
        public float Scale;
        public float Twist;
        public float DistanceAngle;      // 眼睛、眉毛间距
        public float LeftRightAngle;     // 嘴巴左右偏移

        public static PartInfo Default => new PartInfo
        {
            Radius = 1f,
            Color = Color.white,
            Height = 0f,
            HeightOffset = 0f,
            Scale = 1f,
            Twist = 0f,
            DistanceAngle = 0f,
            LeftRightAngle = 0f
        };

        public static PartInfo RandomColor()
        {
            var info = Default;
            info.Color = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);
            return info;
        }

        /// <summary>
        /// 创建随机部位信息
        /// </summary>
        public static PartInfo Random(float minScale = 0.3f, float maxScale = 4.0f)
        {
            return new PartInfo
            {
                Radius = 1f,
                Color = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f),
                Height = UnityEngine.Random.Range(-0.3f, 0.3f),
                HeightOffset = 0f,
                Scale = UnityEngine.Random.Range(minScale, maxScale),
                Twist = UnityEngine.Random.Range(-90f, 90f),
                DistanceAngle = UnityEngine.Random.Range(0f, 90f),
                LeftRightAngle = UnityEngine.Random.Range(-50f, 50f)
            };
        }
    }
}

