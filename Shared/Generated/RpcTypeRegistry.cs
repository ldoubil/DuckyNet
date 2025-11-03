// 自动生成的类型注册代码
// 由 RpcCodeGen 工具自动生成，请勿手动修改

using System;
using System.Collections.Generic;

namespace DuckyNet.Shared.RPC.Generated
{
    /// <summary>
    /// 自动生成的 RPC 序列化类型注册表
    /// </summary>
    public static class RpcTypeRegistry
    {
        /// <summary>
        /// 获取所有需要序列化的类型
        /// </summary>
        public static List<Type> GetSerializableTypes()
        {
            return new List<Type>
            {
                // 基础类型
                typeof(string),
                typeof(int),
                typeof(long),
                typeof(float),
                typeof(double),
                typeof(bool),
                typeof(byte[]),
                typeof(object[]),
                typeof(DateTime),

                // RPC 消息类型
                typeof(DuckyNet.Shared.RPC.RpcMessage),
                typeof(DuckyNet.Shared.RPC.RpcResponse),

                // 应用数据类型 (自动发现)
                typeof(DuckyNet.Shared.Data.AnimatorSyncData),
                typeof(DuckyNet.Shared.Data.CharacterAppearanceData),
                typeof(DuckyNet.Shared.Data.ItemDropData),
                typeof(DuckyNet.Shared.Data.ItemPickupRequest),
                typeof(DuckyNet.Shared.Data.ScenelData),
                typeof(DuckyNet.Shared.Data.UnitySyncData),
                typeof(DuckyNet.Shared.Services.CreateRoomRequest),
                typeof(DuckyNet.Shared.Services.JoinRoomRequest),
                typeof(DuckyNet.Shared.Services.LoginResult),
                typeof(DuckyNet.Shared.Services.MessageType),
                typeof(DuckyNet.Shared.Services.PlayerInfo),
                typeof(DuckyNet.Shared.Services.PlayerInfo[]),
                typeof(DuckyNet.Shared.Services.RoomInfo),
                typeof(DuckyNet.Shared.Services.RoomInfo[]),
                typeof(DuckyNet.Shared.Services.RoomOperationResult),
                typeof(System.Byte[]),
            };
        }
    }
}
