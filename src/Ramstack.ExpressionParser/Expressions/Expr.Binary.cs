namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a binary operation expression in an expression tree, consisting of an operator and two operands.
    /// </summary>
    public sealed class Binary(Identifier @operator, Expr lhs, Expr rhs) : Expr(ExprKind.Binary)
    {
        /// <summary>
        /// Gets the operator of the binary expression.
        /// </summary>
        /// <value>
        /// An <see cref="Identifier"/> representing the operator, such as <c>"+"</c> or <c>"-"</c>.
        /// </value>
        public Identifier Operator { get; } = @operator;

        /// <summary>
        /// Gets the left-hand side operand of the binary expression.
        /// </summary>
        public Expr Left { get; } = lhs;

        /// <summary>
        /// Gets the right-hand side operand of the binary expression.
        /// </summary>
        public Expr Right { get; } = rhs;

        /// <summary>
        /// Deconstructs the binary expression into its operator and operands.
        /// </summary>
        /// <param name="operator">The <see cref="Identifier"/> representing the operator of the binary expression.</param>
        /// <param name="lhs">The <see cref="Expr"/> representing the left-hand side operand.</param>
        /// <param name="rhs">The <see cref="Expr"/> representing the right-hand side operand.</param>
        public void Deconstruct(out Identifier @operator, out Expr lhs, out Expr rhs) =>
            (@operator, lhs, rhs) = (Operator, Left, Right);

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitBinary(this);

        /// <inheritdoc />
        public override string ToString() =>
            $"({Left} {Operator} {Right})";
    }
}
