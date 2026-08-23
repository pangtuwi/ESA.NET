namespace App.Core;

/// <summary>
/// Thrown when a legacy side file cannot be read: malformed content, a row count that
/// does not match the body, or a table that exceeds one of the fixed legacy capacities.
/// </summary>
/// <remarks>
/// The Delphi loaders responded to an over-long <c>.cwt</c> or <c>.exh</c> by calling
/// <c>Halt</c>, which terminates the application outright and loses the user's work.
/// The port raises instead, so the caller can report the offending file and carry on.
/// </remarks>
public class LegacyDataException : Exception
{
    public LegacyDataException()
    {
    }

    public LegacyDataException(string message) : base(message)
    {
    }

    public LegacyDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
