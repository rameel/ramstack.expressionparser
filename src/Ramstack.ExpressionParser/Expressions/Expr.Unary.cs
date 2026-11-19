namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a unary operation expression in an expression tree, consisting of an operator and a single operand.
    /// </summary>
    /// <param name="operator">The <see cref="Identifier" /> representing the unary operator, e.g., <c>"-"</c> or <c>"int"</c>.</param>
    /// <param name="unaryType">The <see cref="UnaryType" /> specifying the kind of unary operation, such as negation or conversion.</param>
    /// <param name="operand">The <see cref="Expr" /> on which the unary operation is applied.</param>
    public sealed class Unary(Identifier @operator, UnaryType unaryType, Expr operand)
        : Expr(ExprKind.Unary, operand.Depth + 1)
    {
        /// <summary>
        /// Gets the operator of the unary expression.
        /// </summary>
        /// <value>
        /// An <see cref="Identifier" /> representing the unary operator, such as <c>"-"</c> or a type name like <c>"int"</c>.
        /// </value>
        public Identifier Operator { get; } = @operator;

        /// <summary>
        /// Gets the kind of the unary operation.
        /// </summary>
        /// <value>
        /// A <see cref="UnaryType" /> value indicating the kind of operation, such as negation or conversion.
        /// </value>
        public UnaryType UnaryType { get; } = unaryType;

        /// <summary>
        /// Gets the operand of the unary expression.
        /// </summary>
        /// <value>
        /// An <see cref="Expr" /> representing the expression the operator is applied to, e.g., <c>x</c>.
        /// </value>
        public Expr Operand { get; } = operand;

        /// <summary>
        /// Deconstructs the unary expression into its kind and operand.
        /// </summary>
        /// <param name="unaryType">The <see cref="UnaryType" /> of the unary operation.</param>
        /// <param name="operand">The <see cref="Expr" /> on which the operation is applied.</param>
        /// <example>
        /// <code>
        /// var unary = new Expr.Unary(new Identifier("-"), UnaryType.Negate, new Expr.Reference(new Identifier("x")));
        /// var (kind, operand) = unary;
        /// // kind is UnaryType.Negate, operand is the reference to x
        /// </code>
        /// </example>
        public void Deconstruct(out UnaryType unaryType, out Expr operand) =>
            (unaryType, operand) = (UnaryType, Operand);

        /// <summary>
        /// Deconstructs the unary expression into its operator, kind, and operand.
        /// </summary>
        /// <param name="operator">The <see cref="Identifier" /> representing the unary operator.</param>
        /// <param name="unaryType">The <see cref="UnaryType" /> of the unary operation.</param>
        /// <param name="operand">The <see cref="Expr" /> on which the operation is applied.</param>
        /// <example>
        /// <code>
        /// var unary = new Expr.Unary(new Identifier("-"), UnaryType.Negate, new Expr.Reference(new Identifier("x")));
        /// var (op, kind, operand) = unary;
        /// // op.Name is "-", kind is UnaryType.Negate, operand is the reference to x
        /// </code>
        /// </example>
        public void Deconstruct(out Identifier @operator, out UnaryType unaryType, out Expr operand) =>
            (@operator, unaryType, operand) = (Operator, UnaryType, Operand);

        /// <inheritdoc />
        /// <returns>
        /// The result of type <typeparamref name="T" /> produced by the visitor's <see cref="ExprVisitor{T}.VisitUnary" /> method.
        /// </returns>
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitUnary(this);

        /// <inheritdoc />
        /// <returns>
        /// A string representation of the unary expression, e.g., <c>-(x)</c> for operators or <c>x :> int</c> for conversions.
        /// </returns>
        public override string ToString() => UnaryType switch
        {
            UnaryType.Convert => $"{Operand} :> {Operator}",
            _ => $"{Operator}({Operand})"
        };
    }
}
