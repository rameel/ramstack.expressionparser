namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Represents an identifier in an expression, encapsulating its name as a string.
/// </summary>
public sealed class Identifier(string name)
{
    /// <summary>
    /// Gets the name of the identifier.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Deconstructs the identifier into its constituent name.
    /// </summary>
    /// <param name="name">The name of the identifier, output as a string.</param>
    public void Deconstruct(out string name) =>
        name = Name;

    /// <inheritdoc />
    public override string ToString() =>
        Name;
}
