using System;
using UnityEngine;
using HarmonyLib;
using DuckyNet.Shared.Data;


namespace DuckyNet.Client.Core.Helpers
{
    /// <summary>
    /// 角色外观转换器 - 在游戏数据和网络数据之间转换
    /// </summary>
    public class CharacterAppearanceConverter
    {
        private Type? _customFaceSettingDataType;
        private Type? _customFaceHeadSettingType;
        private Type? _customFacePartInfoType;
        private bool _initialized = false;

        public CharacterAppearanceConverter()
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

                _initialized = _customFaceSettingDataType != null 
                    && _customFaceHeadSettingType != null 
                    && _customFacePartInfoType != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceConverter] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从游戏数据转换为网络数据
        /// </summary>
        public CharacterAppearanceData? ConvertToNetworkData(object gameCustomData)
        {
            if (!_initialized || gameCustomData == null)
                return null;

            try
            {
                var result = new CharacterAppearanceData();

                // 转换头部设置
                var headSettingField = AccessTools.Field(_customFaceSettingDataType, "headSetting");
                object? headSetting = headSettingField?.GetValue(gameCustomData);
                if (headSetting != null)
                {
                    result.HeadSetting = ConvertHeadSetting(headSetting);
                }

                // 转换部位数据
                var partsField = AccessTools.Field(_customFaceSettingDataType, "parts");
                object? partsArray = partsField?.GetValue(gameCustomData);
                if (partsArray is Array parts && parts.Length > 0)
                {
                    result.Parts = new PartData[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        object? part = parts.GetValue(i);
                        if (part != null)
                        {
                            result.Parts[i] = ConvertPart(part);
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceConverter] 转换到网络数据失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从网络数据转换为游戏数据
        /// </summary>
        public object? ConvertToGameData(CharacterAppearanceData networkData)
        {
            if (!_initialized || networkData == null || _customFaceSettingDataType == null)
                return null;

            try
            {
                object customData = Activator.CreateInstance(_customFaceSettingDataType);

                // 转换头部设置
                object headSetting = ConvertHeadSettingToGame(networkData.HeadSetting);
                var headSettingField = AccessTools.Field(_customFaceSettingDataType, "headSetting");
                headSettingField?.SetValue(customData, headSetting);

                // 转换部位数据
                if (networkData.Parts.Length > 0)
                {
                    var partsList = new System.Collections.Generic.List<object>();
                    foreach (var partData in networkData.Parts)
                    {
                        object? gamePart = ConvertPartToGame(partData);
                        if (gamePart != null)
                        {
                            partsList.Add(gamePart);
                        }
                    }

                    var partsField = AccessTools.Field(_customFaceSettingDataType, "parts");
                    var partsArrayType = partsField?.FieldType;
                    if (partsArrayType != null)
                    {
                        var partsArray = Array.CreateInstance(partsArrayType.GetElementType()!, partsList.Count);
                        for (int i = 0; i < partsList.Count; i++)
                        {
                            partsArray.SetValue(partsList[i], i);
                        }
                        partsField?.SetValue(customData, partsArray);
                    }
                }

                return customData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceConverter] 转换到游戏数据失败: {ex.Message}");
                return null;
            }
        }

        private HeadSettingData ConvertHeadSetting(object gameHeadSetting)
        {
            var scaleField = AccessTools.Field(_customFaceHeadSettingType, "scale");
            var offsetField = AccessTools.Field(_customFaceHeadSettingType, "offset");
            var rotationField = AccessTools.Field(_customFaceHeadSettingType, "rotation");

            Vector3 scale = (Vector3)(scaleField?.GetValue(gameHeadSetting) ?? Vector3.one);
            Vector3 offset = (Vector3)(offsetField?.GetValue(gameHeadSetting) ?? Vector3.zero);
            Vector3 rotation = (Vector3)(rotationField?.GetValue(gameHeadSetting) ?? Vector3.zero);

            var scaleCompressed = FloatCompression.CompressVector3(scale.x, scale.y, scale.z);
            var offsetCompressed = FloatCompression.CompressVector3(offset.x, offset.y, offset.z);
            var rotationCompressed = FloatCompression.CompressVector3(rotation.x, rotation.y, rotation.z);

            return new HeadSettingData
            {
                ScaleX = scaleCompressed.x,
                ScaleY = scaleCompressed.y,
                ScaleZ = scaleCompressed.z,
                OffsetX = offsetCompressed.x,
                OffsetY = offsetCompressed.y,
                OffsetZ = offsetCompressed.z,
                RotationX = rotationCompressed.x,
                RotationY = rotationCompressed.y,
                RotationZ = rotationCompressed.z
            };
        }

        private object ConvertHeadSettingToGame(HeadSettingData data)
        {
            object headSetting = Activator.CreateInstance(_customFaceHeadSettingType!);

            var scale = FloatCompression.DecompressVector3(data.ScaleX, data.ScaleY, data.ScaleZ);
            var offset = FloatCompression.DecompressVector3(data.OffsetX, data.OffsetY, data.OffsetZ);
            var rotation = FloatCompression.DecompressVector3(data.RotationX, data.RotationY, data.RotationZ);

            var scaleField = AccessTools.Field(_customFaceHeadSettingType, "scale");
            var offsetField = AccessTools.Field(_customFaceHeadSettingType, "offset");
            var rotationField = AccessTools.Field(_customFaceHeadSettingType, "rotation");

            scaleField?.SetValue(headSetting, new Vector3(scale.x, scale.y, scale.z));
            offsetField?.SetValue(headSetting, new Vector3(offset.x, offset.y, offset.z));
            rotationField?.SetValue(headSetting, new Vector3(rotation.x, rotation.y, rotation.z));

            return headSetting;
        }

        private PartData ConvertPart(object gamePart)
        {
            var typeField = AccessTools.Field(_customFacePartInfoType, "type");
            var idField = AccessTools.Field(_customFacePartInfoType, "id");
            var scaleField = AccessTools.Field(_customFacePartInfoType, "scale");
            var offsetField = AccessTools.Field(_customFacePartInfoType, "offset");
            var rotationField = AccessTools.Field(_customFacePartInfoType, "rotation");

            int type = (int)(typeField?.GetValue(gamePart) ?? 0);
            int id = (int)(idField?.GetValue(gamePart) ?? 0);
            Vector3 scale = (Vector3)(scaleField?.GetValue(gamePart) ?? Vector3.one);
            Vector3 offset = (Vector3)(offsetField?.GetValue(gamePart) ?? Vector3.zero);
            Vector3 rotation = (Vector3)(rotationField?.GetValue(gamePart) ?? Vector3.zero);

            var scaleCompressed = FloatCompression.CompressVector3(scale.x, scale.y, scale.z);
            var offsetCompressed = FloatCompression.CompressVector3(offset.x, offset.y, offset.z);
            var rotationCompressed = FloatCompression.CompressVector3(rotation.x, rotation.y, rotation.z);

            return new PartData
            {
                PartType = (byte)type,
                PartId = (ushort)id,
                ScaleX = scaleCompressed.x,
                ScaleY = scaleCompressed.y,
                ScaleZ = scaleCompressed.z,
                OffsetX = offsetCompressed.x,
                OffsetY = offsetCompressed.y,
                OffsetZ = offsetCompressed.z,
                RotationX = rotationCompressed.x,
                RotationY = rotationCompressed.y,
                RotationZ = rotationCompressed.z
            };
        }

        private object? ConvertPartToGame(PartData data)
        {
            try
            {
                object part = Activator.CreateInstance(_customFacePartInfoType!);

                var scale = FloatCompression.DecompressVector3(data.ScaleX, data.ScaleY, data.ScaleZ);
                var offset = FloatCompression.DecompressVector3(data.OffsetX, data.OffsetY, data.OffsetZ);
                var rotation = FloatCompression.DecompressVector3(data.RotationX, data.RotationY, data.RotationZ);

                var typeField = AccessTools.Field(_customFacePartInfoType, "type");
                var idField = AccessTools.Field(_customFacePartInfoType, "id");
                var scaleField = AccessTools.Field(_customFacePartInfoType, "scale");
                var offsetField = AccessTools.Field(_customFacePartInfoType, "offset");
                var rotationField = AccessTools.Field(_customFacePartInfoType, "rotation");

                typeField?.SetValue(part, (int)data.PartType);
                idField?.SetValue(part, (int)data.PartId);
                scaleField?.SetValue(part, new Vector3(scale.x, scale.y, scale.z));
                offsetField?.SetValue(part, new Vector3(offset.x, offset.y, offset.z));
                rotationField?.SetValue(part, new Vector3(rotation.x, rotation.y, rotation.z));

                return part;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAppearanceConverter] 转换部位到游戏数据失败: {ex.Message}");
                return null;
            }
        }
    }
}

