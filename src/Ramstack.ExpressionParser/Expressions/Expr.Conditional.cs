namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a conditional (ternary) expression in an expression tree.
    /// </summary>
    public sealed class Conditional(Expr test, Expr ifTrue, Expr ifFalse) : Expr(ExprKind.Conditional)
    {
        /// <summary>
        /// Gets the condition expression evaluated to determine the outcome.
        /// </summary>
        /// <value>
        /// An <see cref="Expr"/> representing the test condition, e.g., <c>a > b</c>.
        /// </value>
        public Expr Test { get; } = test;

        /// <summary>
        /// Gets the expression evaluated if the condition is <see langword="true" />.
        /// </summary>
        public Expr IfTrue { get; } = ifTrue;

        /// <summary>
        /// Gets the expression evaluated if the condition is <see langword="false" />.
        /// </summary>
        public Expr IfFalse { get; } = ifFalse;

        /// <summary>
        /// Deconstructs the conditional expression into its test condition and branch expressions.
        /// </summary>
        /// <param name="test">The <see cref="Expr"/> representing the condition to evaluate.</param>
        /// <param name="ifTrue">The <see cref="Expr"/> representing the <see langword="true" /> branch.</param>
        /// <param name="ifFalse">The <see cref="Expr"/> representing the <see langword="false" /> branch.</param>
        public void Deconstruct(out Expr test, out Expr ifTrue, out Expr ifFalse) =>
            (test, ifTrue, ifFalse) = (Test, IfTrue, IfFalse);

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitConditional(this);

        /// <inheritdoc />
        public override string ToString() =>
            $"{Test} ? {IfTrue} : {IfFalse}";
    }
}
