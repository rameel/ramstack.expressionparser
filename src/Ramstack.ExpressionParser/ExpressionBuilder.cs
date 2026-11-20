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
    private static readonly MethodBase[] s_concatMethods = typeof(string)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m =>
            m.Name == nameof(string.Concat)
            && (m.CallingConvention & CallingConventions.VarArgs) == 0)
        .Cast<MethodBase>()
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

        //
        // The null literal converts to a nullable value type, so opposite an operand of such a type
        // it takes that type, as in 'x == null' or 'x ?? null'. For an enum type E, only E + U and U + E
        // are defined, so in 'x + null' it takes the nullable form of the underlying type U instead.
        //
        if (lhsType.IsNullable() && IsNullLiteral(expr.Right))
            rhs = Expression.Constant(null, GetNullLiteralType(expr.Operator, lhsType));
        else if (rhsType.IsNullable() && IsNullLiteral(expr.Left))
            lhs = Expression.Constant(null, GetNullLiteralType(expr.Operator, rhsType));

        ApplyEnumZeroConversion(expr, ref lhs, ref rhs);

        var factory = ResolveBinaryOperatorFactory(expr.Operator.Name);

        switch (expr.Operator.Name)
        {
            case "&&":
            case "||":
                lhs = ApplyImplicitConversion(lhs, typeof(bool));
                rhs = ApplyImplicitConversion(rhs, typeof(bool));

                if (lhs is null)
                    Error.MissingImplicitConversion(lhsType, typeof(bool));

                if (rhs is null)
                    Error.MissingImplicitConversion(rhsType, typeof(bool));

                break;

            case "??":
                return ApplyNullCoalescing(expr.Operator, lhs, rhs);

            case "+":
                if (lhsType != typeof(string) && rhsType != typeof(string))
                    break;

                var arguments = new List<Expression>();
                var argumentTypes = new List<Type>();

                Flatten(lhs);
                Flatten(rhs);

                if (argumentTypes.Any(t => t != typeof(string)))
                {
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        argumentTypes[i] = typeof(object);
                        if (arguments[i].Type.IsValueType)
                            arguments[i] = Expression.Convert(arguments[i], typeof(object));
                    }
                }

                var cmi = (MethodInfo?)MethodResolver.ResolveMethod(s_concatMethods, argumentTypes.ToArray())!;
                Debug.Assert(cmi is not null);

                var args = CreateInjectingArguments(cmi, arguments);
                return Expression.Call(null, cmi, args);

                void Flatten(Expression e)
                {
                    if (e is MethodCallExpression call
                        && call.Method.Name == nameof(string.Concat)
                        && call.Method.DeclaringType == typeof(string))
                    {
                        var list = call.Arguments;
                        if (list.Count != 1)
                        {
                            foreach (var item in list)
                            {
                                arguments.Add(item);
                                argumentTypes.Add(item.Type);
                            }
                        }
                        else if (list[0] is NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array)
                        {
                            foreach (var item in array.Expressions)
                            {
                                arguments.Add(item);
                                argumentTypes.Add(item.Type);
                            }
                        }
                        else
                        {
                            //
                            // The only argument is not an array of operands created for the expanded form,
                            // as in string.Concat(value) or string.Concat(array), so the call itself is the operand.
                            //
                            arguments.Add(e);
                            argumentTypes.Add(e.Type);
                        }
                    }
                    else
                    {
                        arguments.Add(e);
                        argumentTypes.Add(e.Type);
                    }
                }
        }

        try
        {
            return ApplyBinaryExpression(expr.Operator, factory, lhs, rhs);
        }
        catch (Exception e) when (e is not ParseErrorException)
        {
            Error.NonApplicableBinaryOperator(expr.Operator, lhsType, rhsType);
        }

        return null!;

        static Type GetNullLiteralType(Identifier op, Type operandType) =>
            op.Name == "+" && operandType.GetNonNullableType() is { IsEnum: true } enumType
                ? enumType.GetEnumUnderlyingType().MakeNullableType()
                : operandType;
    }

    /// <inheritdoc />
    protected internal override Expression VisitIndex(Expr.Indexer expr)
    {
        var self = Visit(expr.Expression);
        var parameters = Visit(expr.Parameters).ToArray();
        var parameterTypes = parameters.Length != 0
            ? new Type[parameters.Length]
            : [];

        for (var i = 0; i < parameterTypes.Length; i++)
            parameterTypes[i] = parameters[i].Type;

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
            for (var i = 0; i < parameterTypes.Length; i++)
            {
                var argumentType = parameterTypes[i];
                if (argumentType != typeof(int))
                {
                    if (!TypeUtils.CanConvertPrimitive(argumentType, typeof(int)) && TypeUtils.FindConversionOperator(argumentType, typeof(int)) is null)
                        Error.MissingImplicitConversion(argumentType, typeof(int));

                    parameters[i] = Expression.Convert(parameters[i], typeof(int));
                }
            }

            return Expression.ArrayAccess(self, parameters);
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

            var method = (MethodInfo?)MethodResolver.ResolveMethod(methods.ToArray(), parameterTypes);
            if (method is null)
                Error.AmbiguousMatch(indexers);

            //
            // As in C#, an indexer whose get accessor is not public takes part in overload resolution,
            // but cannot be read.
            //
            if (!method.IsPublic)
                Error.InaccessibleGetAccessor(indexers[methods.IndexOf(method)]);

            var injectingArguments = CreateInjectingArguments(method, parameters);
            return Expression.Call(self, method, injectingArguments);
        }
    }

    /// <inheritdoc />
    protected internal override Expression VisitCall(Expr.Call expr)
    {
        var arguments = Visit(expr.Parameters);
        var argumentTypes = arguments.Count != 0
            ? new Type[arguments.Count]
            : [];

        for (var i = 0; i < argumentTypes.Length; i++)
            argumentTypes[i] = arguments[i].Type;

        if (expr.Expression.Kind == ExprKind.MemberAccess)
            return ProcessMemberAccess((Expr.MemberAccess)expr.Expression);

        if (expr.Expression.Kind == ExprKind.Reference)
            return ProcessReference((Expr.Reference)expr.Expression);

        Error.MethodNameExpected(expr.Expression);
        return null;

        Expression Invoke(Expression? instance, MethodInfo? method)
        {
            Debug.Assert(instance is not null || method is not null);

            if (instance is not null)
            {
                //
                // Without a method, the instance is a delegate to invoke. A method can also be called
                // on a delegate, as in 'func.Invoke(1)' or 'func.ToString()'.
                //
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
            var instance = e.Expression is Expr.Reference r
                ? ResolveSymbol(r)
                : Visit(e.Expression);

            if (e.Member is Expr.Reference m)
            {
                Debug.Assert(instance is Type or Expression);

                var type = instance as Type;
                var expression = instance as Expression;

                var memberInfo = Binder.BindToMember(type ?? expression?.Type, m.Value, expression is null);
                if (memberInfo is not null)
                {
                    if (!typeof(Delegate).IsAssignableFrom(memberInfo.GetMemberType()))
                        Error.NonInvocableMember(m);

                    //
                    // A member accessed through a type is static, so it is accessed without an instance.
                    //
                    expression = CreateMemberAccess(expression, memberInfo);
                    return Invoke(expression, null);
                }

                var methodInfo = Binder.BindToMethod(type ?? expression?.Type, m.Value, argumentTypes, expression is null);
                if (methodInfo is null)
                    Error.CannotResolveSymbol(m.Value);

                return Invoke(expression, methodInfo);
            }

            Error.MethodNameExpected(e.Member);
            return null;
        }

        Expression ProcessReference(Expr.Reference e)
        {
            var symbol = TryResolveSymbol(e);
            var type = symbol as Type;

            Debug.Assert(symbol is null or Type or Expression);

            if (symbol is Expression expression)
            {
                if (!typeof(Delegate).IsAssignableFrom(expression.Type))
                    Error.NonInvocableMember(e);

                return Invoke(expression, null);
            }

            if (type is not null)
                Error.NonInvocableMember(e);

            var methodInfo = Binder.BindToMethod(null, e.Value, argumentTypes, true);
            if (methodInfo is null)
                Error.CannotResolveSymbol(e.Value);

            var instance = methodInfo.IsStatic ? null : Binder.Context;
            return Invoke(instance, methodInfo);
        }
    }

    /// <inheritdoc />
    protected internal override Expression VisitMemberAccess(Expr.MemberAccess expr)
    {
        var instance = expr.Expression is Expr.Reference r
            ? ResolveSymbol(r)
            : Visit(expr.Expression);

        if (expr.Member is Expr.Reference m)
        {
            var type = instance as Type;
            var expression = instance as Expression;

            Debug.Assert(type is not null || expression is not null);

            var memberInfo = Binder.BindToMember(type ?? expression!.Type, m.Value, expression is null);

            if (memberInfo is null)
                Error.CannotResolveSymbol(m.Value);

            if (expression?.Type.IsSZArray == true && memberInfo.DeclaringType == typeof(Array) && memberInfo.Name == nameof(Array.Length))
                return Expression.ArrayLength(expression);

            return CreateMemberAccess(expression, memberInfo);
        }

        Error.IdentifierExpected(expr.Member);
        return null;
    }

    /// <inheritdoc />
    protected internal override Expression VisitUnary(Expr.Unary expr)
    {
        var operand = Visit(expr.Operand);

        //
        // For an operand of a nullable value type, the operators below are lifted:
        // they apply to the underlying type and return the nullable form of their result type.
        //
        var operandType = operand.Type;
        var nonNullableType = operandType.GetNonNullableType();
        var lifted = operandType.IsNullable();

        //
        // C# does not define the unary '-' and '+' operators for an enum, and '~' applies to its underlying type.
        // A conversion takes the enum itself, so that converting it to object boxes the enum
        // rather than its underlying value.
        //
        if (nonNullableType.IsEnum && expr.UnaryType is UnaryType.Negate or UnaryType.UnaryPlus)
            Error.NonApplicableUnaryOperator(expr, operandType);

        if (nonNullableType.IsEnum && expr.UnaryType != UnaryType.Convert)
            operand = ConvertOperand(operand, nonNullableType.GetEnumUnderlyingType(), lifted);

        switch (expr.UnaryType, operand)
        {
            case (UnaryType.Negate, ConstantExpression { Value: 0x80000000u }):
                return Expression.Constant(int.MinValue);

            case (UnaryType.Negate, ConstantExpression { Value: 0x8000000000000000ul }):
                return Expression.Constant(long.MinValue);

            //
            // C# has no negation operator for uint, so the operand is converted to long
            // and then negated, which keeps the sign of the result.
            //
            case (UnaryType.Negate, _) when operand.Type.GetNonNullableType() == typeof(uint):
                operand = ConvertOperand(operand, typeof(long), lifted);
                break;

            case (not UnaryType.Convert, _):
                operand = ApplyUnaryNumericPromotion(operand);
                break;
        }

        try
        {
            switch (expr.UnaryType)
            {
                case UnaryType.Not:
                    //
                    // Expression.Not performs a bitwise complement on integers and falls back to op_OnesComplement
                    // when a type does not define op_LogicalNot, so '!' would silently work as '~'.
                    // Allow only the logical negation, as C# does. For a nullable operand,
                    // Expression.Not lifts the negation of the underlying type.
                    //
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
            var condition = ApplyImplicitConversion(test, typeof(bool));
            if (condition is null)
                Error.MissingImplicitConversion(test.Type, typeof(bool));

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
