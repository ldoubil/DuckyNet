using DuckyNet.RPC.Core;
using DuckyNet.RPC.Messages;
using FluentAssertions;
using Xunit;

namespace DuckyNet.RPC.Tests
{
    public class RpcSerializerTests
    {
        [Fact]
        public void SerializeAndDeserializeRpcMessage_PreservesPayload()
        {
            var serializer = RpcSerializer.Instance;
            var message = new RpcMessage
            {
                MessageId = 42,
                ServiceName = "Inventory",
                MethodName = "AddItem",
                Parameters = serializer.SerializeParameters(new object?[] { "apple", 3 })
            };

            var data = serializer.Serialize(message);

            serializer.DetectMessageType(data).Should().Be(RpcMessageType.Request);

            var roundTrip = serializer.Deserialize<RpcMessage>(data);
            roundTrip.Should().BeEquivalentTo(message);

            var parameters = serializer.DeserializeParameters(roundTrip.Parameters);
            parameters.Should().BeEquivalentTo(new object?[] { "apple", 3 });
        }

        [Fact]
        public void SerializeParameters_ReturnsNullForEmptyInputs()
        {
            var serializer = RpcSerializer.Instance;

            serializer.SerializeParameters(null).Should().BeNull();
            serializer.SerializeParameters(Array.Empty<object>()).Should().BeNull();
            serializer.DeserializeParameters(null).Should().BeNull();
            serializer.DeserializeParameters(Array.Empty<byte>()).Should().BeNull();
        }
    }
}
