using Azure;
using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Test double for <see cref="QueueClient"/> that overrides the methods used by the
/// Storage Queue processor / sender / poison-mover and records the calls so tests
/// can assert on them. The Azure SDK's <c>QueueClient</c> is constructible against
/// <c>UseDevelopmentStorage=true</c> without performing network I/O, so subclassing
/// works without Azurite running.
/// </summary>
internal sealed class FakeQueueClient(string queueName = "test-queue")
    : QueueClient("UseDevelopmentStorage=true", queueName)
{
    private readonly ConcurrentQueue<QueueMessage> _pendingReceives = new();
    private readonly ConcurrentBag<string> _deletedMessageIds = new();
    private readonly ConcurrentBag<string> _sentMessageBodies = new();
    private int _receiveCount;

    /// <summary>Gets the ids of messages that have been deleted from this fake.</summary>
    public IReadOnlyCollection<string> DeletedMessageIds => [.. _deletedMessageIds];

    /// <summary>Gets the bodies of messages that have been sent to this fake.</summary>
    public IReadOnlyCollection<string> SentMessageBodies => [.. _sentMessageBodies];

    /// <summary>Gets the number of <c>ReceiveMessagesAsync</c> invocations.</summary>
    public int ReceiveCallCount => _receiveCount;

    /// <summary>Gets or sets an optional override for <c>ReceiveMessagesAsync</c>.</summary>
    public Func<int, Task<QueueMessage[]>>? ReceiveOverride { get; set; }

    /// <summary>Gets or sets an optional callback invoked when <c>DeleteMessageAsync</c> is called.</summary>
    public Func<string, string, Task>? OnDelete { get; set; }

    /// <summary>Gets or sets an optional callback invoked when <c>SendMessageAsync</c> is called.</summary>
    public Func<string, Task>? OnSend { get; set; }

    /// <summary>Queues a message to be returned by the next <c>ReceiveMessagesAsync</c> call.</summary>
    /// <param name="message">The message to enqueue.</param>
    public void EnqueueReceivable(QueueMessage message) => _pendingReceives.Enqueue(message);

    /// <inheritdoc />
    public override Task<Response<QueueMessage[]>> ReceiveMessagesAsync(
        int? maxMessages = null,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var index = Interlocked.Increment(ref _receiveCount) - 1;

        if (ReceiveOverride is not null)
        {
            return WrapAsync(ReceiveOverride(index));
        }

        var messages = new List<QueueMessage>();
        var max = maxMessages ?? int.MaxValue;
        while (messages.Count < max && _pendingReceives.TryDequeue(out var m))
        {
            messages.Add(m);
        }

        return WrapAsync(Task.FromResult(messages.ToArray()));
    }

    /// <inheritdoc />
    public override async Task<Response> DeleteMessageAsync(
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default)
    {
        _deletedMessageIds.Add(messageId);

        if (OnDelete is not null)
        {
            // `await` (not `ContinueWith`) so a faulted callback task surfaces as the
            // expected exception instead of being silently masked by a fake OK response.
            await OnDelete(messageId, popReceipt).ConfigureAwait(false);
        }

        return FakeResponse.Ok;
    }

    /// <inheritdoc />
    public override async Task<Response<SendReceipt>> SendMessageAsync(
        string messageText,
        CancellationToken cancellationToken = default)
    {
        _sentMessageBodies.Add(messageText);

        if (OnSend is not null)
        {
            // See DeleteMessageAsync: await so OnSend's faulted task propagates the
            // exception instead of being swallowed by a continuation.
            await OnSend(messageText).ConfigureAwait(false);
        }

        return Response.FromValue(
            QueuesModelFactory.SendReceipt(
                messageId: Guid.NewGuid().ToString("N"),
                insertionTime: DateTimeOffset.UtcNow,
                expirationTime: DateTimeOffset.UtcNow.AddDays(7),
                popReceipt: Guid.NewGuid().ToString("N"),
                timeNextVisible: DateTimeOffset.UtcNow),
            FakeResponse.Created);
    }

    private static async Task<Response<QueueMessage[]>> WrapAsync(Task<QueueMessage[]> source)
    {
        var value = await source.ConfigureAwait(false);
        return Response.FromValue(value, FakeResponse.Ok);
    }

    private sealed class FakeResponse(int status) : Response
    {
        // Shared, stateless instances — disposing them is a no-op so reuse is safe.
        public static readonly Response Ok = new FakeResponse(200);
        public static readonly Response Created = new FakeResponse(201);

        public override int Status { get; } = status;

        public override string ReasonPhrase => string.Empty;

        public override Stream? ContentStream
        {
            get => null;
            set { }
        }

        public override string ClientRequestId
        {
            get => string.Empty;
            set { }
        }

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }
}
