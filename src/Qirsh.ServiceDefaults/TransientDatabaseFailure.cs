using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Qirsh.ServiceDefaults;

/// <summary>
/// Failures that mean "another transaction won a race; run the consumer again and it will see the winner's rows",
/// as opposed to bugs or bad messages. EF Core wraps the Postgres error, so the whole inner-exception chain is checked.
/// </summary>
public static class TransientDatabaseFailure
{
    /// <summary>serialization_failure, deadlock_detected, unique_violation (two consumers inserting the same key).</summary>
    private static readonly HashSet<string> SqlStates = ["40001", "40P01", "23505"];

    public static bool Is(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is DbUpdateConcurrencyException || (e is DbException { SqlState: { } state } && SqlStates.Contains(state)))
            {
                return true;
            }
        }

        return false;
    }
}
