namespace Ramstack.Parsing;

/// <summary>
/// Represents an exception that occurs during parsing.
/// </summary>
public sealed class ParseErrorException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseErrorException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public ParseErrorException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseErrorException"/> class
    /// with a specified error message and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="innerException">The exception that caused the current exception,
    /// or <see langword="null"/> if no inner exception is specified.</param>
    public ParseErrorException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
