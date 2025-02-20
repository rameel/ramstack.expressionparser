namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a parenthesized expression in an expression tree, used to enforce precedence or grouping.
    /// </summary>
    /// <param name="expression">The <see cref="Expr"/> enclosed within parentheses.</param>
    public sealed class Parenthesized(Expr expression) : Expr(ExprKind.Parenthesized)
    {
        /// <summary>
        /// Gets the expression enclosed within parentheses.
        /// </summary>
        public Expr Expression { get; } = expression;

        /// <summary>
        /// Deconstructs the parenthesized expression into its inner expression.
        /// </summary>
        /// <param name="expression">The <see cref="Expr"/> enclosed within parentheses.</param>
        public void Deconstruct(out Expr expression) =>
            expression = Expression;

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitParenthesized(this);

        /// <inheritdoc />
        public override string ToString() =>
            $"({Expression})";
    }
}
