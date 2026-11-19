namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a method or delegate call in an expression tree, consisting of a target expression and its arguments.
    /// </summary>
    /// <param name="expression">The <see cref="Expr" /> representing the target of the call.</param>
    /// <param name="parameters">The <see cref="IReadOnlyList{T}" /> of <see cref="Expr" /> instances representing the call's arguments.</param>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "The type is named after the ExprKind.Call expression kind, and renaming it would break the public API.")]
    public sealed class Call(Expr expression, IReadOnlyList<Expr> parameters)
        : Expr(ExprKind.Call, ComputeDepth(expression, parameters))
    {
        /// <summary>
        /// Gets the expression that specifies the method or delegate to call.
        /// </summary>
        /// <value>
        /// An <see cref="Expr" /> representing the target of the call, such as <c>Math.Abs</c> in <c>Math.Abs(-5)</c>.
        /// </value>
        public Expr Expression { get; } = expression;

        /// <summary>
        /// Gets the arguments passed to the call.
        /// </summary>
        /// <value>
        /// An <see cref="IReadOnlyList{T}" /> of <see cref="Expr" /> instances representing the arguments,
        /// such as <c>-5</c> in <c>Math.Abs(-5)</c>.
        /// </value>
        public IReadOnlyList<Expr> Parameters { get; } = parameters;

        /// <summary>
        /// Deconstructs the call expression into its target expression and arguments.
        /// </summary>
        /// <param name="expression">The <see cref="Expr" /> representing the target of the call.</param>
        /// <param name="parameters">The <see cref="IReadOnlyList{T}" /> of <see cref="Expr" /> instances representing the call's arguments.</param>
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
