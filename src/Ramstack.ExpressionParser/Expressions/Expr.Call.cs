namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a method call expression in an expression tree, consisting of a target expression and its parameters.
    /// </summary>
    public sealed class Call(Expr expression, IReadOnlyList<Expr> parameters) : Expr(ExprKind.Call)
    {
        /// <summary>
        /// Gets the expression being called, such as a method or function reference.
        /// </summary>
        /// <value>
        /// An <see cref="Expr"/> representing the target of the call, e.g., <c>Math.Abs</c>.
        /// </value>
        public Expr Expression { get; } = expression;

        /// <summary>
        /// Gets the list of parameters passed to the call.
        /// </summary>
        /// <value>
        /// An <see cref="IReadOnlyList{T}"/> of <see cref="Expr"/> instances representing the arguments, e.g., <c>-5</c>.
        /// </value>
        public IReadOnlyList<Expr> Parameters { get; } = parameters;

        /// <summary>
        /// Deconstructs the call expression into its target expression and parameters.
        /// </summary>
        /// <param name="expression">The <see cref="Expr"/> representing the target of the call.</param>
        /// <param name="parameters">The <see cref="IReadOnlyList{T}"/> of <see cref="Expr"/> instances representing the call’s arguments.</param>
        public void Deconstruct(out Expr expression, out IReadOnlyList<Expr> parameters) =>
            (expression, parameters) = (Expression, Parameters);

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitCall(this);

        /// <inheritdoc />
        public override string ToString() =>
            $"{Expression}({string.Join(", ", Parameters)})";
    }
}
