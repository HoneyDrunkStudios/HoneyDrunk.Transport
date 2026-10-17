using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Tests.Core.Abstractions;

/// <summary>
/// Tests for <see cref="PublishOptions"/>.
/// </summary>
public sealed class PublishOptionsTests
{
    /// <summary>
    /// Defaults are neutral and normal priority.
    /// </summary>
    [Fact]
    public void PublishOptions_WhenCreated_HasExpectedDefaults()
    {
        var options = new PublishOptions();

        Assert.Null(options.TimeToLive);
        Assert.Null(options.ScheduledEnqueueTime);
        Assert.Null(options.PartitionKey);
        Assert.Null(options.SessionId);
        Assert.Equal(MessagePriority.Normal, options.Priority);
        Assert.Null(options.AdditionalOptions);
    }

    /// <summary>
    /// Init-only options preserve custom values.
    /// </summary>
    [Fact]
    public void PublishOptions_WithCustomValues_StoresValues()
    {
        var scheduled = DateTimeOffset.UtcNow.AddMinutes(10);
        var additional = new Dictionary<string, object> { ["transport"] = "custom" };

        var options = new PublishOptions
        {
            TimeToLive = TimeSpan.FromMinutes(5),
            ScheduledEnqueueTime = scheduled,
            PartitionKey = "partition-1",
            SessionId = "session-1",
            Priority = MessagePriority.High,
            AdditionalOptions = additional
        };

        Assert.Equal(TimeSpan.FromMinutes(5), options.TimeToLive);
        Assert.Equal(scheduled, options.ScheduledEnqueueTime);
        Assert.Equal("partition-1", options.PartitionKey);
        Assert.Equal("session-1", options.SessionId);
        Assert.Equal(MessagePriority.High, options.Priority);
        Assert.Same(additional, options.AdditionalOptions);
    }
}
