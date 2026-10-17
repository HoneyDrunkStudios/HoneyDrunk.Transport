using HoneyDrunk.Transport.StorageQueue.Configuration;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for Storage Queue configuration.
/// </summary>
public sealed class StorageQueueConfigurationTests
{
    /// <summary>
    /// Verifies StorageQueueOptions defaults.
    /// </summary>
    [Fact]
    public void StorageQueueOptions_WhenCreated_HasExpectedDefaults()
    {
        var opts = new StorageQueueOptions();
        Assert.Null(opts.ConnectionString);
        Assert.Equal(string.Empty, opts.QueueName);
        Assert.True(opts.CreateIfNotExists);
        Assert.Equal(TimeSpan.FromSeconds(30), opts.VisibilityTimeout);
        Assert.Equal(16, opts.PrefetchMaxMessages);
        Assert.Equal(5, opts.MaxDequeueCount);
        Assert.Equal(1, opts.BatchProcessingConcurrency);
    }

    /// <summary>
    /// Verifies StorageQueueOptions can be set.
    /// </summary>
    [Fact]
    public void StorageQueueOptions_WithCustomValues_StoresValuesCorrectly()
    {
        var opts = new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false,
            VisibilityTimeout = TimeSpan.FromMinutes(1),
            PrefetchMaxMessages = 10,
            MaxDequeueCount = 3,
            BatchProcessingConcurrency = 4
        };

        Assert.Equal("UseDevelopmentStorage=true", opts.ConnectionString);
        Assert.Equal("test-queue", opts.QueueName);
        Assert.False(opts.CreateIfNotExists);
        Assert.Equal(TimeSpan.FromMinutes(1), opts.VisibilityTimeout);
        Assert.Equal(10, opts.PrefetchMaxMessages);
        Assert.Equal(3, opts.MaxDequeueCount);
        Assert.Equal(4, opts.BatchProcessingConcurrency);
    }
}
