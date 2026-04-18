namespace HoneyDrunk.Transport.Exceptions;

/// <summary>
/// Shared helpers for filtering exceptions in <c>catch</c> clauses.
/// </summary>
public static class ExceptionFilters
{
    /// <summary>
    /// Returns <c>true</c> for exceptions the process cannot meaningfully recover from
    /// (out-of-memory, stack-overflow, access-violation, thread-abort). Walks inner
    /// exceptions so wrappers like <see cref="AggregateException"/>,
    /// <see cref="TypeInitializationException"/>, or
    /// <see cref="System.Reflection.TargetInvocationException"/> don't hide a fatal
    /// condition. Use as the predicate in
    /// <c>catch (Exception ex) when (!ex.IsFatal())</c> so broad catches still let
    /// fatal conditions propagate.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns><c>true</c> if the exception (or any wrapped inner exception) represents an unrecoverable runtime condition; otherwise <c>false</c>.</returns>
    public static bool IsFatal(this Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex switch
        {
            OutOfMemoryException or StackOverflowException or AccessViolationException or ThreadAbortException => true,
            AggregateException aggregate => aggregate.InnerExceptions.Any(IsFatal),
            { InnerException: { } inner } => inner.IsFatal(),
            _ => false,
        };
    }
}
