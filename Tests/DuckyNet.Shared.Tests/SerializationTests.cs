using DuckyNet.RPC.Core;
using DuckyNet.RPC.Generated;
using DuckyNet.Shared.Data;
using FluentAssertions;
using Xunit;

namespace DuckyNet.Shared.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void TypeRegistry_IncludesSharedDtos()
        {
            var types = RpcTypeRegistry.GetSerializableTypes();

            types.Should().Contain(typeof(ScenelData));
            types.Should().Contain(typeof(NpcSpawnData));
            types.Should().Contain(typeof(EquipmentSlotUpdateRequest));
        }

        [Fact]
        public void SerializeAndDeserialize_ScenelData_RoundTrips()
        {
            var serializer = RpcSerializer.Instance;
            var scenelData = new ScenelData("MainScene", "");

            var data = serializer.Serialize(scenelData);
            var roundTrip = serializer.Deserialize<ScenelData>(data);

            roundTrip.Should().BeEquivalentTo(scenelData);
        }

        [Fact]
        public void SerializeAndDeserialize_NpcSpawnData_WithDefaults()
        {
            var serializer = RpcSerializer.Instance;
            var npcSpawn = new NpcSpawnData
            {
                NpcId = string.Empty,
                SceneName = string.Empty,
                SubSceneName = string.Empty,
                NpcType = string.Empty,
                PositionX = 0f,
                PositionY = 0f,
                PositionZ = 0f,
                RotationY = 0f,
                MaxHealth = 0f,
                SpawnTimestamp = 0
            };

            var data = serializer.Serialize(npcSpawn);
            var roundTrip = serializer.Deserialize<NpcSpawnData>(data);

            roundTrip.Should().BeEquivalentTo(npcSpawn);
        }
    }
}
