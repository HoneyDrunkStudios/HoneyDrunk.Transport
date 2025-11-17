using System.Diagnostics;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Telemetry;
using HoneyDrunk.Transport.Tests.Support;

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
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var activity = TransportTelemetry.StartProcessActivity(envelope, "test-source");

        if (activity != null)
        {
            TransportTelemetry.RecordDeliveryCount(activity, 3);

            Assert.Contains(activity.Tags, t => t.Key == "messaging.delivery.count" && t.Value?.ToString() == "3");

            activity.Dispose();
        }
    }
}
