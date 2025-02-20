namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a reference to a variable, parameter, or member in an expression tree.
    /// </summary>
    public sealed class Reference(Identifier value) : Expr(ExprKind.Reference)
    {
        /// <summary>
        /// Gets the identifier representing the referenced entity.
        /// </summary>
        public Identifier Value { get; } = value;

        /// <summary>
        /// Deconstructs the reference expression into its identifier.
        /// </summary>
        /// <param name="value">The <see cref="Identifier"/> representing the referenced entity.</param>
        public void Deconstruct(out Identifier value) =>
            value = Value;

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitReference(this);

        /// <inheritdoc />
        public override string ToString() =>
            Value.ToString();
    }
}
