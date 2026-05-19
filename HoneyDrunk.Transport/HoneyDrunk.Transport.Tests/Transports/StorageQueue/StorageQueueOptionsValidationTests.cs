using HoneyDrunk.Transport.StorageQueue.Configuration;
using System.ComponentModel.DataAnnotations;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for <see cref="StorageQueueOptions"/> custom validation.
/// </summary>
public sealed class StorageQueueOptionsValidationTests
{
    /// <summary>
    /// Valid connection string settings produce no custom validation failures.
    /// </summary>
    [Fact]
    public void Validate_WithConnectionStringAndValidSettings_ReturnsNoErrors()
    {
        var options = new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "orders"
        };

        var errors = Validate(options);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Valid endpoint settings produce no custom validation failures.
    /// </summary>
    [Fact]
    public void Validate_WithAccountEndpointAndValidSettings_ReturnsNoErrors()
    {
        var options = new StorageQueueOptions
        {
            AccountEndpoint = new Uri("https://account.queue.core.windows.net"),
            QueueName = "orders"
        };

        var errors = Validate(options);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Missing authentication source returns a validation error.
    /// </summary>
    [Fact]
    public void Validate_WithoutConnectionStringOrEndpoint_ReturnsAuthenticationError()
    {
        var options = new StorageQueueOptions { QueueName = "orders" };

        var error = Assert.Single(Validate(options));

        Assert.Contains("Either ConnectionString or AccountEndpoint", error.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(nameof(StorageQueueOptions.ConnectionString), error.MemberNames);
        Assert.Contains(nameof(StorageQueueOptions.AccountEndpoint), error.MemberNames);
    }

    /// <summary>
    /// Resource-exhausting concurrency is rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenMaxConcurrencyTooHigh_ReturnsConcurrencyError()
    {
        var options = ValidOptions();
        options.MaxConcurrency = 101;

        var error = Assert.Single(Validate(options));

        Assert.Contains("MaxConcurrency cannot exceed 100", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Batch processing concurrency cannot exceed the fetched message count.
    /// </summary>
    [Fact]
    public void Validate_WhenBatchProcessingExceedsPrefetch_ReturnsBatchError()
    {
        var options = ValidOptions();
        options.PrefetchMaxMessages = 4;
        options.BatchProcessingConcurrency = 5;

        var error = Assert.Single(Validate(options));

        Assert.Contains("BatchProcessingConcurrency (5) cannot exceed PrefetchMaxMessages (4)", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Empty queue polling interval must not exceed the maximum polling interval.
    /// </summary>
    [Fact]
    public void Validate_WhenEmptyPollingExceedsMaxPolling_ReturnsPollingError()
    {
        var options = ValidOptions();
        options.EmptyQueuePollingInterval = TimeSpan.FromSeconds(10);
        options.MaxPollingInterval = TimeSpan.FromSeconds(5);

        var error = Assert.Single(Validate(options));

        Assert.Contains("EmptyQueuePollingInterval cannot be greater", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Visibility timeout below the service minimum is rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenVisibilityTimeoutTooShort_ReturnsVisibilityError()
    {
        var options = ValidOptions();
        options.VisibilityTimeout = TimeSpan.FromMilliseconds(500);

        var error = Assert.Single(Validate(options));

        Assert.Contains("VisibilityTimeout must be at least 1 second", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Visibility timeout above the service maximum is rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenVisibilityTimeoutTooLong_ReturnsVisibilityError()
    {
        var options = ValidOptions();
        options.VisibilityTimeout = TimeSpan.FromDays(8);

        var error = Assert.Single(Validate(options));

        Assert.Contains("VisibilityTimeout cannot exceed 7 days", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Message TTL below the service minimum is rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenMessageTimeToLiveTooShort_ReturnsTtlError()
    {
        var options = ValidOptions();
        options.MessageTimeToLive = TimeSpan.FromMilliseconds(500);

        var error = Assert.Single(Validate(options));

        Assert.Contains("MessageTimeToLive must be at least 1 second", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Message TTL above the service maximum is rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenMessageTimeToLiveTooLong_ReturnsTtlError()
    {
        var options = ValidOptions();
        options.MessageTimeToLive = TimeSpan.FromDays(8);

        var error = Assert.Single(Validate(options));

        Assert.Contains("MessageTimeToLive cannot exceed 7 days", error.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Custom poison queue name overrides the default name.
    /// </summary>
    [Fact]
    public void GetPoisonQueueName_WhenCustomNameSet_ReturnsCustomName()
    {
        var options = new StorageQueueOptions
        {
            QueueName = "orders",
            PoisonQueueName = "orders-dead"
        };

        Assert.Equal("orders-dead", options.GetPoisonQueueName());
    }

    /// <summary>
    /// Missing poison queue name defaults from the primary queue name.
    /// </summary>
    [Fact]
    public void GetPoisonQueueName_WhenCustomNameMissing_ReturnsDefaultName()
    {
        var options = new StorageQueueOptions { QueueName = "orders" };

        Assert.Equal("orders-poison", options.GetPoisonQueueName());
    }

    /// <summary>
    /// Creates valid options for focused validation tests.
    /// </summary>
    /// <returns>Valid storage queue options.</returns>
    private static StorageQueueOptions ValidOptions()
    {
        return new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "orders"
        };
    }

    /// <summary>
    /// Runs custom validation for options.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <returns>The validation results.</returns>
    private static List<ValidationResult> Validate(StorageQueueOptions options)
    {
        return options.Validate(new ValidationContext(options)).ToList();
    }
}
