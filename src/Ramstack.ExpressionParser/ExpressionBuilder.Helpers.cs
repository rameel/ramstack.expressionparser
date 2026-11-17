using System.Linq.Expressions;
using System.Reflection;

using Ramstack.Parsing.Internal;

namespace Ramstack.Parsing;

partial class ExpressionBuilder
{
    private object ResolveSymbol(Expr.Reference expr)
    {
        var expression = TryResolveSymbol(expr);
        if (expression is null)
            Error.CannotResolveSymbol(expr.Value);

        return expression;
    }

    private object? TryResolveSymbol(Expr.Reference expr)
    {
        var name = expr.Value;
        var type = Binder.BindToType(name);

        if (type is not null)
            return type;

        var member = Binder.BindToMember(type: null, memberName: name, isStatic: true);
        if (member is null)
            return null;

        var context = member.IsStatic() ? null : Binder.Context;

        Debug.Assert(context is not null || member.IsStatic());
        Debug.Assert(context is null || !member.IsStatic());

        return Expression.MakeMemberAccess(context, member);
    }

    private static Expression? ApplyImplicitConversion(Expression expression, Type conversionType)
    {
        if (expression.Type == conversionType
            || expression.Type.IsClass && conversionType.IsAssignableFrom(expression.Type))
            return expression;

        if (TypeUtils.CanConvertPrimitive(expression.Type, conversionType))
            return Expression.Convert(expression, conversionType);

        var conversion = TypeUtils.FindConversionOperator(expression.Type, typeof(bool));
        if (conversion is not null)
            return Expression.Convert(expression, conversionType, conversion);

        return null;
    }

    /// <summary>
    /// Converts the branches of a conditional expression to their common type.
    /// </summary>
    /// <remarks>
    /// As in C#, the common type is the type of one of the branches, so it is <see cref="object"/> only
    /// when a branch is of that type, for example after an explicit conversion. The null literal takes the type
    /// of the other branch if that type accepts <see langword="null" />.
    /// </remarks>
    /// <param name="expr">The conditional expression.</param>
    /// <param name="ifTrue">The expression of the true branch.</param>
    /// <param name="ifFalse">The expression of the false branch.</param>
    /// <returns>
    /// The common type of the branches.
    /// </returns>
    private static Type ApplyConditionalConversions(Expr.Conditional expr, ref Expression ifTrue, ref Expression ifFalse)
    {
        var isTrueNull = IsNullLiteral(expr.IfTrue);
        var isFalseNull = IsNullLiteral(expr.IfFalse);

        if (isTrueNull || isFalseNull)
        {
            if (!isFalseNull && CanBeNull(ifFalse.Type))
            {
                ifTrue = Expression.Constant(null, ifFalse.Type);
                return ifFalse.Type;
            }

            if (!isTrueNull && CanBeNull(ifTrue.Type))
            {
                ifFalse = Expression.Constant(null, ifTrue.Type);
                return ifTrue.Type;
            }

            Error.NonApplicableConditionalOperator(
                isTrueNull ? null : ifTrue.Type,
                isFalseNull ? null : ifFalse.Type);
        }

        if (ifTrue.Type == ifFalse.Type)
            return ifTrue.Type;

        var trueToFalse = ApplyBranchConversion(ifTrue, ifFalse.Type);
        var falseToTrue = ApplyBranchConversion(ifFalse, ifTrue.Type);

        if (trueToFalse is not null && falseToTrue is null)
        {
            ifTrue = trueToFalse;
            return ifFalse.Type;
        }

        if (falseToTrue is not null && trueToFalse is null)
        {
            ifFalse = falseToTrue;
            return ifTrue.Type;
        }

        Error.NonApplicableConditionalOperator(ifTrue.Type, ifFalse.Type);
        return null!;

        static bool IsNullLiteral(Expr expr)
        {
            while (expr is Expr.Parenthesized parenthesized)
                expr = parenthesized.Expression;

            return expr is Expr.Literal { Value: null };
        }

        static bool CanBeNull(Type type) =>
            !type.IsValueType || type.IsNullable();
    }

    /// <summary>
    /// Applies an implicit conversion of a branch of a conditional expression to the type of the other branch.
    /// </summary>
    /// <param name="expression">The expression of the branch.</param>
    /// <param name="conversionType">The type of the other branch.</param>
    /// <returns>
    /// The converted expression, or <see langword="null" /> if no implicit conversion exists.
    /// </returns>
    private static Expression? ApplyBranchConversion(Expression expression, Type conversionType)
    {
        //
        // ApplyImplicitConversion does not box values, so handle the boxing conversion here,
        // for example of a number to a branch explicitly converted to object.
        //
        if (expression.Type.IsValueType
            && !conversionType.IsValueType
            && conversionType.IsAssignableFrom(expression.Type))
            return Expression.Convert(expression, conversionType);

        return ApplyImplicitConversion(expression, conversionType);
    }

    private static List<Expression> CreateInjectingArguments(MethodInfo method, IReadOnlyList<Expression> args)
    {
        var parameters = method.GetParameters();
        var list = new List<Expression>();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var parameter = parameters[i];

            if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
            {
                Debug.Assert(parameter.ParameterType.IsSZArray);
                Debug.Assert(parameter.ParameterType.GetElementType() is not null);

                //
                // When the only argument for the parameter array converts to the array type,
                // the call is in its normal form, and the argument is passed as the array itself, as in C#.
                // MethodResolver selects the normal form under the same condition.
                //
                if (i != args.Count - 1 || !parameter.ParameterType.IsAssignableFrom(arg.Type))
                {
                    var paramArray = new List<Expression>();
                    var paramArrayType = parameter.ParameterType.GetElementType()!;

                    for (; i < args.Count; i++)
                        paramArray.Add(Convert(args[i], paramArrayType));

                    list.Add(
                        Expression.NewArrayInit(
                            paramArrayType,
                            paramArray));

                    break;
                }
            }

            list.Add(Convert(arg, parameter.ParameterType));
        }

        return list;

        static Expression Convert(Expression argument, Type conversionType)
        {
            if (argument.Type == conversionType)
                return argument;

            if (argument.Type.IsClass)
                if (conversionType.IsAssignableFrom(argument.Type))
                    return argument;

            return Expression.Convert(argument, conversionType);
        }
    }

    private static Expression ApplyBinaryExpression(Identifier op, Func<Expression, Expression, Expression> apply, Expression lhs, Expression rhs)
    {
        var enumUnderlyingType = lhs.Type.IsEnum
            ? lhs.Type.GetEnumUnderlyingType()
            : rhs.Type.IsEnum
                ? rhs.Type.GetEnumUnderlyingType()
                : null;

        var enumType = lhs.Type.IsEnum
            ? lhs.Type
            : rhs.Type.IsEnum
                ? rhs.Type
                : null;

        if (enumUnderlyingType is not null)
        {
            if (lhs.Type != rhs.Type)
                if (lhs.Type.IsEnum ? enumUnderlyingType != rhs.Type : enumUnderlyingType != lhs.Type)
                    Error.NonApplicableBinaryOperator(op, lhs.Type, rhs.Type);

            if (lhs.Type.IsEnum)
                lhs = Expression.Convert(lhs, enumUnderlyingType);

            if (rhs.Type.IsEnum)
                rhs = Expression.Convert(rhs, enumUnderlyingType);
        }

        if (enumType is null && op.Name is ">>" or ">>>" or "<<")
        {
            if (!TypeUtils.IsInteger(lhs.Type) && TypeUtils.IsInteger(rhs.Type))
                Error.NonApplicableBinaryOperator(op, lhs.Type, rhs.Type);

            if (rhs.Type == typeof(sbyte)
                || rhs.Type == typeof(byte)
                || rhs.Type == typeof(short)
                || rhs.Type == typeof(ushort))
                rhs = Expression.Convert(rhs, typeof(int));

            if (rhs.Type != typeof(int))
                Error.NonApplicableBinaryOperator(op, lhs.Type, rhs.Type);

            if (lhs.Type == typeof(sbyte)
                || lhs.Type == typeof(byte)
                || lhs.Type == typeof(short)
                || lhs.Type == typeof(ushort))
                lhs = Expression.Convert(lhs, typeof(int));

            if (op.Name == ">>>")
            {
                if (lhs.Type == typeof(int))
                    return Expression.Convert(
                        apply(Expression.Convert(lhs, typeof(uint)), rhs),
                        typeof(int));

                if (lhs.Type == typeof(long))
                    return Expression.Convert(
                        apply(Expression.Convert(lhs, typeof(ulong)), rhs),
                        typeof(long));
            }

            return apply(lhs, rhs);
        }

        ApplyBinaryNumericPromotions(op, ref lhs, ref rhs);
        var result = apply(lhs, rhs);

        if (enumType is not null)
        {
            switch (op.Name)
            {
                case "==":
                case "!=":
                case ">":
                case "<":
                case ">=":
                case "<=":
                    return result;

                default:
                    return Expression.Convert(result, enumType);
            }
        }

        return result;
    }

    private static void ApplyBinaryNumericPromotions(Identifier op, ref Expression lhs, ref Expression rhs)
    {
        //
        // 12.4.7.3 Binary numeric promotions
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12473-binary-numeric-promotions
        //
        // Binary numeric promotion implicitly converts both operands to a common type which,
        // in case of the non-relational operators, also becomes the result type of the operation.
        // Binary numeric promotion consists of applying the following rules, in the order they appear here:
        //
        // * If either operand is of type decimal, the other operand is converted to type decimal,
        //   or a binding-time error occurs if the other operand is of type float or double.
        // * Otherwise, if either operand is of type double, the other operand is converted to type double.
        // * Otherwise, if either operand is of type float, the other operand is converted to type float.
        // * Otherwise, if either operand is of type ulong, the other operand is converted to type ulong,
        //   or a binding-time error occurs if the other operand is of type sbyte, short, int, or long.
        // * Otherwise, if either operand is of type long, the other operand is converted to type long.
        // * Otherwise, if either operand is of type uint and the other operand is of type sbyte, short, or int,
        //   both operands are converted to type long.
        // * Otherwise, if either operand is of type uint, the other operand is converted to type uint.
        // * Otherwise, both operands are converted to type int.

        var lhsType = lhs.Type;
        var rhsType = rhs.Type;

        if (!TypeUtils.IsNumeric(lhsType)
            || !TypeUtils.IsNumeric(rhsType))
            return;

        var conversionTypes = new[]
        {
            typeof(decimal),
            typeof(double),
            typeof(float),
            typeof(ulong),
            typeof(long),
            typeof(uint),
            typeof(int)
        };

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < conversionTypes.Length; i++)
        {
            var conversionType = conversionTypes[i];

            if (conversionType != typeof(int))
                if (lhsType != conversionType && rhsType != conversionType)
                    continue;

            if (conversionType == typeof(decimal))
            {
                //
                // If either operand is of type decimal, the other operand is converted to type decimal,
                // or a compile-time error occurs if the other operand is of type float or double.
                //
                if (lhsType == typeof(double)
                    || lhsType == typeof(float)
                    || rhsType == typeof(double)
                    || rhsType == typeof(float))
                    Error.NonApplicableBinaryOperator(op, lhsType, rhsType);
            }
            else if (conversionType == typeof(ulong))
            {
                //
                // If either operand is of type ulong, the other operand is converted to type ulong,
                // or a compile-time error occurs if the other operand is of type sbyte, short, int, or long.
                //
                if (lhsType == typeof(sbyte)
                    || lhsType == typeof(short)
                    || lhsType == typeof(int)
                    || lhsType == typeof(long)
                    || rhsType == typeof(sbyte)
                    || rhsType == typeof(short)
                    || rhsType == typeof(int)
                    || rhsType == typeof(long))
                    Error.NonApplicableBinaryOperator(op, lhsType, rhsType);
            }
            else if (conversionType == typeof(uint))
            {
                //
                // If either operand is of type uint and the other operand is of type sbyte, short, or int,
                // both operands are converted to type long.
                //
                if (lhsType == typeof(sbyte)
                    || lhsType == typeof(short)
                    || lhsType == typeof(int)
                    || rhsType == typeof(sbyte)
                    || rhsType == typeof(short)
                    || rhsType == typeof(int))
                    conversionType = typeof(long);
            }

            if (lhsType != conversionType)
                lhs = Expression.Convert(lhs, conversionType);

            if (rhsType != conversionType)
                rhs = Expression.Convert(rhs, conversionType);

            return;
        }
    }

    private static Func<Expression, Expression, Expression> ResolveBinaryOperatorFactory(string @operator)
    {
        return @operator switch
        {
            "??" => Expression.Coalesce,
            "&&" => Expression.AndAlso,
            "||" => Expression.OrElse,
            "==" => Expression.Equal,
            "!=" => Expression.NotEqual,
            "<=" => Expression.LessThanOrEqual,
            ">=" => Expression.GreaterThanOrEqual,
            ">>" => Expression.RightShift,
            "<<" => Expression.LeftShift,
            ">>>" => Expression.RightShift,
            "<" => Expression.LessThan,
            ">" => Expression.GreaterThan,
            "&" => Expression.And,
            "|" => Expression.Or,
            "^" => Expression.ExclusiveOr,
            "+" => Expression.Add,
            "-" => Expression.Subtract,
            "*" => Expression.Multiply,
            "/" => Expression.Divide,
            "%" => Expression.Modulo,
            _ => throw new NotSupportedException($"Operator '{@operator}' is not supported.")
        };
    }
}
