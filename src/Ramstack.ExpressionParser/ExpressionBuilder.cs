using System.Linq.Expressions;
using System.Reflection;

using Ramstack.Parsing.Internal;

namespace Ramstack.Parsing;

/// <summary>
/// Builds the <see cref="Expression"/> from the <see cref="Expr"/>.
/// </summary>
/// <param name="binder">The binder instance.</param>
public partial class ExpressionBuilder(Binder binder) : ExprVisitor<Expression>
{
    /// <summary>
    /// Gets the binder instance.
    /// </summary>
    public Binder Binder { get; } = binder;

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
                if (lhsType.IsValueType && !lhsType.IsNullable())
                    Error.NonApplicableBinaryOperator(expr.Operator, lhsType, rhsType);

                rhs = ApplyImplicitConversion(rhs, lhsType);
                if (rhs is null)
                    Error.NonApplicableBinaryOperator(expr.Operator, lhsType, rhsType);

                break;

            case "+":
                if (lhsType != typeof(string) && rhsType != typeof(string))
                    break;

                var arguments = new List<Expression>();
                var argumentTypes = new List<Type>();

                Flatten(lhs);
                Flatten(rhs);

                var methods = typeof(string)
                    .GetMethods(
                        BindingFlags.Public | BindingFlags.Static)
                    .Where(m =>
                        m.Name == nameof(string.Concat)
                        && (m.CallingConvention & CallingConventions.VarArgs) == 0)
                    .Cast<MethodBase>()
                    .ToArray();

                if (argumentTypes.Any(t => t != typeof(string)))
                {
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        argumentTypes[i] = typeof(object);
                        if (arguments[i].Type.IsValueType)
                            arguments[i] = Expression.Convert(arguments[i], typeof(object));
                    }
                }

                var cmi = (MethodInfo?)MethodResolver.ResolveMethod(methods, argumentTypes.ToArray())!;
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
                        else
                        {
                            var array = (NewArrayExpression)list[0];
                            foreach (var item in array.Expressions)
                            {
                                arguments.Add(item);
                                argumentTypes.Add(item.Type);
                            }
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
            var methods = new List<MethodBase>();

            foreach (var pi in self.Type.GetProperties())
            {
                var mi = pi.GetMethod;
                if (mi?.GetParameters().Length != 0)
                    methods.Add(mi!);
            }

            if (methods.Count == 0)
                Error.NonIndexingType(self.Type);

            var method = (MethodInfo?)MethodResolver.ResolveMethod(methods.ToArray(), parameterTypes);
            if (method is null)
            {
                var list = self.Type.GetProperties().Where(p => p.GetMethod?.GetParameters().Length != 0);
                Error.AmbiguousMatch(list);
            }

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
                Debug.Assert(method is null && typeof(Delegate).IsAssignableFrom(instance.Type)
                    || method is not null && !typeof(Delegate).IsAssignableFrom(instance.Type));

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
                    // It's not supposed to happen
                    //
                    if (expression is null)
                        throw new InvalidOperationException(
                            "Cannot access a member on a null reference.");

                    expression = Expression.MakeMemberAccess(expression, memberInfo);
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

            if (memberInfo.DeclaringType == typeof(Array) && memberInfo.Name == nameof(Array.Length))
            {
                Debug.Assert(expression is not null);
                return Expression.ArrayLength(expression);
            }

            return Expression.MakeMemberAccess(expression, memberInfo);
        }

        Error.IdentifierExpected(expr.Member);
        return null;
    }

    /// <inheritdoc />
    protected internal override Expression VisitUnary(Expr.Unary expr)
    {
        var operand = Visit(expr.Operand);

        var operandType = operand.Type;
        var underlyingType = operandType.IsEnum
            ? operandType.GetEnumUnderlyingType()
            : operandType;

        if (operandType.IsEnum)
            operand = Expression.Convert(operand, underlyingType);

        switch (expr.UnaryType, operand)
        {
            case (UnaryType.Negate, ConstantExpression { Value: 0x80000000u }):
                return Expression.Constant(int.MinValue);

            case (UnaryType.Negate, ConstantExpression { Value: 0x8000000000000000ul }):
                return Expression.Constant(long.MinValue);

            case (UnaryType.Negate, _) when operand.Type == typeof(uint):
                return Expression.Convert(operand, typeof(long));

            case (not UnaryType.Convert, _) when underlyingType == typeof(byte):
            case (not UnaryType.Convert, _) when underlyingType == typeof(sbyte):
            case (not UnaryType.Convert, _) when underlyingType == typeof(short):
            case (not UnaryType.Convert, _) when underlyingType == typeof(ushort):
                operand = Expression.Convert(operand, typeof(int));
                break;
        }

        try
        {
            switch (expr.UnaryType)
            {
                case UnaryType.Not:
                    return Expression.Not(operand);

                case UnaryType.Negate:
                    return Expression.Negate(operand);

                case UnaryType.UnaryPlus:
                    return Expression.UnaryPlus(operand);

                case UnaryType.OnesComplement:
                    operand = Expression.OnesComplement(operand);
                    return operandType.IsEnum
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

            return Expression.Condition(condition, ifTrue, ifFalse);
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
