using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Outbox;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Outbox;

/// <summary>
/// Tests for <see cref="DefaultOutboxDispatcher"/> dispatch behavior.
/// </summary>
public sealed class DefaultOutboxDispatcherTests
{
    /// <summary>
    /// Empty batches return without publishing or marking messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DispatchPendingAsync_WhenNoMessagesExist_DoesNothing()
    {
        var store = Substitute.For<IOutboxStore>();
        store.LoadPendingAsync(10, Arg.Any<CancellationToken>()).Returns([]);
        var publisher = Substitute.For<ITransportPublisher>();
        using var dispatcher = Create(store, publisher);

        await dispatcher.DispatchPendingAsync(10);

        await publisher.DidNotReceive().PublishAsync(
            Arg.Any<ITransportEnvelope>(),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Successfully published outbox messages are marked dispatched.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DispatchPendingAsync_WhenPublishSucceeds_MarksMessageDispatched()
    {
        var message = CreateMessage("outbox-1", attempts: 0);
        var store = Substitute.For<IOutboxStore>();
        store.LoadPendingAsync(100, Arg.Any<CancellationToken>()).Returns([message]);
        var publisher = Substitute.For<ITransportPublisher>();
        using var dispatcher = Create(store, publisher);

        await dispatcher.DispatchPendingAsync();

        await publisher.Received(1).PublishAsync(message.Envelope, message.Destination, Arg.Any<CancellationToken>());
        await store.Received(1).MarkDispatchedAsync("outbox-1", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Failed publish attempts below the retry limit are marked failed with a future retry time.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DispatchPendingAsync_WhenPublishFailsBelowRetryLimit_MarksFailedWithRetryTime()
    {
        var before = DateTimeOffset.UtcNow;
        var message = CreateMessage("outbox-2", attempts: 2);
        var store = Substitute.For<IOutboxStore>();
        store.LoadPendingAsync(100, Arg.Any<CancellationToken>()).Returns([message]);
        var publisher = Substitute.For<ITransportPublisher>();
        publisher.PublishAsync(message.Envelope, message.Destination, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("broker down"));
        using var dispatcher = Create(store, publisher, new OutboxDispatcherOptions
        {
            BaseRetryDelay = TimeSpan.FromSeconds(1),
            MaxRetryDelay = TimeSpan.FromMinutes(1),
            MaxRetryAttempts = 5
        });

        await dispatcher.DispatchPendingAsync();

        await store.Received(1).MarkFailedAsync(
            "outbox-2",
            "broker down",
            Arg.Is<DateTimeOffset?>(value => value >= before.AddSeconds(3) && value <= DateTimeOffset.UtcNow.AddSeconds(10)),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().MarkPoisonedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Failed publish attempts at the retry limit are marked poisoned.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DispatchPendingAsync_WhenPublishFailsAtRetryLimit_MarksPoisoned()
    {
        var message = CreateMessage("outbox-3", attempts: 5);
        var store = Substitute.For<IOutboxStore>();
        store.LoadPendingAsync(100, Arg.Any<CancellationToken>()).Returns([message]);
        var publisher = Substitute.For<ITransportPublisher>();
        publisher.PublishAsync(message.Envelope, message.Destination, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("broker down"));
        using var dispatcher = Create(store, publisher, new OutboxDispatcherOptions { MaxRetryAttempts = 5 });

        await dispatcher.DispatchPendingAsync();

        await store.Received(1).MarkPoisonedAsync(
            "outbox-3",
            "Exceeded maximum retry attempts (5)",
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().MarkFailedAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Dispatch handles mixed success and failure in one batch.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DispatchPendingAsync_WhenBatchHasMixedOutcomes_ProcessesAllMessages()
    {
        var success = CreateMessage("success", attempts: 0);
        var failure = CreateMessage("failure", attempts: 1);
        var store = Substitute.For<IOutboxStore>();
        store.LoadPendingAsync(100, Arg.Any<CancellationToken>()).Returns([success, failure]);
        var publisher = Substitute.For<ITransportPublisher>();
        publisher.PublishAsync(failure.Envelope, failure.Destination, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("broker down"));
        using var dispatcher = Create(store, publisher);

        await dispatcher.DispatchPendingAsync();

        await store.Received(1).MarkDispatchedAsync("success", Arg.Any<CancellationToken>());
        await store.Received(1).MarkFailedAsync(
            "failure",
            "broker down",
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creates an outbox dispatcher for tests.
    /// </summary>
    /// <param name="store">The outbox store.</param>
    /// <param name="publisher">The transport publisher.</param>
    /// <param name="options">Optional dispatcher options.</param>
    /// <returns>An outbox dispatcher.</returns>
    private static DefaultOutboxDispatcher Create(
        IOutboxStore store,
        ITransportPublisher publisher,
        OutboxDispatcherOptions? options = null)
    {
        return new DefaultOutboxDispatcher(
            store,
            publisher,
            Options.Create(options ?? new OutboxDispatcherOptions()),
            NullLogger<DefaultOutboxDispatcher>.Instance);
    }

    /// <summary>
    /// Creates a pending outbox message.
    /// </summary>
    /// <param name="id">The outbox identifier.</param>
    /// <param name="attempts">The current attempt count.</param>
    /// <returns>An outbox message.</returns>
    private static OutboxMessage CreateMessage(string id, int attempts)
    {
        return new OutboxMessage
        {
            Id = id,
            Destination = TestData.Address("orders", "orders.queue"),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = id }),
            State = OutboxMessageState.Pending,
            Attempts = attempts,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
