namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Represents a name in an expression: an identifier, such as a member or type name,
/// or an operator, such as <c>+</c> or <c>??</c>.
/// </summary>
/// <param name="name">The name of the identifier or the symbol of the operator.</param>
public sealed class Identifier(string name)
{
    /// <summary>
    /// Gets the name of the identifier or the symbol of the operator.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Deconstructs the identifier into its name.
    /// </summary>
    /// <param name="name">The name of the identifier or the symbol of the operator.</param>
    public void Deconstruct(out string name) =>
        name = Name;

    /// <inheritdoc />
    public override string ToString() =>
        Name;

    /// <summary>
    /// Returns the name as it is written in an expression.
    /// </summary>
    /// <returns>
    /// The name, prefixed with <c>@</c> if it is a keyword, such as <c>true</c>,
    /// so that the parser reads it back as an identifier.
    /// </returns>
    /// <remarks>
    /// The keywords are those of the <c>keyword</c> rule in <see cref="ExpressionParser" />.
    /// </remarks>
    internal string ToSourceString() =>
        Name is "true" or "false" or "null" ? $"@{Name}" : Name;
}
