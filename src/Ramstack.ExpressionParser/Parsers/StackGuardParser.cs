namespace Ramstack.Parsing.Parsers;

/// <summary>
/// Represents a parser that stops parsing with a fatal error when the thread's stack
/// is too small to run the underlying parser safely.
/// </summary>
/// <remarks>
/// Wrap the parser through which a recursive grammar re-enters itself, so that deeply nested input
/// produces a parse error instead of a <see cref="StackOverflowException"/>.
/// </remarks>
/// <typeparam name="T">The type of the value produced by the underlying parser.</typeparam>
/// <param name="parser">The underlying parser.</param>
internal sealed class StackGuardParser<T>(Parser<T> parser) : Parser<T>
{
    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, [NotNullWhen(true)] out T? value)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            Parser.FatalError("The expression is nested too deeply.");

        return parser.TryParse(ref context, out value);
    }

    //
    // NOTE: The default void wrapper calls TryParse, so the check also runs when the value is discarded.
    //
}
