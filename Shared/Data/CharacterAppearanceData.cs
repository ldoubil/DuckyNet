using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 角色外观数据 - 紧凑的二进制格式
    /// 设计目标：最小化网络传输大小
    /// </summary>
    [Serializable]
    public class CharacterAppearanceData
    {
        /// <summary>
        /// 头部设置数据（紧凑格式）
        /// </summary>
        public HeadSettingData HeadSetting { get; set; } = new HeadSettingData();

        /// <summary>
        /// 部位数据数组（最多32个部位）
        /// </summary>
        public PartData[] Parts { get; set; } = Array.Empty<PartData>();

        /// <summary>
        /// 压缩为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // 版本号（1字节）
                writer.Write((byte)1);

                // 头部设置
                HeadSetting.WriteTo(writer);

                // 部位数量（1字节，最多255个部位）
                writer.Write((byte)Parts.Length);

                // 部位数据
                foreach (var part in Parts)
                {
                    part.WriteTo(writer);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 从字节数组解压
        /// </summary>
        public static CharacterAppearanceData FromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new CharacterAppearanceData();

            using (var ms = new System.IO.MemoryStream(data))
            using (var reader = new System.IO.BinaryReader(ms))
            {
                var result = new CharacterAppearanceData();

                // 版本号
                byte version = reader.ReadByte();
                if (version != 1)
                    throw new Exception($"不支持的外观数据版本: {version}");

                // 头部设置
                result.HeadSetting = HeadSettingData.ReadFrom(reader);

                // 部位数据
                byte partCount = reader.ReadByte();
                result.Parts = new PartData[partCount];
                for (int i = 0; i < partCount; i++)
                {
                    result.Parts[i] = PartData.ReadFrom(reader);
                }

                return result;
            }
        }
    }

    /// <summary>
    /// 头部设置数据（18字节）
    /// </summary>
    [Serializable]
    public class HeadSettingData
    {
        // 缩放（6字节 = 3 * Int16）
        public short ScaleX { get; set; }
        public short ScaleY { get; set; }
        public short ScaleZ { get; set; }

        // 偏移（6字节 = 3 * Int16）
        public short OffsetX { get; set; }
        public short OffsetY { get; set; }
        public short OffsetZ { get; set; }

        // 旋转（6字节 = 3 * Int16，存储度数 * 100）
        public short RotationX { get; set; }
        public short RotationY { get; set; }
        public short RotationZ { get; set; }

        public void WriteTo(System.IO.BinaryWriter writer)
        {
            writer.Write(ScaleX);
            writer.Write(ScaleY);
            writer.Write(ScaleZ);
            writer.Write(OffsetX);
            writer.Write(OffsetY);
            writer.Write(OffsetZ);
            writer.Write(RotationX);
            writer.Write(RotationY);
            writer.Write(RotationZ);
        }

        public static HeadSettingData ReadFrom(System.IO.BinaryReader reader)
        {
            return new HeadSettingData
            {
                ScaleX = reader.ReadInt16(),
                ScaleY = reader.ReadInt16(),
                ScaleZ = reader.ReadInt16(),
                OffsetX = reader.ReadInt16(),
                OffsetY = reader.ReadInt16(),
                OffsetZ = reader.ReadInt16(),
                RotationX = reader.ReadInt16(),
                RotationY = reader.ReadInt16(),
                RotationZ = reader.ReadInt16()
            };
        }
    }

    /// <summary>
    /// 部位数据（21字节）
    /// </summary>
    [Serializable]
    public class PartData
    {
        // 部位类型（1字节，0-255）
        public byte PartType { get; set; }

        // 部位ID（2字节，0-65535）
        public ushort PartId { get; set; }

        // 缩放（6字节）
        public short ScaleX { get; set; }
        public short ScaleY { get; set; }
        public short ScaleZ { get; set; }

        // 偏移（6字节）
        public short OffsetX { get; set; }
        public short OffsetY { get; set; }
        public short OffsetZ { get; set; }

        // 旋转（6字节）
        public short RotationX { get; set; }
        public short RotationY { get; set; }
        public short RotationZ { get; set; }

        public void WriteTo(System.IO.BinaryWriter writer)
        {
            writer.Write(PartType);
            writer.Write(PartId);
            writer.Write(ScaleX);
            writer.Write(ScaleY);
            writer.Write(ScaleZ);
            writer.Write(OffsetX);
            writer.Write(OffsetY);
            writer.Write(OffsetZ);
            writer.Write(RotationX);
            writer.Write(RotationY);
            writer.Write(RotationZ);
        }

        public static PartData ReadFrom(System.IO.BinaryReader reader)
        {
            return new PartData
            {
                PartType = reader.ReadByte(),
                PartId = reader.ReadUInt16(),
                ScaleX = reader.ReadInt16(),
                ScaleY = reader.ReadInt16(),
                ScaleZ = reader.ReadInt16(),
                OffsetX = reader.ReadInt16(),
                OffsetY = reader.ReadInt16(),
                OffsetZ = reader.ReadInt16(),
                RotationX = reader.ReadInt16(),
                RotationY = reader.ReadInt16(),
                RotationZ = reader.ReadInt16()
            };
        }
    }

    /// <summary>
    /// 辅助类：浮点数和Int16之间的转换
    /// 范围：-327.68 到 327.67，精度：0.01
    /// </summary>
    public static class FloatCompression
    {
        private const float SCALE = 100f;

        public static short Compress(float value)
        {
            return (short)Math.Round(value * SCALE);
        }

        public static float Decompress(short value)
        {
            return value / SCALE;
        }

        public static (short x, short y, short z) CompressVector3(float x, float y, float z)
        {
            return (Compress(x), Compress(y), Compress(z));
        }

        public static (float x, float y, float z) DecompressVector3(short x, short y, short z)
        {
            return (Decompress(x), Decompress(y), Decompress(z));
        }
    }
}

