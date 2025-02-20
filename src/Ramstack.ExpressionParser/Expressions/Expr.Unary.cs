namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents an unary operation expression in an expression tree, consisting of an operator and a single operand.
    /// </summary>
    /// <param name="operator">The <see cref="Identifier"/> representing the unary operator, e.g., <c>"-"</c> or <c>"int"</c>.</param>
    /// <param name="unaryType">The <see cref="UnaryType"/> specifying the kind of unary operation (e.g., arithmetic or conversion).</param>
    /// <param name="operand">The <see cref="Expr"/> on which the unary operation is applied.</param>
    public sealed class Unary(Identifier @operator, UnaryType unaryType, Expr operand) : Expr(ExprKind.Unary)
    {
        /// <summary>
        /// Gets the operator of the unary expression.
        /// </summary>
        /// <value>
        /// An <see cref="Identifier"/> representing the unary operator, such as <c>"-"</c> or a type name like <c>"int"</c>.
        /// </value>
        public Identifier Operator { get; } = @operator;

        /// <summary>
        /// Gets the type of the unary operation.
        /// </summary>
        /// <value>
        /// A <see cref="UnaryType"/> value indicating the nature of the operation, e.g., arithmetic or conversion.
        /// </value>
        public UnaryType UnaryType { get; } = unaryType;

        /// <summary>
        /// Gets the operand of the unary expression.
        /// </summary>
        /// <value>
        /// An <see cref="Expr"/> representing the expression the operator is applied to, e.g., <c>x</c>.
        /// </value>
        public Expr Operand { get; } = operand;

        /// <summary>
        /// Deconstructs the unary expression into its type and operand.
        /// </summary>
        /// <param name="unaryType">The <see cref="UnaryType"/> of the unary operation.</param>
        /// <param name="operand">The <see cref="Expr"/> on which the operation is applied.</param>
        /// <example>
        /// <code>
        /// var unary = new Unary(new Identifier("-"), UnaryType.Arithmetic, new Identifier("x"));
        /// unary.Deconstruct(out UnaryType type, out Expr op);
        /// // type is UnaryType.Arithmetic, op is Identifier("x")
        /// </code>
        /// </example>
        public void Deconstruct(out UnaryType unaryType, out Expr operand) =>
            (unaryType, operand) = (UnaryType, Operand);

        /// <summary>
        /// Deconstructs the unary expression into its operator, type, and operand.
        /// </summary>
        /// <param name="operator">The <see cref="Identifier"/> representing the unary operator.</param>
        /// <param name="unaryType">The <see cref="UnaryType"/> of the unary operation.</param>
        /// <param name="operand">The <see cref="Expr"/> on which the operation is applied.</param>
        /// <example>
        /// <code>
        /// var unary = new Unary(new Identifier("-"), UnaryType.Arithmetic, new Identifier("x"));
        /// unary.Deconstruct(out Identifier op, out UnaryType type, out Expr operand);
        /// // op.Name is "-", type is UnaryType.Arithmetic, operand is Identifier("x")
        /// </code>
        /// </example>
        public void Deconstruct(out Identifier @operator, out UnaryType unaryType, out Expr operand) =>
            (@operator, unaryType, operand) = (Operator, UnaryType, Operand);

        /// <inheritdoc />
        /// <returns>
        /// The result of type <typeparamref name="T"/> produced by the visitor's <see cref="ExprVisitor{T}.VisitUnary"/> method.
        /// </returns>
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitUnary(this);

        /// <inheritdoc />
        /// <returns>
        /// A string representation of the unary expression, e.g., <c>-x</c> for operators or <c>x:int</c> for conversions.
        /// </returns>
        public override string ToString() => UnaryType switch
        {
            UnaryType.Convert => $"{Operand}:{Operator}",
            _ => $"{Operator}({Operand})"
        };
    }
}
