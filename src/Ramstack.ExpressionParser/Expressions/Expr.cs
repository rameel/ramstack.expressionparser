namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Represents a node in an expression tree, serving as the base class for all expression types.
/// </summary>
public abstract partial class Expr(ExprKind kind)
{
    /// <summary>
    /// Gets the kind of this expression node.
    /// </summary>
    /// <value>
    /// An <see cref="ExprKind"/> value indicating the type of expression, such as a literal, binary operation, or reference.
    /// </value>
    public ExprKind Kind { get; init; } = kind;

    /// <summary>
    /// Deconstructs the expression into its kind.
    /// </summary>
    /// <param name="kind">The <see cref="ExprKind"/> of this expression node, output as an enumeration value.</param>
    public void Deconstruct(out ExprKind kind) =>
        kind = Kind;

    /// <summary>
    /// Accepts a visitor to process this expression node and return a result of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the visitor.</typeparam>
    /// <param name="visitor">The <see cref="ExprVisitor{T}"/> instance that processes this expression.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> as determined by the visitor's implementation.
    /// </returns>
    public abstract T Accept<T>(ExprVisitor<T> visitor);
}
