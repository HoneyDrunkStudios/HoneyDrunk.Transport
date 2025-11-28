using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Telemetry;
using System.Diagnostics;

namespace HoneyDrunk.Transport.Tests.Core.Telemetry;

/// <summary>
/// Tests for <see cref="TransportTelemetry"/> activity creation and tagging.
/// </summary>
public sealed class TransportTelemetryActivityTests
{
    /// <summary>
    /// Verifies publish activity sets expected tags.
    /// </summary>
    [Fact]
    public void StartPublishActivity_SetsExpectedTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == TransportTelemetry.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        var envelope = new TestEnvelope();
        using var activity = TransportTelemetry.StartPublishActivity(envelope, new TestEndpointAddress("orders"));
        Assert.NotNull(activity);
        Assert.Equal("publish", activity!.OperationName);
        AssertTag(activity, TransportTelemetry.Tags.MessageId, "mid");
        AssertTag(activity, TransportTelemetry.Tags.MessageType, envelope.MessageType);
        AssertTag(activity, TransportTelemetry.Tags.Destination, "orders");
        AssertTag(activity, TransportTelemetry.Tags.Operation, "publish");
    }

    /// <summary>
    /// Records outcome sets error status for non-success.
    /// </summary>
    [Fact]
    public void RecordOutcome_ErrorSetsStatus()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == TransportTelemetry.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        var envelope = new TestEnvelope();
        using var activity = TransportTelemetry.StartPublishActivity(envelope, new TestEndpointAddress("orders"));
        TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Retry);
        Assert.Equal(ActivityStatusCode.Error, activity?.Status);
    }

    /// <summary>
    /// Records error sets error tags.
    /// </summary>
    [Fact]
    public void RecordError_SetsErrorTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == TransportTelemetry.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        var envelope = new TestEnvelope();
        using var activity = TransportTelemetry.StartPublishActivity(envelope, new TestEndpointAddress("orders"));
        var ex = new InvalidOperationException("boom");
        TransportTelemetry.RecordError(activity, ex);
        Assert.Equal(ActivityStatusCode.Error, activity?.Status);
        AssertTag(activity!, TransportTelemetry.Tags.ErrorType, ex.GetType().FullName!);
        AssertTag(activity!, TransportTelemetry.Tags.ErrorMessage, "boom");
    }

    private static void AssertTag(Activity activity, string key, string expected)
    {
        var actual = activity.Tags.FirstOrDefault(t => t.Key == key).Value;
        Assert.Equal(expected, actual);
    }

    // Helper types after tests.
    private sealed class TestEnvelope : ITransportEnvelope
    {
        public string MessageId { get; init; } = "mid";

        public string? CorrelationId { get; init; } = "corr";

        public string? CausationId { get; init; } = "cause";

        public string? NodeId { get; init; }

        public string? StudioId { get; init; }

        public string? TenantId { get; init; }

        public string? ProjectId { get; init; }

        public string? Environment { get; init; }

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        public string MessageType { get; init; } = typeof(TestEnvelope).FullName!;

        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

        public ReadOnlyMemory<byte> Payload { get; init; } = new byte[] { 1, 2 };

        public ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders) => this;

        public ITransportEnvelope WithGridContext(
            string? correlationId = null,
            string? causationId = null,
            string? nodeId = null,
            string? studioId = null,
            string? tenantId = null,
            string? projectId = null,
            string? environment = null) => this;
    }

    private sealed class TestEndpointAddress(string address) : IEndpointAddress
    {
        public string Name { get; } = "test";

        public string Address { get; } = address;

        public IReadOnlyDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
    }
}
