using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// Unity 位置同步数据 (压缩版)
    /// 使用量化和压缩技术减少网络传输量
    /// 原始大小: ~64 bytes -> 压缩后: ~24 bytes (减少 62.5%)
    /// </summary>
    [Serializable]
    public class UnitySyncData
    {
        // 量化精度常量
        private const float POSITION_PRECISION = 0.01f;  // 1cm 精度
        private const float VELOCITY_PRECISION = 0.1f;   // 0.1 m/s 精度
        private const float ROTATION_PRECISION = 0.0001f; // 四元数精度
        private const int POSITION_MULTIPLIER = 100;     // 1/0.01
        private const int VELOCITY_MULTIPLIER = 10;      // 1/0.1
        private const int ROTATION_MULTIPLIER = 10000;   // 1/0.0001

        /// <summary>
        /// 玩家ID (SteamId) - 由服务器填充,客户端不需要发送
        /// </summary>
        public string SteamId { get; set; } = string.Empty;

        /// <summary>
        /// 位置 X (量化为 short: -327.68m ~ 327.67m, 精度 1cm)
        /// </summary>
        public short PosX { get; set; }

        /// <summary>
        /// 位置 Y (量化为 short: -327.68m ~ 327.67m, 精度 1cm)
        /// </summary>
        public short PosY { get; set; }

        /// <summary>
        /// 位置 Z (量化为 short: -327.68m ~ 327.67m, 精度 1cm)
        /// </summary>
        public short PosZ { get; set; }

        /// <summary>
        /// 压缩的旋转数据 (使用 Smallest Three 算法)
        /// 3 个 short 存储最小的 3 个四元数分量 + 2 bit 存储省略的分量索引
        /// </summary>
        public short RotA { get; set; }
        public short RotB { get; set; }
        public short RotC { get; set; }
        
        /// <summary>
        /// 旋转省略索引 (0-3 表示省略 x,y,z,w 中的哪一个)
        /// </summary>
        public byte RotOmitIndex { get; set; }

        /// <summary>
        /// 速度 X (量化为 short: -3276.8 ~ 3276.7 m/s, 精度 0.1)
        /// </summary>
        public short VelX { get; set; }

        /// <summary>
        /// 速度 Y (量化为 short: -3276.8 ~ 3276.7 m/s, 精度 0.1)
        /// </summary>
        public short VelY { get; set; }

        /// <summary>
        /// 速度 Z (量化为 short: -3276.8 ~ 3276.7 m/s, 精度 0.1)
        /// </summary>
        public short VelZ { get; set; }

        /// <summary>
        /// 序列号 (用于排序和去重，不传输绝对时间)
        /// 客户端递增发送，服务器转发时保留
        /// 范围: 0 ~ 4,294,967,295 (约49天后循环)
        /// </summary>
        public uint SequenceNumber { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public UnitySyncData()
        {
            // 使用毫秒级时间戳作为初始序列号（确保唯一性）
            SequenceNumber = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFFFFFF);
        }

        // ============ 辅助方法:编码 ============

        /// <summary>
        /// 从浮点位置编码
        /// </summary>
        public void SetPosition(float x, float y, float z)
        {
            PosX = (short)Math.Clamp((int)(x * POSITION_MULTIPLIER), short.MinValue, short.MaxValue);
            PosY = (short)Math.Clamp((int)(y * POSITION_MULTIPLIER), short.MinValue, short.MaxValue);
            PosZ = (short)Math.Clamp((int)(z * POSITION_MULTIPLIER), short.MinValue, short.MaxValue);
        }

        /// <summary>
        /// 从四元数编码 (使用 Smallest Three 压缩)
        /// </summary>
        public void SetRotation(float x, float y, float z, float w)
        {
            // 找出绝对值最大的分量并省略它
            float[] components = { x, y, z, w };
            int omitIndex = 0;
            float maxAbs = Math.Abs(components[0]);
            
            for (int i = 1; i < 4; i++)
            {
                float abs = Math.Abs(components[i]);
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                    omitIndex = i;
                }
            }

            // 确保省略的分量符号为正 (如果为负,翻转所有分量)
            float sign = components[omitIndex] < 0 ? -1f : 1f;
            
            // 提取并量化其他 3 个分量
            int writeIndex = 0;
            short[] compressed = new short[3];
            
            for (int i = 0; i < 4; i++)
            {
                if (i != omitIndex)
                {
                    compressed[writeIndex++] = (short)Math.Clamp(
                        (int)(components[i] * sign * ROTATION_MULTIPLIER), 
                        short.MinValue, 
                        short.MaxValue
                    );
                }
            }

            RotA = compressed[0];
            RotB = compressed[1];
            RotC = compressed[2];
            RotOmitIndex = (byte)omitIndex;
        }

        /// <summary>
        /// 从速度向量编码
        /// </summary>
        public void SetVelocity(float x, float y, float z)
        {
            VelX = (short)Math.Clamp((int)(x * VELOCITY_MULTIPLIER), short.MinValue, short.MaxValue);
            VelY = (short)Math.Clamp((int)(y * VELOCITY_MULTIPLIER), short.MinValue, short.MaxValue);
            VelZ = (short)Math.Clamp((int)(z * VELOCITY_MULTIPLIER), short.MinValue, short.MaxValue);
        }

        // ============ 辅助方法:解码 ============

        /// <summary>
        /// 解码位置
        /// </summary>
        public (float x, float y, float z) GetPosition()
        {
            return (
                PosX * POSITION_PRECISION,
                PosY * POSITION_PRECISION,
                PosZ * POSITION_PRECISION
            );
        }

        /// <summary>
        /// 解码旋转 (从 Smallest Three 还原四元数)
        /// </summary>
        public (float x, float y, float z, float w) GetRotation()
        {
            float[] components = new float[4];
            float[] compressed = {
                RotA * ROTATION_PRECISION,
                RotB * ROTATION_PRECISION,
                RotC * ROTATION_PRECISION
            };

            // 还原 3 个分量
            int readIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i != RotOmitIndex)
                {
                    components[i] = compressed[readIndex++];
                }
            }

            // 根据四元数归一化特性,计算省略的分量
            float sumSquares = components[0] * components[0] + 
                              components[1] * components[1] + 
                              components[2] * components[2];
            
            components[RotOmitIndex] = (float)Math.Sqrt(Math.Max(0, 1.0f - sumSquares));

            return (components[0], components[1], components[2], components[3]);
        }

        /// <summary>
        /// 解码速度
        /// </summary>
        public (float x, float y, float z) GetVelocity()
        {
            return (
                VelX * VELOCITY_PRECISION,
                VelY * VELOCITY_PRECISION,
                VelZ * VELOCITY_PRECISION
            );
        }

        /// <summary>
        /// 创建副本
        /// </summary>
        public UnitySyncData Clone()
        {
            return new UnitySyncData
            {
                SteamId = this.SteamId,
                PosX = this.PosX,
                PosY = this.PosY,
                PosZ = this.PosZ,
                RotA = this.RotA,
                RotB = this.RotB,
                RotC = this.RotC,
                RotOmitIndex = this.RotOmitIndex,
                VelX = this.VelX,
                VelY = this.VelY,
                VelZ = this.VelZ,
                SequenceNumber = this.SequenceNumber,
            };
        }

        public override string ToString()
        {
            var pos = GetPosition();
            var vel = GetVelocity();
            return $"Player:{SteamId} Pos:({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                   $"Vel:({vel.x:F2},{vel.y:F2},{vel.z:F2}) Seq:{SequenceNumber}";
        }
    }
}
