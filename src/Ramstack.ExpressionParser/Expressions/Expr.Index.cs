namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents an indexing operation in an expression tree, such as accessing an array or a parameterized property.
    /// </summary>
    public sealed class Indexer(Expr expression, IReadOnlyList<Expr> parameters) : Expr(ExprKind.Index)
    {
        /// <summary>
        /// Gets the expression being indexed, such as an array or object with an indexer.
        /// </summary>
        public Expr Expression { get; } = expression;

        /// <summary>
        /// Gets the list of parameters used as indices or arguments for the indexing operation.
        /// </summary>
        public IReadOnlyList<Expr> Parameters { get; } = parameters;

        /// <summary>
        /// Deconstructs the indexer expression into its target expression and index parameters.
        /// </summary>
        /// <param name="expression">The <see cref="Expr"/> representing the target being indexed.</param>
        /// <param name="parameters">The <see cref="IReadOnlyList{T}"/> of <see cref="Expr"/> instances representing the indices.</param>
        public void Deconstruct(out Expr expression, out IReadOnlyList<Expr> parameters) =>
            (expression, parameters) = (Expression, Parameters);

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitIndex(this);

        /// <inheritdoc />
        public override string ToString() =>
            $"{Expression}[{string.Join(", ", Parameters)}]";
    }
}
