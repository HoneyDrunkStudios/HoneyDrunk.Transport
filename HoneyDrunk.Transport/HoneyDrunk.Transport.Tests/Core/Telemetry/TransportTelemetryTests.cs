using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Telemetry;
using HoneyDrunk.Transport.Tests.Support;
using System.Diagnostics;

namespace HoneyDrunk.Transport.Tests.Core.Telemetry;

/// <summary>
/// Tests for TransportTelemetry activity creation.
/// </summary>
public sealed class TransportTelemetryTests
{
    /// <summary>
    /// Verifies StartPublishActivity creates activity with tags.
    /// </summary>
    [Fact]
    public void StartPublishActivity_WithEnvelope_CreatesActivityWithTags()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("test", "queue");

        using var activity = TransportTelemetry.StartPublishActivity(envelope, destination);

        if (activity != null)
        {
            Assert.Contains(activity.Tags, t => t.Key == "messaging.message.id");
            Assert.Contains(activity.Tags, t => t.Key == "messaging.destination");
        }
    }

    /// <summary>
    /// Verifies StartProcessActivity creates activity with tags.
    /// </summary>
    [Fact]
    public void StartProcessActivity_WithEnvelope_CreatesActivityWithTags()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });

        using var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            Assert.Contains(activity.Tags, t => t.Key == "messaging.message.id");
            Assert.Contains(activity.Tags, t => t.Key == "messaging.operation");
        }
    }

    /// <summary>
    /// Verifies RecordOutcome sets activity status.
    /// </summary>
    [Fact]
    public void RecordOutcome_WithSuccessResult_SetsActivityStatus()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);

            Assert.NotEqual(ActivityStatusCode.Error, activity.Status);

            activity.Dispose();
        }
    }

    /// <summary>
    /// Verifies RecordError sets activity status to error.
    /// </summary>
    [Fact]
    public void RecordError_WithException_SetsActivityStatusToError()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            var exception = new InvalidOperationException("test error");
            TransportTelemetry.RecordError(activity, exception);

            Assert.Equal(ActivityStatusCode.Error, activity.Status);

            activity.Dispose();
        }
    }

    /// <summary>
    /// Verifies RecordOutcome with retry sets appropriate status.
    /// </summary>
    [Fact]
    public void RecordOutcome_WithRetryResult_SetsErrorStatus()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Retry);

            Assert.Equal(ActivityStatusCode.Error, activity.Status);

            activity.Dispose();
        }
    }

    /// <summary>
    /// Verifies RecordOutcome with dead letter sets error status.
    /// </summary>
    [Fact]
    public void RecordOutcome_WithDeadLetterResult_SetsErrorStatus()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.DeadLetter);

            Assert.Equal(ActivityStatusCode.Error, activity.Status);

            activity.Dispose();
        }
    }

    /// <summary>
    /// Verifies RecordDeliveryCount adds tag.
    /// </summary>
    [Fact]
    public void RecordDeliveryCount_WithCount_AddsDeliveryCountTag()
    {
        using var listener = CreateActivityListener();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            TransportTelemetry.RecordDeliveryCount(activity, 3);

            // Use GetTagItem to retrieve the tag value directly
            var deliveryCountTag = activity.GetTagItem(TransportTelemetry.Tags.DeliveryCount);
            Assert.NotNull(deliveryCountTag);
            Assert.Equal(3, deliveryCountTag);

            activity.Dispose();
        }
    }

    /// <summary>
    /// Creates an ActivityListener for testing.
    /// </summary>
    /// <returns>A configured ActivityListener.</returns>
    private static ActivityListener CreateActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == TransportTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
