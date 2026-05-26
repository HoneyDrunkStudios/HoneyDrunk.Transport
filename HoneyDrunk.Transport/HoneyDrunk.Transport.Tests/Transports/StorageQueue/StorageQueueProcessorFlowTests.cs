using Azure.Storage.Queues.Models;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.StorageQueue.Internal;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// End-to-end flow tests for <see cref="StorageQueueProcessor"/> covering the helpers
/// extracted during the Sonar cleanup refactor:
/// <c>ConsumeMessagesAsync</c>, <c>RunReceiveAndProcessIterationAsync</c>,
/// <c>ProcessBatchAsync</c>, <c>ProcessMessageAsync</c>,
/// <c>HandlePipelineResultAsync</c>, <c>CompleteSuccessAsync</c>,
/// <c>PoisonAsync</c>, <c>ScheduleRetryOrPoisonAsync</c>, and <c>HandleEmptyQueueAsync</c>.
/// </summary>
/// <remarks>
/// Uses a <see cref="FakeQueueClient"/> + <see cref="FakeQueueClientFactory"/> rather
/// than Azurite so the suite runs in the same process as the other unit tests.
/// </remarks>
public sealed class StorageQueueProcessorFlowTests
{
    private const string PrimaryQueue = "test-queue";
    private const string PoisonQueue = "test-queue-poison";
    private static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// On <see cref="MessageProcessingResult.Success"/> the processor deletes the
    /// message from the primary queue and never touches the poison queue.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnSuccess_DeletesPrimaryAndLeavesPoisonAlone()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        primary.EnqueueReceivable(BuildMessage("msg-success", dequeueCount: 1));

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Success);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => primary.DeletedMessageIds.Count > 0);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Contains("msg-success", primary.DeletedMessageIds);
            Assert.Empty(poison.SentMessageBodies);
            Assert.Equal(1, harness.Pipeline.ProcessedCount);
        }
    }

    /// <summary>
    /// On <see cref="MessageProcessingResult.DeadLetter"/> the processor pushes to the
    /// poison queue and then deletes from the primary.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnDeadLetter_SendsToPoisonAndDeletesFromPrimary()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        primary.EnqueueReceivable(BuildMessage("msg-dead-letter", dequeueCount: 1));

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.DeadLetter);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => poison.SentMessageBodies.Count > 0 && primary.DeletedMessageIds.Count > 0);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Single(poison.SentMessageBodies);
            Assert.Contains("msg-dead-letter", primary.DeletedMessageIds);
        }
    }

    /// <summary>
    /// On <see cref="MessageProcessingResult.Retry"/> below the dequeue ceiling the
    /// processor leaves the message alone — Storage Queue will resurface it after the
    /// visibility timeout.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnRetry_BelowMaxDequeue_LeavesMessageUntouched()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        primary.EnqueueReceivable(BuildMessage("msg-retry", dequeueCount: 1));

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Retry, opts => opts.MaxDequeueCount = 5);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => harness.Pipeline.ProcessedCount >= 1);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Empty(primary.DeletedMessageIds);
            Assert.Empty(poison.SentMessageBodies);
        }
    }

    /// <summary>
    /// On <see cref="MessageProcessingResult.Retry"/> when the message has already
    /// hit <c>MaxDequeueCount</c> the processor escalates to poison instead of
    /// letting the message resurface forever.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnRetry_AtMaxDequeue_EscalatesToPoison()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        primary.EnqueueReceivable(BuildMessage("msg-max-dequeue", dequeueCount: 5));

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Retry, opts => opts.MaxDequeueCount = 5);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => poison.SentMessageBodies.Count > 0);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Single(poison.SentMessageBodies);
            Assert.Contains("msg-max-dequeue", primary.DeletedMessageIds);
        }
    }

    /// <summary>
    /// When pipeline processing throws and the message has been dequeued past the
    /// ceiling the processor moves it to the poison queue via the exception-path
    /// fallback (not <see cref="MessageProcessingResult.DeadLetter"/>).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnPipelineException_AtMaxDequeue_MovesToPoison()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        primary.EnqueueReceivable(BuildMessage("msg-thrown", dequeueCount: 3));

        var pipeline = new StubMessagePipeline(new InvalidOperationException("handler boom"));
        var harness = BuildProcessorWith(primary, poison, pipeline, opts => opts.MaxDequeueCount = 3);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => poison.SentMessageBodies.Count > 0);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Single(poison.SentMessageBodies);
            Assert.Contains("msg-thrown", primary.DeletedMessageIds);
        }
    }

    /// <summary>
    /// When pipeline processing throws and dequeue count is below the ceiling the
    /// processor swallows the error (Storage Queue will resurface the message) and
    /// does not delete or poison.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnPipelineException_BelowMaxDequeue_SwallowsAndLeavesMessage()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        primary.EnqueueReceivable(BuildMessage("msg-thrown-retry", dequeueCount: 1));

        var pipeline = new StubMessagePipeline(new InvalidOperationException("handler boom"));
        var harness = BuildProcessorWith(primary, poison, pipeline, opts => opts.MaxDequeueCount = 5);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => pipeline.ProcessedCount >= 1);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Empty(primary.DeletedMessageIds);
            Assert.Empty(poison.SentMessageBodies);
        }
    }

    /// <summary>
    /// When the queue is empty the processor backs off and polls again instead of
    /// hot-looping. The receive count grows over time but the pipeline is never
    /// invoked because no messages arrive.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnEmptyQueue_BacksOffAndKeepsPolling()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Success);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => primary.ReceiveCallCount >= 2);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Equal(0, harness.Pipeline.ProcessedCount);
            Assert.Empty(primary.DeletedMessageIds);
        }
    }

    /// <summary>
    /// Calling <see cref="StorageQueueProcessor.StartAsync"/> twice without an
    /// intervening stop is invalid — the processor surfaces an
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_StartAsync_WhenAlreadyStarted_Throws()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Success);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => harness.Processor.StartAsync(CancellationToken.None));
            }
            finally
            {
                await harness.Processor.StopAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Calling <see cref="StorageQueueProcessor.StopAsync"/> without a prior start
    /// is a no-op — the processor does not throw and never receives from the queue.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_StopAsync_WithoutStart_IsNoOp()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Success);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Equal(0, primary.ReceiveCallCount);
            Assert.Empty(primary.DeletedMessageIds);
            Assert.Empty(poison.SentMessageBodies);
        }
    }

    /// <summary>
    /// With <c>BatchProcessingConcurrency &gt; 1</c> the processor parallel-processes
    /// the batch via the semaphore-gated path. All messages must still be deleted on
    /// success and the pipeline invoked once per message.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnBatchWithConcurrency_ProcessesAllMessagesInParallel()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        for (var i = 0; i < 3; i++)
        {
            primary.EnqueueReceivable(BuildMessage($"batch-{i}", dequeueCount: 1));
        }

        var harness = BuildProcessor(
            primary,
            poison,
            MessageProcessingResult.Success,
            opts =>
            {
                opts.BatchProcessingConcurrency = 4;
                opts.PrefetchMaxMessages = 32;
            });
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => primary.DeletedMessageIds.Count >= 3);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Equal(3, primary.DeletedMessageIds.Count);
            Assert.Empty(poison.SentMessageBodies);
            Assert.Equal(3, harness.Pipeline.ProcessedCount);
        }
    }

    /// <summary>
    /// A transient HTTP 5xx <see cref="Azure.RequestFailedException"/> from the
    /// receive call is caught by <c>RunReceiveAndProcessIterationAsync</c>, the loop
    /// backs off briefly, and the next iteration succeeds — so the eventual
    /// success-path message is still processed.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnTransientReceiveError_RecoversOnNextIteration()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        var followUp = BuildMessage("after-transient", dequeueCount: 1);

        primary.ReceiveOverride = index => index == 0
            ? Task.FromException<QueueMessage[]>(new Azure.RequestFailedException(503, "Service unavailable"))
            : Task.FromResult<QueueMessage[]>(index == 1 ? [followUp] : []);

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Success);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);

            // The transient-error branch sleeps a hardcoded 5 seconds before the
            // next iteration, so this test must tolerate that worst case.
            await WaitForAsync(() => primary.DeletedMessageIds.Contains("after-transient"), timeoutMs: 8000);
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Contains("after-transient", primary.DeletedMessageIds);
            Assert.Empty(poison.SentMessageBodies);
        }
    }

    /// <summary>
    /// When pushing to the poison queue fails the processor logs and does NOT
    /// rethrow — the primary message stays so it can be retried (Storage Queue
    /// reappear-after-visibility-timeout semantics).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_OnPoisonSendFailure_KeepsPrimaryMessageForRetry()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        poison.OnSend = _ => throw new InvalidOperationException("poison send broken");

        primary.EnqueueReceivable(BuildMessage("msg-poison-fail", dequeueCount: 1));

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.DeadLetter);
        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => harness.Pipeline.ProcessedCount >= 1);
            await harness.Processor.StopAsync(CancellationToken.None);

            // The pipeline ran (so PoisonAsync executed), the poison send threw, and
            // therefore the primary delete was skipped — message will resurface.
            Assert.Empty(primary.DeletedMessageIds);
        }
    }

    /// <summary>
    /// With every log level enabled (via <see cref="CapturingLogger{T}"/>) the
    /// processor exercises the structured-logging branches that are skipped under
    /// the default <see cref="NullLogger{T}"/>: <c>LogDebug</c> on success,
    /// <c>LogWarning</c> on max-dequeue escalation, <c>LogError</c> on poison-send
    /// failure, and the <c>LogDebug</c> retry-path entry.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_WithLoggingEnabled_EmitsExpectedLogBranches()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);

        // Three messages — one of each path: success → log Debug on completion;
        // dequeue >= max → log Warning + Poison; retry → log Debug.
        primary.EnqueueReceivable(BuildMessage("log-success", dequeueCount: 1));
        primary.EnqueueReceivable(BuildMessage("log-max-dequeue", dequeueCount: 5));
        primary.EnqueueReceivable(BuildMessage("log-retry", dequeueCount: 1));

        var pipeline = new StubMessagePipeline((env, _, _) =>
        {
            // Map messageId → desired result so a single test exercises all paths.
            return env.MessageId switch
            {
                "log-success" => Task.FromResult(MessageProcessingResult.Success),
                _ => Task.FromResult(MessageProcessingResult.Retry),
            };
        });

        var capturing = new CapturingLogger<StorageQueueProcessor>();
        var harness = BuildProcessorWith(
            primary,
            poison,
            pipeline,
            opts =>
            {
                opts.MaxDequeueCount = 5;
                opts.PrefetchMaxMessages = 32;
            },
            capturing);

        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => primary.DeletedMessageIds.Count >= 2 && pipeline.ProcessedCount >= 3);
            await harness.Processor.StopAsync(CancellationToken.None);

            // log-success → deleted from primary; log-max-dequeue → moved to poison +
            // deleted from primary; log-retry → no delete, no poison.
            Assert.Contains("log-success", primary.DeletedMessageIds);
            Assert.Contains("log-max-dequeue", primary.DeletedMessageIds);
            Assert.DoesNotContain("log-retry", primary.DeletedMessageIds);
            Assert.Single(poison.SentMessageBodies);

            // Verify the gated log-emission branches actually ran.
            Assert.Contains(capturing.Entries, e => e.level == LogLevel.Warning && e.message.Contains("exceeded max dequeue count"));
            Assert.Contains(capturing.Entries, e => e.level == LogLevel.Debug && e.message.Contains("Successfully processed message"));
            Assert.Contains(capturing.Entries, e => e.level == LogLevel.Debug && e.message.Contains("will be retried"));
        }
    }

    /// <summary>
    /// When pushing to the poison queue fails on the <c>MaxDequeue</c> escalation
    /// path (<c>dueToMaxDequeue: true</c>) the processor takes the corresponding
    /// branch of the now-split <c>LogError</c> emission with the
    /// "poison queue operation" wording.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_PoisonFailure_AtMaxDequeue_LogsPoisonOperationTemplate()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        poison.OnSend = _ => throw new InvalidOperationException("poison send broken");

        // dequeueCount == MaxDequeueCount triggers ScheduleRetryOrPoisonAsync's
        // escalation path (dueToMaxDequeue: true).
        primary.EnqueueReceivable(BuildMessage("log-poison-max-dequeue", dequeueCount: 5));

        var capturing = new CapturingLogger<StorageQueueProcessor>();
        var harness = BuildProcessorWith(
            primary,
            poison,
            new StubMessagePipeline(MessageProcessingResult.Retry),
            opts => opts.MaxDequeueCount = 5,
            capturing);

        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => capturing.Entries.Any(e => e.level == LogLevel.Error && e.message.Contains("poison queue operation")));
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Empty(primary.DeletedMessageIds);
            Assert.Contains(capturing.Entries, e => e.level == LogLevel.Error && e.message.Contains("poison queue operation"));
        }
    }

    /// <summary>
    /// When pushing to the poison queue fails AND the logger has <c>Error</c>
    /// enabled, the processor takes the <c>LogError</c> branch with the
    /// dueToMaxDequeue-aware message template. Combined with
    /// <c>Processor_OnPoisonSendFailure_KeepsPrimaryMessageForRetry</c> this also
    /// exercises the <c>dueToMaxDequeue == false</c> arm of the conditional template.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_PoisonFailure_WithLoggingEnabled_LogsErrorTemplate()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);
        poison.OnSend = _ => throw new InvalidOperationException("poison send broken");

        primary.EnqueueReceivable(BuildMessage("log-poison-fail", dequeueCount: 1));

        var capturing = new CapturingLogger<StorageQueueProcessor>();
        var harness = BuildProcessorWith(
            primary,
            poison,
            new StubMessagePipeline(MessageProcessingResult.DeadLetter),
            configure: null,
            capturing);

        await using (harness.ConfigureAwait(false))
        {
            await harness.Processor.StartAsync(CancellationToken.None);
            await WaitForAsync(() => capturing.Entries.Any(e => e.level == LogLevel.Error && e.message.Contains("dead-letter operation")));
            await harness.Processor.StopAsync(CancellationToken.None);

            Assert.Empty(primary.DeletedMessageIds);
            Assert.Contains(capturing.Entries, e => e.level == LogLevel.Error && e.message.Contains("dead-letter operation"));
        }
    }

    /// <summary>
    /// After disposal further <see cref="StorageQueueProcessor.StartAsync"/> calls
    /// must throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Processor_StartAsync_AfterDispose_Throws()
    {
        var primary = new FakeQueueClient(PrimaryQueue);
        var poison = new FakeQueueClient(PoisonQueue);

        var harness = BuildProcessor(primary, poison, MessageProcessingResult.Success);
        await harness.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => harness.Processor.StartAsync(CancellationToken.None));
    }

    private static ProcessorHarness BuildProcessor(
        FakeQueueClient primary,
        FakeQueueClient poison,
        MessageProcessingResult result,
        Action<StorageQueueOptions>? configure = null)
    {
        var pipeline = new StubMessagePipeline(result);
        return BuildProcessorWith(primary, poison, pipeline, configure);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transferred to ProcessorHarness which disposes everything in DisposeAsync.")]
    private static ProcessorHarness BuildProcessorWith(
        FakeQueueClient primary,
        FakeQueueClient poison,
        StubMessagePipeline pipeline,
        Action<StorageQueueOptions>? configure = null,
        ILogger<StorageQueueProcessor>? logger = null)
    {
        var queueOptions = new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = PrimaryQueue,
            PoisonQueueName = PoisonQueue,
            CreateIfNotExists = false,
            MaxConcurrency = 1,
            BatchProcessingConcurrency = 1,
            MaxDequeueCount = 5,
            EmptyQueuePollingInterval = FastPoll,
            MaxPollingInterval = FastPoll,
            EnableTelemetry = false
        };
        configure?.Invoke(queueOptions);

        var options = Options.Create(queueOptions);
        var factory = new FakeQueueClientFactory(primary, poison);
        var poisonMover = new PoisonQueueMover(NullLogger<PoisonQueueMover>.Instance);
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var processor = new StorageQueueProcessor(
            factory,
            pipeline,
            scopeFactory,
            poisonMover,
            options,
            logger ?? NullLogger<StorageQueueProcessor>.Instance);

        return new ProcessorHarness(processor, pipeline, factory, services);
    }

    private static QueueMessage BuildMessage(string id, long dequeueCount)
    {
        var envelope = new
        {
            messageId = id,
            timestamp = DateTimeOffset.UtcNow,
            messageType = "HoneyDrunk.Transport.Tests.Support.SampleMessage",
            headers = new Dictionary<string, string>(),
            payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"value\":\"x\"}"))
        };
        var body = JsonSerializer.Serialize(envelope);

        return QueuesModelFactory.QueueMessage(
            messageId: id,
            popReceipt: $"pop-{id}",
            messageText: body,
            dequeueCount: dequeueCount);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Condition not satisfied within {timeoutMs}ms.");
    }

    /// <summary>
    /// Disposable bag that wraps the processor + collaborators so tests get a single
    /// <c>await using</c> handle and CA2000 sees a deterministic ownership chain.
    /// </summary>
    private sealed class ProcessorHarness(
        StorageQueueProcessor processor,
        StubMessagePipeline pipeline,
        FakeQueueClientFactory factory,
        ServiceProvider services) : IAsyncDisposable
    {
        public StorageQueueProcessor Processor { get; } = processor;

        public StubMessagePipeline Pipeline { get; } = pipeline;

        public async ValueTask DisposeAsync()
        {
            await Processor.DisposeAsync().ConfigureAwait(false);
            await factory.DisposeAsync().ConfigureAwait(false);
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }
}
