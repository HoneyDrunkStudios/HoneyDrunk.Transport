using System.Diagnostics;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Telemetry;

/// <summary>
/// Telemetry and observability hooks for transport operations.
/// </summary>
public static class TransportTelemetry
{
    /// <summary>
    /// Activity source name for transport telemetry.
    /// </summary>
    public const string ActivitySourceName = "HoneyDrunk.Transport";

    /// <summary>
    /// Activity source for creating spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Creates a span for a publish operation.
    /// </summary>
    /// <param name="envelope">The message envelope being published.</param>
    /// <param name="destination">The destination endpoint.</param>
    /// <returns>An activity instance if telemetry is enabled; otherwise, null.</returns>
    public static Activity? StartPublishActivity(ITransportEnvelope envelope, IEndpointAddress destination)
    {
        var activity = ActivitySource.StartActivity("publish", ActivityKind.Producer);
        if (activity != null)
        {
            activity.SetTag(Tags.MessageId, envelope.MessageId);
            activity.SetTag(Tags.MessageType, envelope.MessageType);
            activity.SetTag(Tags.CorrelationId, envelope.CorrelationId);
            activity.SetTag(Tags.CausationId, envelope.CausationId);
            activity.SetTag(Tags.Destination, destination.Address);
            activity.SetTag(Tags.Operation, "publish");
            activity.SetTag(Tags.PayloadSize, envelope.Payload.Length);
        }

        return activity;
    }

    /// <summary>
    /// Creates a span for a receive/process operation.
    /// </summary>
    /// <param name="envelope">The message envelope being processed.</param>
    /// <param name="source">The source endpoint name.</param>
    /// <returns>An activity instance if telemetry is enabled; otherwise, null.</returns>
    public static Activity? StartProcessActivity(ITransportEnvelope envelope, string source)
    {
        var activity = ActivitySource.StartActivity("process", ActivityKind.Consumer);
        if (activity != null)
        {
            activity.SetTag(Tags.MessageId, envelope.MessageId);
            activity.SetTag(Tags.MessageType, envelope.MessageType);
            activity.SetTag(Tags.CorrelationId, envelope.CorrelationId);
            activity.SetTag(Tags.CausationId, envelope.CausationId);
            activity.SetTag(Tags.Destination, source);
            activity.SetTag(Tags.Operation, "process");
            activity.SetTag(Tags.PayloadSize, envelope.Payload.Length);
        }

        return activity;
    }

    /// <summary>
    /// Records the outcome of a message operation.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="result">The processing result.</param>
    public static void RecordOutcome(Activity? activity, MessageProcessingResult result)
    {
        activity?.SetTag(Tags.Outcome, result.ToString().ToLowerInvariant());

        if (result != MessageProcessingResult.Success)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
        }
    }

    /// <summary>
    /// Records an error in the activity.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordError(Activity? activity, Exception exception)
    {
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag(Tags.ErrorType, exception.GetType().FullName);
            activity.SetTag(Tags.ErrorMessage, exception.Message);
        }
    }

    /// <summary>
    /// Records delivery count.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="deliveryCount">The number of delivery attempts.</param>
    public static void RecordDeliveryCount(Activity? activity, int deliveryCount)
    {
        activity?.SetTag(Tags.DeliveryCount, deliveryCount);
    }

    /// <summary>
    /// Enriches an activity with custom tags.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="tags">The custom tags to add.</param>
    public static void EnrichActivity(Activity? activity, IDictionary<string, object?> tags)
    {
        if (activity == null)
        {
            return;
        }

        foreach (var kvp in tags)
        {
            activity.SetTag(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Telemetry tag names.
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// Message identifier tag.
        /// </summary>
        public const string MessageId = "messaging.message.id";

        /// <summary>
        /// Message type tag.
        /// </summary>
        public const string MessageType = "messaging.message.type";

        /// <summary>
        /// Correlation identifier tag.
        /// </summary>
        public const string CorrelationId = "messaging.correlation.id";

        /// <summary>
        /// Causation identifier tag.
        /// </summary>
        public const string CausationId = "messaging.causation.id";

        /// <summary>
        /// Destination endpoint tag.
        /// </summary>
        public const string Destination = "messaging.destination";

        /// <summary>
        /// Operation type tag.
        /// </summary>
        public const string Operation = "messaging.operation";

        /// <summary>
        /// Delivery count tag.
        /// </summary>
        public const string DeliveryCount = "messaging.delivery.count";

        /// <summary>
        /// Payload size tag.
        /// </summary>
        public const string PayloadSize = "messaging.payload.size";

        /// <summary>
        /// Outcome tag.
        /// </summary>
        public const string Outcome = "messaging.outcome";

        /// <summary>
        /// Error type tag.
        /// </summary>
        public const string ErrorType = "error.type";

        /// <summary>
        /// Error message tag.
        /// </summary>
        public const string ErrorMessage = "error.message";
    }
}
