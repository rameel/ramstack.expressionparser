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
}
