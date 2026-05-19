using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using HoneyDrunk.Transport.StorageQueue.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Globalization;
using System.Text.Json;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for <see cref="PoisonQueueMover"/>.
/// </summary>
public sealed class PoisonQueueMoverTests
{
    /// <summary>
    /// MoveMessageToPoisonQueueAsync writes a diagnostic poison payload with error metadata.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task MoveMessageToPoisonQueueAsync_WithError_SendsDiagnosticEnvelope()
    {
        var mover = new PoisonQueueMover(NullLogger<PoisonQueueMover>.Instance);
        var poisonQueueClient = Substitute.For<QueueClient>();
        var message = CreateQueueMessage();
        string? captured = null;

        poisonQueueClient
            .SendMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<string>();
                return Task.FromResult(Substitute.For<Response<SendReceipt>>());
            });

        var error = new InvalidOperationException("handler failed");

        await mover.MoveMessageToPoisonQueueAsync(poisonQueueClient, message, error, 5, CancellationToken.None);

        Assert.NotNull(captured);
        using var document = JsonDocument.Parse(captured!);
        var root = document.RootElement;
        Assert.Equal("message-1", root.GetProperty("originalMessageId").GetString());
        Assert.Equal("body", root.GetProperty("originalMessage").GetString());
        Assert.Equal(5, root.GetProperty("dequeueCount").GetInt64());
        Assert.Equal(typeof(InvalidOperationException).FullName, root.GetProperty("errorType").GetString());
        Assert.Equal("handler failed", root.GetProperty("errorMessage").GetString());
        Assert.Equal("pop-1", root.GetProperty("metadata").GetProperty("PopReceipt").GetString());
    }

    /// <summary>
    /// MoveMessageToPoisonQueueAsync allows explicit dead-lettering without an exception.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task MoveMessageToPoisonQueueAsync_WithoutError_SerializesNullErrorMetadata()
    {
        var mover = new PoisonQueueMover(NullLogger<PoisonQueueMover>.Instance);
        var poisonQueueClient = Substitute.For<QueueClient>();
        var message = CreateQueueMessage();
        string? captured = null;

        poisonQueueClient
            .SendMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<string>();
                return Task.FromResult(Substitute.For<Response<SendReceipt>>());
            });

        await mover.MoveMessageToPoisonQueueAsync(poisonQueueClient, message, null, 7, CancellationToken.None);

        Assert.NotNull(captured);
        using var document = JsonDocument.Parse(captured!);
        var root = document.RootElement;
        Assert.Equal(7, root.GetProperty("dequeueCount").GetInt64());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errorType").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errorMessage").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errorStackTrace").ValueKind);
    }

    /// <summary>
    /// Null dependencies are rejected before any queue call is attempted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task MoveMessageToPoisonQueueAsync_WithNullArguments_Throws()
    {
        var mover = new PoisonQueueMover(NullLogger<PoisonQueueMover>.Instance);
        var message = CreateQueueMessage();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            mover.MoveMessageToPoisonQueueAsync(null!, message, null, 1, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            mover.MoveMessageToPoisonQueueAsync(Substitute.For<QueueClient>(), null!, null, 1, CancellationToken.None));
    }

    private static QueueMessage CreateQueueMessage() => QueuesModelFactory.QueueMessage(
        messageId: "message-1",
        popReceipt: "pop-1",
        body: BinaryData.FromString("body"),
        dequeueCount: 3,
        insertedOn: DateTimeOffset.Parse("2026-01-02T03:04:05Z", CultureInfo.InvariantCulture),
        expiresOn: DateTimeOffset.Parse("2026-01-09T03:04:05Z", CultureInfo.InvariantCulture),
        nextVisibleOn: DateTimeOffset.Parse("2026-01-02T03:05:05Z", CultureInfo.InvariantCulture));
}
