namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Defines the types of unary operations supported in an expression tree.
/// </summary>
public enum UnaryType
{
    /// <summary>
    /// Represents a type cast or conversion operation, such as converting a value to a specific type.
    /// </summary>
    /// <example>
    /// In the expression <c>x:int</c>, this indicates a conversion of <c>x</c> to an integer.
    /// </example>
    Convert,

    /// <summary>
    /// Represents an arithmetic negation operation.
    /// </summary>
    /// <example>
    /// In the expression <c>-x</c>, this indicates the arithmetic negation of <c>x</c>.
    /// </example>
    Negate,

    /// <summary>
    /// Represents a logical negation operation.
    /// </summary>
    /// <example>
    /// In the expression <c>!x</c>, this indicates a logical NOT operation on <c>x</c>.
    /// </example>
    Not,

    /// <summary>
    /// Represents a bitwise ones complement operation, flipping all bits of a value.
    /// </summary>
    /// <example>
    /// In the expression <c>~x</c>, this indicates a ones complement operation on <c>x</c>.
    /// </example>
    OnesComplement,

    /// <summary>
    /// Represents a unary plus operation.
    /// </summary>
    /// <example>
    /// In the expression <c>+x</c>, this indicates a unary plus operation on <c>x</c>.
    /// </example>
    UnaryPlus
}
