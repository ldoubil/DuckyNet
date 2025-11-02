using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 动画状态同步数据 - 紧凑的二进制格式
    /// 用于同步 Animator 的关键参数和状态
    /// </summary>
    [Serializable]
    public class AnimatorSyncData
    {
        /// <summary>
        /// 当前动画状态哈希（主层）
        /// </summary>
        public int StateHash { get; set; }

        /// <summary>
        /// 动画归一化时间 (0-1)
        /// 压缩为 ushort (0-65535 映射到 0-1)
        /// </summary>
        public ushort NormalizedTime { get; set; }

        /// <summary>
        /// 关键 Float 参数（最多 8 个）
        /// 使用 short 存储，精度 0.01
        /// </summary>
        public short[] FloatParams { get; set; } = new short[8];

        /// <summary>
        /// Bool 参数打包（最多 32 个）
        /// 使用位标志压缩
        /// </summary>
        public uint BoolParams { get; set; }

        /// <summary>
        /// 压缩为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // StateHash (4 字节)
                writer.Write(StateHash);

                // NormalizedTime (2 字节)
                writer.Write(NormalizedTime);

                // FloatParams (16 字节 = 8 * 2)
                for (int i = 0; i < 8; i++)
                {
                    writer.Write(FloatParams[i]);
                }

                // BoolParams (4 字节)
                writer.Write(BoolParams);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 从字节数组解压
        /// </summary>
        public static AnimatorSyncData FromBytes(byte[] data)
        {
            using (var ms = new System.IO.MemoryStream(data))
            using (var reader = new System.IO.BinaryReader(ms))
            {
                return new AnimatorSyncData
                {
                    StateHash = reader.ReadInt32(),
                    NormalizedTime = reader.ReadUInt16(),
                    FloatParams = new short[]
                    {
                        reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(),
                        reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()
                    },
                    BoolParams = reader.ReadUInt32()
                };
            }
        }

        /// <summary>
        /// 获取归一化时间（0-1）
        /// </summary>
        public float GetNormalizedTime()
        {
            return NormalizedTime / 65535f;
        }

        /// <summary>
        /// 设置归一化时间（0-1）
        /// </summary>
        public void SetNormalizedTime(float value)
        {
            // 使用标准 C# Math.Clamp (或手动实现)
            float clamped = Math.Max(0f, Math.Min(1f, value));
            NormalizedTime = (ushort)(clamped * 65535f);
        }

        /// <summary>
        /// 获取 Float 参数值
        /// </summary>
        public float GetFloatParam(int index)
        {
            if (index < 0 || index >= 8) return 0f;
            return FloatParams[index] / 100f;
        }

        /// <summary>
        /// 设置 Float 参数值
        /// </summary>
        public void SetFloatParam(int index, float value)
        {
            if (index < 0 || index >= 8) return;
            FloatParams[index] = (short)(value * 100f);
        }

        /// <summary>
        /// 获取 Bool 参数值
        /// </summary>
        public bool GetBoolParam(int index)
        {
            if (index < 0 || index >= 32) return false;
            return (BoolParams & (1u << index)) != 0;
        }

        /// <summary>
        /// 设置 Bool 参数值
        /// </summary>
        public void SetBoolParam(int index, bool value)
        {
            if (index < 0 || index >= 32) return;
            if (value)
                BoolParams |= (1u << index);
            else
                BoolParams &= ~(1u << index);
        }
    }
}
