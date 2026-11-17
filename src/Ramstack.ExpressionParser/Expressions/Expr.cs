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
    /// Gets the depth of this expression node.
    /// </summary>
    /// <value>
    /// One for a node without child expressions; otherwise, one more than the depth of its deepest child.
    /// </value>
    /// <remarks>
    /// The depth is computed from the depths of the children when the node is created,
    /// so the tree is not traversed recursively.
    /// </remarks>
    internal int Depth { get; } = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Expr"/> class with the specified kind and depth.
    /// </summary>
    /// <param name="kind">The kind of this expression node.</param>
    /// <param name="depth">The depth of this expression node.</param>
    private Expr(ExprKind kind, int depth) : this(kind) =>
        Depth = depth;

    /// <summary>
    /// Computes the depth of a node whose children are the specified expression and the specified list of expressions.
    /// </summary>
    /// <param name="expression">The first child expression.</param>
    /// <param name="list">The remaining child expressions.</param>
    /// <returns>
    /// One more than the depth of the deepest child.
    /// </returns>
    private static int ComputeDepth(Expr expression, IReadOnlyList<Expr> list)
    {
        var depth = expression.Depth;
        var count = list.Count;

        for (var i = 0; i < count; i++)
            depth = Math.Max(depth, list[i].Depth);

        return depth + 1;
    }

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
