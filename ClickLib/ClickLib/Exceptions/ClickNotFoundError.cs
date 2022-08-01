using System;

namespace ClickLib.Exceptions;

/// <summary>
/// An exception thrown when a click cannot be found.
/// </summary>
public class ClickNotFoundError : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickNotFoundError"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    public ClickNotFoundError(string message)
        : base(message)
    {
    }
}
