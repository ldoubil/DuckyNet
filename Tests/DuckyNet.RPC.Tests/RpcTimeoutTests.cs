using System.Collections.Concurrent;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Utils;
using FluentAssertions;
using Xunit;

namespace DuckyNet.RPC.Tests
{
    public class RpcTimeoutTests
    {
        [Fact]
        public async Task SetTimeout_MarksTimedOut()
        {
            var timeoutManager = new RpcTimeoutManager();

            timeoutManager.SetTimeout(7, 50);

            await Task.Delay(120);

            timeoutManager.IsTimedOut(7).Should().BeTrue();

            timeoutManager.ClearTimeout(7);
            timeoutManager.IsTimedOut(7).Should().BeFalse();
        }

        [Fact]
        public async Task RegisterPendingCall_TimesOutAndFaultsTask()
        {
            var pending = new ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>>();
            var timeoutManager = new RpcTimeoutManager();
            var config = new RpcConfig { DefaultTimeoutMs = 50 };
            var serializer = RpcSerializer.Instance;
            var handler = new RpcResponseHandler(pending, timeoutManager, config, serializer);
            var tcs = new TaskCompletionSource<RpcResponse>();

            handler.RegisterPendingCall(99, tcs);

            Func<Task> act = async () => await tcs.Task;

            await act.Should().ThrowAsync<TimeoutException>();
        }
    }
}
