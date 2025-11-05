using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 武器射击数据（客户端→服务器→其他客户端）
    /// 仅用于播放视觉和音效特效，不影响游戏逻辑
    /// </summary>
    [Serializable]
    public class WeaponFireData
    {
        /// <summary>开枪玩家的ID</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>枪口位置X</summary>
        public float MuzzlePositionX { get; set; }
        
        /// <summary>枪口位置Y</summary>
        public float MuzzlePositionY { get; set; }
        
        /// <summary>枪口位置Z</summary>
        public float MuzzlePositionZ { get; set; }

        /// <summary>枪口方向X</summary>
        public float MuzzleDirectionX { get; set; }
        
        /// <summary>枪口方向Y</summary>
        public float MuzzleDirectionY { get; set; }
        
        /// <summary>枪口方向Z</summary>
        public float MuzzleDirectionZ { get; set; }

        /// <summary>是否使用消音器</summary>
        public bool IsSilenced { get; set; }

        /// <summary>武器类型ID（用于获取特效配置，可选）</summary>
        public int WeaponTypeId { get; set; }
    }

    /// <summary>
    /// 批量武器射击数据（霰弹枪/连发武器优化）
    /// 🚀 性能优化：封装多发子弹，避免 RPC 数组序列化问题
    /// </summary>
    [Serializable]
    public class WeaponFireBatchData
    {
        /// <summary>开枪玩家的ID</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>是否使用消音器</summary>
        public bool IsSilenced { get; set; }

        /// <summary>武器类型ID</summary>
        public int WeaponTypeId { get; set; }

        /// <summary>子弹数量</summary>
        public int BulletCount { get; set; }

        /// <summary>所有子弹的枪口位置X数组</summary>
        public float[] MuzzlePositionsX { get; set; } = Array.Empty<float>();

        /// <summary>所有子弹的枪口位置Y数组</summary>
        public float[] MuzzlePositionsY { get; set; } = Array.Empty<float>();

        /// <summary>所有子弹的枪口位置Z数组</summary>
        public float[] MuzzlePositionsZ { get; set; } = Array.Empty<float>();

        /// <summary>所有子弹的方向X数组</summary>
        public float[] DirectionsX { get; set; } = Array.Empty<float>();

        /// <summary>所有子弹的方向Y数组</summary>
        public float[] DirectionsY { get; set; } = Array.Empty<float>();

        /// <summary>所有子弹的方向Z数组</summary>
        public float[] DirectionsZ { get; set; } = Array.Empty<float>();

        /// <summary>
        /// 转换为单个 WeaponFireData 数组
        /// </summary>
        public WeaponFireData[] ToFireDataArray()
        {
            var result = new WeaponFireData[BulletCount];
            for (int i = 0; i < BulletCount; i++)
            {
                result[i] = new WeaponFireData
                {
                    PlayerId = PlayerId,
                    MuzzlePositionX = MuzzlePositionsX[i],
                    MuzzlePositionY = MuzzlePositionsY[i],
                    MuzzlePositionZ = MuzzlePositionsZ[i],
                    MuzzleDirectionX = DirectionsX[i],
                    MuzzleDirectionY = DirectionsY[i],
                    MuzzleDirectionZ = DirectionsZ[i],
                    IsSilenced = IsSilenced,
                    WeaponTypeId = WeaponTypeId
                };
            }
            return result;
        }

        /// <summary>
        /// 从 WeaponFireData 数组创建批量数据
        /// </summary>
        public static WeaponFireBatchData FromArray(WeaponFireData[] fireDataArray)
        {
            int count = fireDataArray.Length;
            var batch = new WeaponFireBatchData
            {
                BulletCount = count,
                PlayerId = count > 0 ? fireDataArray[0].PlayerId : "",
                IsSilenced = count > 0 && fireDataArray[0].IsSilenced,
                WeaponTypeId = count > 0 ? fireDataArray[0].WeaponTypeId : 0,
                MuzzlePositionsX = new float[count],
                MuzzlePositionsY = new float[count],
                MuzzlePositionsZ = new float[count],
                DirectionsX = new float[count],
                DirectionsY = new float[count],
                DirectionsZ = new float[count]
            };

            for (int i = 0; i < count; i++)
            {
                batch.MuzzlePositionsX[i] = fireDataArray[i].MuzzlePositionX;
                batch.MuzzlePositionsY[i] = fireDataArray[i].MuzzlePositionY;
                batch.MuzzlePositionsZ[i] = fireDataArray[i].MuzzlePositionZ;
                batch.DirectionsX[i] = fireDataArray[i].MuzzleDirectionX;
                batch.DirectionsY[i] = fireDataArray[i].MuzzleDirectionY;
                batch.DirectionsZ[i] = fireDataArray[i].MuzzleDirectionZ;
            }

            return batch;
        }
    }
}

