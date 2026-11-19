namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Represents a visitor for <see cref="Expr"/> trees that produces a result of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the result produced by visiting the expression tree nodes.</typeparam>
public abstract class ExprVisitor<T>
{
    /// <summary>
    /// Dispatches the expression to the visit method for its node type by calling <see cref="Expr.Accept{T}"/>.
    /// </summary>
    /// <param name="expr">The <see cref="Expr"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by the corresponding visit method.
    /// </returns>
    public virtual T Visit(Expr expr) =>
        expr.Accept(this);

    /// <summary>
    /// Visits each expression in the list in order and returns the results.
    /// </summary>
    /// <param name="nodes">The expressions to visit.</param>
    /// <returns>
    /// The results of visiting the expressions, in the same order as <paramref name="nodes"/>.
    /// </returns>
    public virtual IReadOnlyList<T> Visit(IReadOnlyList<Expr> nodes)
    {
        if (nodes.Count == 0)
            return [];

        var result = new T[nodes.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = Visit(nodes[i]);

        return result;
    }

    /// <summary>
    /// Visits a reference expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Reference"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the reference expression.
    /// </returns>
    protected internal abstract T VisitReference(Expr.Reference expr);

    /// <summary>
    /// Visits a literal expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Literal"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the literal expression.
    /// </returns>
    protected internal abstract T VisitLiteral(Expr.Literal expr);

    /// <summary>
    /// Visits a binary operation expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Binary"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the binary expression.
    /// </returns>
    protected internal abstract T VisitBinary(Expr.Binary expr);

    /// <summary>
    /// Visits a method call expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Call"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the call expression.
    /// </returns>
    protected internal abstract T VisitCall(Expr.Call expr);

    /// <summary>
    /// Visits an indexer expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Indexer"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the indexer expression.
    /// </returns>
    protected internal abstract T VisitIndex(Expr.Indexer expr);

    /// <summary>
    /// Visits a member access expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.MemberAccess"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the member access expression.
    /// </returns>
    protected internal abstract T VisitMemberAccess(Expr.MemberAccess expr);

    /// <summary>
    /// Visits a unary operation expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Unary"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the unary expression.
    /// </returns>
    protected internal abstract T VisitUnary(Expr.Unary expr);

    /// <summary>
    /// Visits a conditional (ternary) expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Conditional"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the conditional expression.
    /// </returns>
    protected internal abstract T VisitConditional(Expr.Conditional expr);

    /// <summary>
    /// Visits a parenthesized expression node.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Parenthesized"/> to visit.</param>
    /// <returns>
    /// A result of type <typeparamref name="T"/> produced by processing the parenthesized expression.
    /// </returns>
    protected internal abstract T VisitParenthesized(Expr.Parenthesized expr);
}
