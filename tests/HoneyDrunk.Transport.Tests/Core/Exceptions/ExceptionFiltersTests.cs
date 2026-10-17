using HoneyDrunk.Transport.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace HoneyDrunk.Transport.Tests.Core.Exceptions;

/// <summary>
/// Tests for <see cref="ExceptionFilters.IsFatal"/> classification.
/// </summary>
[SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "Tests verify classification of runtime-reserved exception types; construction is intentional.")]
public sealed class ExceptionFiltersTests
{
    /// <summary>
    /// Verifies <see cref="OutOfMemoryException"/> is classified fatal.
    /// </summary>
    [Fact]
    public void IsFatal_WithOutOfMemoryException_ReturnsTrue()
    {
        Assert.True(new OutOfMemoryException().IsFatal());
    }

    /// <summary>
    /// Verifies <see cref="StackOverflowException"/> is classified fatal.
    /// </summary>
    [Fact]
    public void IsFatal_WithStackOverflowException_ReturnsTrue()
    {
        Assert.True(new StackOverflowException().IsFatal());
    }

    /// <summary>
    /// Verifies <see cref="AccessViolationException"/> is classified fatal.
    /// </summary>
    [Fact]
    public void IsFatal_WithAccessViolationException_ReturnsTrue()
    {
        Assert.True(new AccessViolationException().IsFatal());
    }

    /// <summary>
    /// Verifies ordinary exceptions are not classified fatal.
    /// </summary>
    /// <param name="exceptionType">The concrete exception type under test.</param>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(ApplicationException))]
    public void IsFatal_WithNonFatalExceptions_ReturnsFalse(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.False(ex.IsFatal());
    }

    /// <summary>
    /// Verifies fatal exceptions wrapped in <see cref="AggregateException"/> are detected.
    /// </summary>
    [Fact]
    public void IsFatal_WithAggregateWrappingOom_ReturnsTrue()
    {
        var aggregate = new AggregateException(new InvalidOperationException("boom"), new OutOfMemoryException());
        Assert.True(aggregate.IsFatal());
    }

    /// <summary>
    /// Verifies non-fatal aggregate exceptions are not classified fatal.
    /// </summary>
    [Fact]
    public void IsFatal_WithAggregateOfNonFatal_ReturnsFalse()
    {
        var aggregate = new AggregateException(new InvalidOperationException(), new TimeoutException());
        Assert.False(aggregate.IsFatal());
    }

    /// <summary>
    /// Verifies fatal exceptions wrapped as an inner exception are detected.
    /// </summary>
    [Fact]
    public void IsFatal_WithFatalAsInnerException_ReturnsTrue()
    {
        var wrapped = new InvalidOperationException("outer", new OutOfMemoryException());
        Assert.True(wrapped.IsFatal());
    }

    /// <summary>
    /// Verifies a deeply nested fatal exception is still detected.
    /// </summary>
    [Fact]
    public void IsFatal_WithDeeplyNestedFatal_ReturnsTrue()
    {
        var innerAggregate = new AggregateException(new OutOfMemoryException());
        var middle = new InvalidOperationException("l2", innerAggregate);
        var outer = new InvalidOperationException("l1", middle);

        Assert.True(outer.IsFatal());
    }

    /// <summary>
    /// Verifies passing <c>null</c> throws.
    /// </summary>
    [Fact]
    public void IsFatal_WithNull_Throws()
    {
        Exception? ex = null;
        Assert.Throws<ArgumentNullException>(() => ex!.IsFatal());
    }
}
