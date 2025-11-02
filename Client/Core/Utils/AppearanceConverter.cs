using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 角色外观数据转换工具
    /// 用于将游戏内的 CustomFaceSettingData 转换为网络传输格式 CharacterAppearanceData
    /// </summary>
    public static class AppearanceConverter
    {
        /// <summary>
        /// 从游戏的 CustomFaceSettingData 转换为网络传输格式
        /// </summary>
        /// <param name="customFaceData">游戏内的外观数据对象</param>
        /// <returns>网络传输格式的外观数据，失败返回 null</returns>
        public static CharacterAppearanceData? ConvertToNetworkFormat(object? customFaceData)
        {
            if (customFaceData == null)
            {
                Debug.LogWarning("[AppearanceConverter] customFaceData 为空");
                return null;
            }

            try
            {
                var result = new CharacterAppearanceData();
                var type = customFaceData.GetType();

                // ============ 解析头部设置 ============
                var headSettingField = AccessTools.Field(type, "headSetting");
                if (headSettingField != null)
                {
                    var headSetting = headSettingField.GetValue(customFaceData);
                    if (headSetting != null)
                    {
                        ParseHeadSetting(headSetting, result);
                    }
                }

                // ============ 解析各个部位 ============
                ParsePart(customFaceData, "hair", result, PartType.Hair);
                ParsePart(customFaceData, "eye", result, PartType.Eye);
                ParsePart(customFaceData, "eyebrow", result, PartType.Eyebrow);
                ParsePart(customFaceData, "mouth", result, PartType.Mouth);
                ParsePart(customFaceData, "tail", result, PartType.Tail);
                ParsePart(customFaceData, "foot", result, PartType.Foot);
                ParsePart(customFaceData, "wing", result, PartType.Wing);

                Debug.Log($"[AppearanceConverter] 成功转换外观数据");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppearanceConverter] 转换失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 从 CustomFaceManager 加载主角外观数据并转换
        /// </summary>
        public static CharacterAppearanceData? LoadMainCharacterAppearance()
        {
            try
            {
                var customFaceManagerType = AccessTools.TypeByName("CustomFaceManager");
                var levelManagerType = AccessTools.TypeByName("LevelManager");

                if (customFaceManagerType == null || levelManagerType == null)
                {
                    Debug.LogWarning("[AppearanceConverter] 无法找到 CustomFaceManager 或 LevelManager 类型");
                    return null;
                }

                var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                var levelManager = instanceProp?.GetValue(null);

                if (levelManager == null)
                {
                    Debug.LogWarning("[AppearanceConverter] LevelManager.Instance 为空");
                    return null;
                }

                var customFaceManagerProp = AccessTools.Property(levelManagerType, "CustomFaceManager");
                var customFaceManager = customFaceManagerProp?.GetValue(levelManager);

                if (customFaceManager == null)
                {
                    Debug.LogWarning("[AppearanceConverter] CustomFaceManager 为空");
                    return null;
                }

                var loadMethod = AccessTools.Method(customFaceManagerType, "LoadMainCharacterSetting");
                object? faceData = loadMethod?.Invoke(customFaceManager, null);

                return ConvertToNetworkFormat(faceData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppearanceConverter] LoadMainCharacterAppearance 失败: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 从角色实例获取外观数据并转换
        /// </summary>
        public static CharacterAppearanceData? GetCharacterAppearance(CharacterMainControl character)
        {
            try
            {
                // 获取 CharacterModel (使用 Field 而不是 Property)
                var characterModelField = AccessTools.Field(character.GetType(), "characterModel");
                var characterModel = characterModelField?.GetValue(character);

                if (characterModel == null)
                {
                    Debug.LogWarning("[AppearanceConverter] CharacterModel 为空");
                    return null;
                }

                // 获取 CustomFace
                var customFaceProp = AccessTools.Property(characterModel.GetType(), "CustomFace");
                var customFaceInstance = customFaceProp?.GetValue(characterModel);

                if (customFaceInstance == null)
                {
                    Debug.LogWarning("[AppearanceConverter] CustomFace 实例为空");
                    return null;
                }

                // 转换为保存数据
                var convertMethod = AccessTools.Method(customFaceInstance.GetType(), "ConvertToSaveData");
                object? faceData = convertMethod?.Invoke(customFaceInstance, null);

                return ConvertToNetworkFormat(faceData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppearanceConverter] GetCharacterAppearance 失败: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 应用外观数据到角色
        /// 使用 CharacterModel.SetFaceFromData() 方法
        /// </summary>
        /// <param name="character">目标角色（CharacterMainControl 或 GameObject）</param>
        /// <param name="appearanceData">网络传输格式的外观数据</param>
        /// <returns>成功返回 true</returns>
        public static bool ApplyAppearanceToCharacter(object character, CharacterAppearanceData appearanceData)
        {
            try
            {
                Debug.Log($"[AppearanceConverter] 🎨 开始应用外观数据到角色");
                if (character == null)
                {
                    Debug.LogWarning("[AppearanceConverter] ❌ 角色为空");
                    return false;
                }
                if (appearanceData == null)
                {
                    Debug.LogWarning("[AppearanceConverter] ❌ 外观数据为空");
                    return false;
                }
                // 如果传入的是 GameObject，获取 CharacterMainControl 组件
                object characterComponent = character;
                if (character is GameObject gameObject)
                {
                    Debug.Log($"[AppearanceConverter] 检测到 GameObject，尝试获取 CharacterMainControl 组件");
                    var characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
                    if (characterMainControlType != null)
                    {
                        var getComponentMethod = typeof(GameObject).GetMethod("GetComponent", new[] { typeof(System.Type) });
                        if (getComponentMethod != null)
                        {
                            characterComponent = getComponentMethod.Invoke(gameObject, new object[] { characterMainControlType })!;
                        }

                        if (characterComponent == null)
                        {
                            Debug.LogError("[AppearanceConverter] ❌ GameObject 上未找到 CharacterMainControl 组件");
                            return false;
                        }
                        Debug.Log($"[AppearanceConverter] ✅ 成功获取 CharacterMainControl 组件");
                    }
                    else
                    {
                        Debug.LogError("[AppearanceConverter] ❌ 无法找到 CharacterMainControl 类型");
                        return false;
                    }
                }
                Debug.Log($"[AppearanceConverter] 📦 外观数据详情 - HeadScale: {appearanceData.HeadSetting.ScaleX}, Parts: {appearanceData.Parts.Length}");
                // 转换为游戏内格式
                Debug.Log($"[AppearanceConverter] 🔄 正在转换网络格式到游戏格式...");
                var customFaceData = ConvertFromNetworkFormat(appearanceData);
                if (customFaceData == null)
                {
                    Debug.LogError("[AppearanceConverter] ❌ 转换外观数据失败");
                    return false;
                }

                Debug.Log($"[AppearanceConverter] ✅ 外观数据转换成功");

                // 使用 CharacterCreationUtils 应用外观
                Debug.Log($"[AppearanceConverter] 🎯 正在应用外观到角色...");
                bool success = CharacterCreationUtils.ApplyCustomFace(characterComponent, customFaceData);

                if (success)
                {
                    Debug.Log($"[AppearanceConverter] ✅ 外观应用成功！");
                }
                else
                {
                    Debug.LogError($"[AppearanceConverter] ❌ 外观应用失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppearanceConverter] ❌ 应用外观数据异常: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 从网络格式转换回游戏内格式（CustomFaceSettingData）
        /// </summary>
        /// <param name="appearanceData">网络传输格式</param>
        /// <returns>游戏内格式的 CustomFaceSettingData</returns>
        public static object? ConvertFromNetworkFormat(CharacterAppearanceData appearanceData)
        {
            try
            {
                var customFaceDataType = AccessTools.TypeByName("CustomFaceSettingData");
                if (customFaceDataType == null)
                {
                    Debug.LogError("[AppearanceConverter] 无法找到 CustomFaceSettingData 类型");
                    return null;
                }

                // 创建 CustomFaceSettingData 实例
                var customFaceData = Activator.CreateInstance(customFaceDataType);
                if (customFaceData == null)
                {
                    Debug.LogError("[AppearanceConverter] 无法创建 CustomFaceSettingData 实例");
                    return null;
                }

                // ============ 应用头部设置 ============
                ApplyHeadSetting(customFaceData, appearanceData.HeadSetting);

                // ============ 应用各个部位 ============
                foreach (var part in appearanceData.Parts)
                {
                    ApplyPart(customFaceData, part);
                }

                Debug.Log($"[AppearanceConverter] 成功转换为游戏内格式 (CustomFaceSettingData)");
                return customFaceData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppearanceConverter] 转换失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // ============ 私有辅助方法 ============

        private enum PartType
        {
            Hair, Eye, Eyebrow, Mouth, Tail, Foot, Wing
        }

        /// <summary>
        /// 解析头部设置
        /// </summary>
        private static void ParseHeadSetting(object headSetting, CharacterAppearanceData result)
        {
            var headSettingData = new HeadSettingData();
            var type = headSetting.GetType();

            // 头部缩放偏移量 -> Scale (正确的字段名: headScaleOffset)
            var headScaleOffsetField = AccessTools.Field(type, "headScaleOffset");
            if (headScaleOffsetField != null)
            {
                float headScaleOffset = Convert.ToSingle(headScaleOffsetField.GetValue(headSetting));
                var (x, y, z) = FloatCompression.CompressVector3(headScaleOffset, headScaleOffset, headScaleOffset);
                headSettingData.ScaleX = x;
                headSettingData.ScaleY = y;
                headSettingData.ScaleZ = z;
            }

            // 前额高度和圆度 -> Offset (正确的字段名: foreheadHeight, foreheadRound)
            var foreheadHeightField = AccessTools.Field(type, "foreheadHeight");
            var foreheadRoundField = AccessTools.Field(type, "foreheadRound");
            if (foreheadHeightField != null && foreheadRoundField != null)
            {
                float foreheadHeight = Convert.ToSingle(foreheadHeightField.GetValue(headSetting));
                float foreheadRound = Convert.ToSingle(foreheadRoundField.GetValue(headSetting));
                var (x, y, z) = FloatCompression.CompressVector3(0, foreheadHeight, foreheadRound);
                headSettingData.OffsetX = x;
                headSettingData.OffsetY = y;
                headSettingData.OffsetZ = z;
            }

            // 皮肤颜色 -> 存储在 Rotation 字段中 (复用字段)
            var skinColorField = AccessTools.Field(type, "skinColor");
            if (skinColorField != null)
            {
                var skinColor = skinColorField.GetValue(headSetting);
                if (skinColor is Color color)
                {
                    // 修正: 直接使用 FloatCompression.Compress 将 0-1 颜色值映射到 0-100
                    headSettingData.RotationX = FloatCompression.Compress(color.r);
                    headSettingData.RotationY = FloatCompression.Compress(color.g);
                    headSettingData.RotationZ = FloatCompression.Compress(color.b);
                }
            }

            // 主颜色（身体颜色）-> MainColor 字段
            var mainColorField = AccessTools.Field(type, "mainColor");
            if (mainColorField != null)
            {
                var mainColor = mainColorField.GetValue(headSetting);
                if (mainColor is Color color)
                {
                    headSettingData.MainColorR = FloatCompression.Compress(color.r);
                    headSettingData.MainColorG = FloatCompression.Compress(color.g);
                    headSettingData.MainColorB = FloatCompression.Compress(color.b);
                }
            }

            result.HeadSetting = headSettingData;
        }

        /// <summary>
        /// 解析部位数据
        /// </summary>
        private static void ParsePart(object customFaceData, string partName, CharacterAppearanceData result, PartType partType)
        {
            var type = customFaceData.GetType();

            // 获取 ID
            var idField = AccessTools.Field(type, $"{partName}ID");
            int partId = 0;
            if (idField != null)
            {
                partId = Convert.ToInt32(idField.GetValue(customFaceData));
            }

            // 获取 Info
            var infoField = AccessTools.Field(type, $"{partName}Info");
            if (infoField == null) return;

            var infoValue = infoField.GetValue(customFaceData);
            if (infoValue == null) return;

            var partData = new PartData
            {
                PartType = (byte)partType,
                PartId = (ushort)partId
            };

            var infoType = infoValue.GetType();

            // Scale 字段: 存储 scale (size)
            var scaleField = AccessTools.Field(infoType, "scale");
            if (scaleField != null)
            {
                float scale = Convert.ToSingle(scaleField.GetValue(infoValue));
                var (x, y, z) = FloatCompression.CompressVector3(scale, scale, scale);
                partData.ScaleX = x;
                partData.ScaleY = y;
                partData.ScaleZ = z;
            }

            // Offset 字段: 存储 height, heightOffset, radius
            var heightField = AccessTools.Field(infoType, "height");
            var heightOffsetField = AccessTools.Field(infoType, "heightOffset");
            var radiusField = AccessTools.Field(infoType, "radius");

            float height = heightField != null ? Convert.ToSingle(heightField.GetValue(infoValue)) : 0f;
            float heightOffset = heightOffsetField != null ? Convert.ToSingle(heightOffsetField.GetValue(infoValue)) : 0f;
            float radius = radiusField != null ? Convert.ToSingle(radiusField.GetValue(infoValue)) : 0f;

            var (ox, oy, oz) = FloatCompression.CompressVector3(height, heightOffset, radius);
            partData.OffsetX = ox;
            partData.OffsetY = oy;
            partData.OffsetZ = oz;

            // Rotation 字段: 存储 distanceAngle, leftRightAngle, twist
            var distanceAngleField = AccessTools.Field(infoType, "distanceAngle");
            var leftRightAngleField = AccessTools.Field(infoType, "leftRightAngle");
            var twistField = AccessTools.Field(infoType, "twist");

            float distanceAngle = distanceAngleField != null ? Convert.ToSingle(distanceAngleField.GetValue(infoValue)) : 0f;
            float leftRightAngle = leftRightAngleField != null ? Convert.ToSingle(leftRightAngleField.GetValue(infoValue)) : 0f;
            float twist = twistField != null ? Convert.ToSingle(twistField.GetValue(infoValue)) : 0f;

            var (rx, ry, rz) = FloatCompression.CompressVector3(distanceAngle, leftRightAngle, twist);
            partData.RotationX = rx;
            partData.RotationY = ry;
            partData.RotationZ = rz;

            // 颜色字段: 存储 RGB 颜色值
            var colorField = AccessTools.Field(infoType, "color");
            if (colorField != null)
            {
                var color = colorField.GetValue(infoValue);
                if (color is Color c)
                {
                    partData.ColorR = FloatCompression.Compress(c.r);
                    partData.ColorG = FloatCompression.Compress(c.g);
                    partData.ColorB = FloatCompression.Compress(c.b);
                }
            }

            // 添加到结果中
            var parts = new System.Collections.Generic.List<PartData>(result.Parts);
            parts.Add(partData);
            result.Parts = parts.ToArray();
        }

        /// <summary>
        /// 应用头部设置到游戏内格式
        /// </summary>
        private static void ApplyHeadSetting(object customFaceData, HeadSettingData headSettingData)
        {
            var type = customFaceData.GetType();

            // 创建或获取 headSetting 对象
            var headSettingField = AccessTools.Field(type, "headSetting");
            if (headSettingField == null) return;

            var headSettingType = headSettingField.FieldType;
            var headSetting = Activator.CreateInstance(headSettingType);
            if (headSetting == null) return;

            var hsType = headSetting.GetType();

            // 头部缩放偏移量 (从 Scale 还原，正确的字段名: headScaleOffset)
            var headScaleOffsetField = AccessTools.Field(hsType, "headScaleOffset");
            if (headScaleOffsetField != null)
            {
                var (x, y, z) = FloatCompression.DecompressVector3(
                    headSettingData.ScaleX,
                    headSettingData.ScaleY,
                    headSettingData.ScaleZ
                );
                headScaleOffsetField.SetValue(headSetting, x); // 使用 X 作为偏移量
            }

            // 前额高度和圆度 (从 Offset 还原，正确的字段名: foreheadHeight, foreheadRound)
            var foreheadHeightField = AccessTools.Field(hsType, "foreheadHeight");
            var foreheadRoundField = AccessTools.Field(hsType, "foreheadRound");
            if (foreheadHeightField != null && foreheadRoundField != null)
            {
                var (x, y, z) = FloatCompression.DecompressVector3(
                    headSettingData.OffsetX,
                    headSettingData.OffsetY,
                    headSettingData.OffsetZ
                );
                foreheadHeightField.SetValue(headSetting, y); // Y 是前额高度
                foreheadRoundField.SetValue(headSetting, z);   // Z 是前额圆度
            }

            // 皮肤颜色 (从 Rotation 还原)
            var skinColorField = AccessTools.Field(hsType, "skinColor");
            if (skinColorField != null)
            {
                // 修正: 使用 FloatCompression.Decompress 将 0-100 值还原为 0-1 颜色值
                float r = FloatCompression.Decompress(headSettingData.RotationX);
                float g = FloatCompression.Decompress(headSettingData.RotationY);
                float b = FloatCompression.Decompress(headSettingData.RotationZ);

                var color = new Color(
                    Mathf.Clamp01(r),
                    Mathf.Clamp01(g),
                    Mathf.Clamp01(b),
                    1f
                );
                skinColorField.SetValue(headSetting, color);
            }

            // 主颜色（身体颜色）(从 MainColor 还原)
            var mainColorField = AccessTools.Field(hsType, "mainColor");
            if (mainColorField != null)
            {
                float r = FloatCompression.Decompress(headSettingData.MainColorR);
                float g = FloatCompression.Decompress(headSettingData.MainColorG);
                float b = FloatCompression.Decompress(headSettingData.MainColorB);

                var color = new Color(
                    Mathf.Clamp01(r),
                    Mathf.Clamp01(g),
                    Mathf.Clamp01(b),
                    1f
                );
                mainColorField.SetValue(headSetting, color);
            }

            // 设置到 customFaceData
            headSettingField.SetValue(customFaceData, headSetting);
        }

        /// <summary>
        /// 应用部位数据到游戏内格式
        /// </summary>
        private static void ApplyPart(object customFaceData, PartData partData)
        {
            var type = customFaceData.GetType();

            // 根据 PartType 确定部位名称
            string partName = ((PartType)partData.PartType) switch
            {
                PartType.Hair => "hair",
                PartType.Eye => "eye",
                PartType.Eyebrow => "eyebrow",
                PartType.Mouth => "mouth",
                PartType.Tail => "tail",
                PartType.Foot => "foot",
                PartType.Wing => "wing",
                _ => ""
            };

            if (string.IsNullOrEmpty(partName)) return;

            // 设置 ID
            var idField = AccessTools.Field(type, $"{partName}ID");
            if (idField != null)
            {
                idField.SetValue(customFaceData, (int)partData.PartId);
            }

            // 设置 Info
            var infoField = AccessTools.Field(type, $"{partName}Info");
            if (infoField == null) return;

            // 创建 Info 对象
            var infoType = infoField.FieldType;
            var infoInstance = Activator.CreateInstance(infoType);
            if (infoInstance == null) return;

            // 还原 scale
            var scaleField = AccessTools.Field(infoType, "scale");
            if (scaleField != null)
            {
                var (x, y, z) = FloatCompression.DecompressVector3(partData.ScaleX, partData.ScaleY, partData.ScaleZ);
                scaleField.SetValue(infoInstance, x);
            }

            // 还原 height, heightOffset, radius
            var (height, heightOffset, radius) = FloatCompression.DecompressVector3(
                partData.OffsetX, partData.OffsetY, partData.OffsetZ);

            var heightField = AccessTools.Field(infoType, "height");
            if (heightField != null) heightField.SetValue(infoInstance, height);

            var heightOffsetField = AccessTools.Field(infoType, "heightOffset");
            if (heightOffsetField != null) heightOffsetField.SetValue(infoInstance, heightOffset);

            var radiusField = AccessTools.Field(infoType, "radius");
            if (radiusField != null) radiusField.SetValue(infoInstance, radius);

            // 还原 distanceAngle, leftRightAngle, twist
            var (distanceAngle, leftRightAngle, twist) = FloatCompression.DecompressVector3(
                partData.RotationX, partData.RotationY, partData.RotationZ);

            var distanceAngleField = AccessTools.Field(infoType, "distanceAngle");
            if (distanceAngleField != null) distanceAngleField.SetValue(infoInstance, distanceAngle);

            var leftRightAngleField = AccessTools.Field(infoType, "leftRightAngle");
            if (leftRightAngleField != null) leftRightAngleField.SetValue(infoInstance, leftRightAngle);

            var twistField = AccessTools.Field(infoType, "twist");
            if (twistField != null) twistField.SetValue(infoInstance, twist);

            // 还原颜色
            var colorField = AccessTools.Field(infoType, "color");
            if (colorField != null)
            {
                float r = FloatCompression.Decompress(partData.ColorR);
                float g = FloatCompression.Decompress(partData.ColorG);
                float b = FloatCompression.Decompress(partData.ColorB);

                var color = new Color(
                    Mathf.Clamp01(r),
                    Mathf.Clamp01(g),
                    Mathf.Clamp01(b),
                    1f
                );
                colorField.SetValue(infoInstance, color);
            }

            // 设置到 customFaceData
            infoField.SetValue(customFaceData, infoInstance);
        }
    }
}
