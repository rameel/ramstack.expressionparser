using System.Linq.Expressions;
using System.Reflection;

using Ramstack.Parsing.Internal;

namespace Ramstack.Parsing;

/// <summary>
/// Represents a visitor that builds an <see cref="Expression" /> from an <see cref="Expr" /> tree.
/// </summary>
/// <param name="binder">The binder that resolves the types, members, and methods referenced in the expression.</param>
public partial class ExpressionBuilder(Binder binder) : ExprVisitor<Expression>
{
    /// <summary>
    /// The overloads of the <c>string.Concat</c> method that the <c>+</c> operator with a string operand is bound to.
    /// </summary>
    private static readonly MethodInfo[] s_concatMethods = typeof(string)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m =>
            m.Name == nameof(string.Concat)
            && (m.CallingConvention & CallingConventions.VarArgs) == 0)
        .ToArray();

    /// <summary>
    /// Gets the binder that resolves the types, members, and methods referenced in the expression.
    /// </summary>
    public Binder Binder { get; } = binder;

    /// <inheritdoc />
    /// <remarks>
    /// Errors in the expression, such as a name that cannot be resolved or an operator that cannot be applied
    /// to its operands, are reported by throwing a <see cref="ParseErrorException" />. The exception is also thrown
    /// if the thread's stack is too small to visit the expression safely.
    /// </remarks>
    public override Expression Visit(Expr expr)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            Error.Throw("The expression is nested too deeply.");

        return base.Visit(expr);
    }

    /// <inheritdoc />
    protected internal override Expression VisitReference(Expr.Reference expr)
    {
        var symbol = ResolveSymbol(expr);
        if (symbol is Expression expression)
            return expression;

        Error.Throw($"'{expr}' is a type, which is not valid in the given context.");
        return null;
    }

    /// <inheritdoc />
    protected internal override Expression VisitLiteral(Expr.Literal expr) =>
        Expression.Constant(expr.Value);

    /// <inheritdoc />
    protected internal override Expression VisitBinary(Expr.Binary expr)
    {
        var lhs = Visit(expr.Left);
        var rhs = Visit(expr.Right);

        var lhsType = lhs.Type;
        var rhsType = rhs.Type;

        ApplyNullLiteralConversion(expr, ref lhs, ref rhs);
        ApplyEnumZeroConversion(expr, ref lhs, ref rhs);

        switch (expr.Operator.Name)
        {
            case "&&" or "||":
                lhs = ApplyBooleanConversion(lhs);
                rhs = ApplyBooleanConversion(rhs);
                break;

            case "??":
                return ApplyNullCoalescing(expr.Operator, lhs, rhs);

            case "+" when lhsType == typeof(string) || rhsType == typeof(string):
                return ApplyStringConcatenation(lhs, rhs);
        }

        // The factory is resolved outside the try block, so that an unknown operator fails with NotSupportedException
        // rather than being reported as an operator that cannot be applied to its operands.
        var factory = ResolveBinaryOperatorFactory(expr.Operator.Name);

        try
        {
            return ApplyBinaryExpression(expr.Operator, factory, lhs, rhs);
        }
        catch (Exception e) when (e is not ParseErrorException)
        {
            Error.NonApplicableBinaryOperator(expr.Operator, lhsType, rhsType);
        }

        return null;
    }

    /// <inheritdoc />
    protected internal override Expression VisitIndex(Expr.Indexer expr)
    {
        var self = Visit(expr.Expression);
        var arguments = Visit(expr.Parameters).ToArray();
        var argumentTypes = GetArgumentTypes(arguments);

        try
        {
            return self.Type.IsArray ? ProcessArray() : ProcessIndex();
        }
        catch (Exception e) when (e is not ParseErrorException)
        {
            Error.Throw(e.Message);
        }

        return null;

        Expression ProcessArray()
        {
            for (var i = 0; i < argumentTypes.Length; i++)
            {
                var argumentType = argumentTypes[i];
                if (argumentType != typeof(int))
                {
                    if (!TypeUtils.CanConvertPrimitive(argumentType, typeof(int)) && TypeUtils.FindConversionOperator(argumentType, typeof(int)) is null)
                        Error.MissingImplicitConversion(argumentType, typeof(int));

                    arguments[i] = Expression.Convert(arguments[i], typeof(int));
                }
            }

            return Expression.ArrayAccess(self, arguments);
        }

        Expression ProcessIndex()
        {
            var indexers = new List<PropertyInfo>();
            var methods = new List<MethodBase>();

            foreach (var pi in self.Type.GetMemberLookupTypes().SelectMany(t => t.GetProperties()))
            {
                var mi = pi.GetMethod;
                if (mi?.GetParameters().Length > 0)
                {
                    indexers.Add(pi);
                    methods.Add(mi);
                }
            }

            if (methods.Count == 0)
                Error.NonIndexingType(self.Type);

            var method = (MethodInfo?)MethodResolver.ResolveMethod(methods, argumentTypes);
            if (method is null)
                Error.AmbiguousMatch(indexers);

            // As in C#, an indexer whose get accessor is not public takes part in overload resolution,
            // but cannot be read.
            if (!method.IsPublic)
                Error.InaccessibleGetAccessor(indexers[methods.IndexOf(method)]);

            var injectingArguments = CreateInjectingArguments(method, arguments);
            return Expression.Call(self, method, injectingArguments);
        }
    }

    /// <inheritdoc />
    protected internal override Expression VisitCall(Expr.Call expr)
    {
        var arguments = Visit(expr.Parameters);
        var argumentTypes = GetArgumentTypes(arguments);

        switch (expr.Expression)
        {
            case Expr.MemberAccess memberAccess:
                return ProcessMemberAccess(memberAccess);

            case Expr.Reference reference:
                return ProcessReference(reference);

            default:
                Error.MethodNameExpected(expr.Expression);
                return null;
        }

        Expression Invoke(Expression? instance, MethodInfo? method)
        {
            Debug.Assert(instance is not null || method is not null);

            if (instance is not null)
            {
                // Without a method, the instance is a delegate to invoke. A method can also be called
                // on a delegate, as in 'func.Invoke(1)' or 'func.ToString()'.
                Debug.Assert(method is not null || typeof(Delegate).IsAssignableFrom(instance.Type));

                if (method is null)
                {
                    const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;
                    method = instance.Type.GetMethod(nameof(Action.Invoke), bindingFlags);
                }
            }

            if (method is null)
                throw new InvalidOperationException(
                    $"Unable to resolve a method: {expr}.");

            var injectingArguments = CreateInjectingArguments(method, arguments);
            return Expression.Call(instance, method, injectingArguments);
        }

        Expression ProcessMemberAccess(Expr.MemberAccess e)
        {
            var (type, instance) = ResolveMemberOwner(e.Expression);
            if (e.Member is not Expr.Reference member)
            {
                Error.MethodNameExpected(e.Member);
                return null;
            }

            var memberInfo = Binder.BindToMember(type, member.Value, isStatic: instance is null);
            if (memberInfo is not null)
            {
                if (!typeof(Delegate).IsAssignableFrom(memberInfo.GetMemberType()))
                    Error.NonInvocableMember(member);

                // A member accessed through a type is static, so it is accessed without an instance.
                return Invoke(CreateMemberAccess(instance, memberInfo), null);
            }

            var methodInfo = Binder.BindToMethod(type, member.Value, argumentTypes, isStatic: instance is null);
            if (methodInfo is null)
                Error.CannotResolveSymbol(member.Value);

            return Invoke(instance, methodInfo);
        }

        Expression ProcessReference(Expr.Reference e)
        {
            switch (TryResolveSymbol(e))
            {
                case Expression expression when typeof(Delegate).IsAssignableFrom(expression.Type):
                    return Invoke(expression, null);

                case Expression or Type:
                    Error.NonInvocableMember(e);
                    break;
            }

            var methodInfo = Binder.BindToMethod(null, e.Value, argumentTypes, isStatic: true);
            if (methodInfo is null)
                Error.CannotResolveSymbol(e.Value);

            var instance = methodInfo.IsStatic ? null : Binder.Context;
            return Invoke(instance, methodInfo);
        }
    }

    /// <inheritdoc />
    protected internal override Expression VisitMemberAccess(Expr.MemberAccess expr)
    {
        var (type, instance) = ResolveMemberOwner(expr.Expression);
        if (expr.Member is not Expr.Reference member)
        {
            Error.IdentifierExpected(expr.Member);
            return null;
        }

        var memberInfo = Binder.BindToMember(type, member.Value, isStatic: instance is null);
        if (memberInfo is null)
            Error.CannotResolveSymbol(member.Value);

        if (instance?.Type.IsSZArray == true
            && memberInfo.DeclaringType == typeof(Array)
            && memberInfo.Name == nameof(Array.Length))
            return Expression.ArrayLength(instance);

        return CreateMemberAccess(instance, memberInfo);
    }

    /// <inheritdoc />
    protected internal override Expression VisitUnary(Expr.Unary expr)
    {
        var operand = Visit(expr.Operand);

        // For an operand of a nullable value type, the operators below are lifted:
        // they apply to the underlying type and return the nullable form of their result type.
        var operandType = operand.Type;
        var nonNullableType = operandType.GetNonNullableType();
        var lifted = operandType.IsNullable();

        // C# does not define the unary '-' and '+' operators for an enum, and '~' applies to its underlying type.
        // A conversion takes the enum itself, so that converting it to object boxes the enum
        // rather than its underlying value.
        if (nonNullableType.IsEnum && expr.UnaryType != UnaryType.Convert)
        {
            if (expr.UnaryType is UnaryType.Negate or UnaryType.UnaryPlus)
                Error.NonApplicableUnaryOperator(expr, operandType);

            operand = ConvertOperand(operand, nonNullableType.GetEnumUnderlyingType(), lifted);
        }

        switch (expr.UnaryType)
        {
            // As in C#, the literals 2147483648 and 9223372036854775808 directly after a unary minus are the constants
            // int.MinValue and long.MinValue. Parentheses or the U suffix keep their uint and ulong types.
            // Other suffixes need no check: 2147483648 with L or UL is not a uint, and C# allows L for 9223372036854775808.
            case UnaryType.Negate when expr.Operand is Expr.Literal { Value: 0x80000000u, HasUnsignedSuffix: false }:
                return Expression.Constant(int.MinValue);

            case UnaryType.Negate when expr.Operand is Expr.Literal { Value: 0x8000000000000000ul, HasUnsignedSuffix: false }:
                return Expression.Constant(long.MinValue);

            // C# has no negation operator for uint, so the operand is converted to long
            // and then negated, which keeps the sign of the result.
            case UnaryType.Negate when operand.Type.GetNonNullableType() == typeof(uint):
                operand = ConvertOperand(operand, typeof(long), lifted);
                break;

            case not UnaryType.Convert:
                operand = ApplyUnaryNumericPromotion(operand);
                break;
        }

        try
        {
            switch (expr.UnaryType)
            {
                case UnaryType.Not:
                    // Expression.Not performs a bitwise complement on integers and falls back to op_OnesComplement
                    // when a type does not define op_LogicalNot, so '!' would silently work as '~'.
                    // Allow only the logical negation, as C# does. For a nullable operand,
                    // Expression.Not lifts the negation of the underlying type.
                    if (nonNullableType == typeof(bool))
                        return Expression.Not(operand);

                    var logicalNot = nonNullableType.GetMethod(
                        "op_LogicalNot",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                        [nonNullableType]);

                    if (logicalNot is null)
                        Error.NonApplicableUnaryOperator(expr, operandType);

                    return Expression.Not(operand, logicalNot);

                case UnaryType.Negate:
                    return Expression.Negate(operand);

                case UnaryType.UnaryPlus:
                    return Expression.UnaryPlus(operand);

                case UnaryType.OnesComplement:
                    operand = Expression.OnesComplement(operand);
                    return nonNullableType.IsEnum
                        ? Expression.Convert(operand, operandType)
                        : operand;

                case UnaryType.Convert:
                    var type = Binder.BindToType(expr.Operator);
                    if (type is null)
                        Error.CannotResolveSymbol(expr.Operator);

                    if (operand.Type == type
                        || operand.Type.IsClass && type.IsAssignableFrom(operand.Type))
                        return operand;

                    return Expression.Convert(operand, type);

                default:
                    var message = $"The unary expression type '{expr.UnaryType}' is not supported.";
                    throw new ArgumentOutOfRangeException(nameof(expr), message);
            }
        }
        catch (Exception e) when (e is not ParseErrorException)
        {
            Error.NonApplicableUnaryOperator(expr, operand.Type);
        }

        return null;
    }

    /// <inheritdoc />
    protected internal override Expression VisitConditional(Expr.Conditional expr)
    {
        var test = Visit(expr.Test);
        var ifTrue = Visit(expr.IfTrue);
        var ifFalse = Visit(expr.IfFalse);

        try
        {
            var condition = ApplyBooleanConversion(test);
            var type = ApplyConditionalConversions(expr, ref ifTrue, ref ifFalse);
            return Expression.Condition(condition, ifTrue, ifFalse, type);
        }
        catch (Exception e) when (e is not ParseErrorException)
        {
            Error.Throw(e.Message);
        }

        return null;
    }

    /// <inheritdoc />
    protected internal override Expression VisitParenthesized(Expr.Parenthesized expr) =>
        Visit(expr.Expression);
}
