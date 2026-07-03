using Merlin.Backend.Services;
using Merlin.Backend.Services.StreamingResponses;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class NonStreamingGenerationAdapterTests
{
    [Fact]
    public async Task StreamAsync_YieldsExactlyOneFinalDelta()
    {
        var adapter = new NonStreamingGenerationAdapter(new FakeChatProvider("Full response."));

        var deltas = await adapter.StreamAsync([new ChatMessage("user", "hello")]).ToListAsync();

        var delta = Assert.Single(deltas);
        Assert.Equal("Full response.", delta.Text);
        Assert.True(delta.IsFinal);
        Assert.Equal("Fake", delta.Provider);
    }

    [Fact]
    public async Task StreamAsync_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var adapter = new NonStreamingGenerationAdapter(new FakeChatProvider("unused"));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in adapter.StreamAsync([new ChatMessage("user", "hello")], cancellation.Token))
            {
            }
        });
    }

    private sealed class FakeChatProvider : IChatProvider
    {
        private readonly string _response;

        public FakeChatProvider(string response)
        {
            _response = response;
        }

        public string Name => "Fake";

        public Task<LlmProviderResult> GenerateAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LlmProviderResult.Succeeded(_response));
        }
    }
}

internal static class AsyncEnumerableTestExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }
}
