using DuckyNet.RPC.Core;
using DuckyNet.RPC.Messages;
using FluentAssertions;
using Xunit;

namespace DuckyNet.RPC.Tests
{
    public class RpcMessageBuilderTests
    {
        [RpcService("Inventory")]
        private interface IInventoryService
        {
        }

        [Fact]
        public void BuildRequest_UsesServiceAttributeAndSequentialIds()
        {
            var serializer = RpcSerializer.Instance;
            var builder = new RpcMessageBuilder(serializer);

            var first = builder.BuildRequest<IInventoryService>("AddItem", "apple", 2);
            var second = builder.BuildRequest<IInventoryService>("RemoveItem", 1);

            first.ServiceName.Should().Be("Inventory");
            first.MethodName.Should().Be("AddItem");
            first.MessageId.Should().Be(1);

            second.ServiceName.Should().Be("Inventory");
            second.MethodName.Should().Be("RemoveItem");
            second.MessageId.Should().Be(2);

            serializer.DeserializeParameters(first.Parameters)
                .Should().BeEquivalentTo(new object?[] { "apple", 2 });
        }
    }
}
