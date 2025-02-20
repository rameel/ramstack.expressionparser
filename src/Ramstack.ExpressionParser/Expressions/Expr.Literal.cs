namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a literal value in an expression tree, such as a number, string, or boolean.
    /// </summary>
    public sealed class Literal(object? value) : Expr(ExprKind.Literal)
    {
        /// <summary>
        /// Gets the constant value of the literal expression.
        /// </summary>
        public object? Value { get; } = value;

        /// <summary>
        /// Deconstructs the literal expression into its value.
        /// </summary>
        /// <param name="value">The constant value of the literal, which may be <c>null</c>.</param>
        public void Deconstruct(out object? value) =>
            value = Value;

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitLiteral(this);

        /// <inheritdoc />
        public override string ToString() => Value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            _ => Value.ToString()!
        };
    }
}
