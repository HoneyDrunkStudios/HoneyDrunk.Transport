namespace HoneyDrunk.Transport.Exceptions;

/// <summary>
/// Shared helpers for filtering exceptions in <c>catch</c> clauses.
/// </summary>
public static class ExceptionFilters
{
    /// <summary>
    /// Returns <c>true</c> for exceptions the process cannot meaningfully recover from
    /// (out-of-memory, stack-overflow, access-violation, thread-abort). Use as the
    /// predicate in <c>catch (Exception ex) when (!ex.IsFatal())</c> so broad catches
    /// still let fatal conditions propagate.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns><c>true</c> if the exception represents an unrecoverable runtime condition; otherwise <c>false</c>.</returns>
    public static bool IsFatal(this Exception ex) =>
        ex is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or ThreadAbortException;
}
