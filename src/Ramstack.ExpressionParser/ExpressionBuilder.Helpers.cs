using System.Linq.Expressions;
using System.Reflection;

using Ramstack.Parsing.Internal;

namespace Ramstack.Parsing;

partial class ExpressionBuilder
{
    private object ResolveSymbol(Expr.Reference expr)
    {
        var symbol = TryResolveSymbol(expr);
        if (symbol is null)
            Error.CannotResolveSymbol(expr.Value);

        return symbol;
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

        return CreateMemberAccess(context, member);
    }

    /// <summary>
    /// Resolves the expression whose member is accessed, which is either a type name or a value.
    /// </summary>
    /// <param name="expr">The expression to the left of the member access.</param>
    /// <returns>
    /// The type whose member is accessed and the value whose member is accessed,
    /// or <see langword="null" /> as the value if <paramref name="expr" /> is a type name.
    /// </returns>
    private (Type Type, Expression? Instance) ResolveMemberOwner(Expr expr)
    {
        var symbol = expr is Expr.Reference reference
            ? ResolveSymbol(reference)
            : Visit(expr);

        if (symbol is Type type)
            return (type, null);

        var instance = (Expression)symbol;
        return (instance.Type, instance);
    }

    /// <summary>
    /// Creates an expression that reads the specified field or property.
    /// </summary>
    /// <param name="expression">The expression of the instance, or <see langword="null" /> for a static member.</param>
    /// <param name="member">The field or property to read.</param>
    /// <returns>
    /// The <see cref="MemberExpression" /> that reads the member.
    /// </returns>
    private static MemberExpression CreateMemberAccess(Expression? expression, MemberInfo member)
    {
        // As in C#, a property is read through its get accessor, so a property whose get accessor
        // is missing or not public cannot be read, even if its set accessor is public.
        if (member is PropertyInfo property)
        {
            var getter = property.GetMethod;
            if (getter is null)
                Error.MissingGetAccessor(property);

            if (!getter.IsPublic)
                Error.InaccessibleGetAccessor(property);
        }

        return Expression.MakeMemberAccess(expression, member);
    }

    private static Expression? ApplyImplicitConversion(Expression expression, Type conversionType)
    {
        if (expression.Type == conversionType
            || expression.Type.IsClass && conversionType.IsAssignableFrom(expression.Type))
            return expression;

        if (TypeUtils.CanConvertPrimitive(expression.Type, conversionType))
            return Expression.Convert(expression, conversionType);

        //
        // 10.2.6 Implicit nullable conversions
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#1026-implicit-nullable-conversions
        //
        // For each predefined implicit identity or numeric conversion from a non-nullable value type S
        // to a non-nullable value type T, implicit conversions exist from S? to T? and from S to T?.
        //
        if (Nullable.GetUnderlyingType(conversionType) is { } underlyingType)
        {
            var sourceType = expression.Type.GetNonNullableType();
            if (sourceType == underlyingType || TypeUtils.CanConvertPrimitive(sourceType, underlyingType))
                return Expression.Convert(expression, conversionType);
        }

        var conversion = TypeUtils.FindConversionOperator(expression.Type, conversionType);
        if (conversion is not null)
            return Expression.Convert(expression, conversionType, conversion);

        return null;
    }

    /// <summary>
    /// Applies an implicit conversion of the specified expression to <see cref="bool" />, as required for
    /// the operands of <c>&amp;&amp;</c> and <c>||</c> and the condition of <c>?:</c>.
    /// </summary>
    /// <param name="expression">The expression to convert.</param>
    /// <returns>
    /// The converted expression.
    /// </returns>
    private static Expression ApplyBooleanConversion(Expression expression)
    {
        var converted = ApplyImplicitConversion(expression, typeof(bool));
        if (converted is null)
            Error.MissingImplicitConversion(expression.Type, typeof(bool));

        return converted;
    }

    /// <summary>
    /// Converts the branches of a conditional expression to their common type.
    /// </summary>
    /// <param name="expr">The conditional expression.</param>
    /// <param name="ifTrue">The expression of the true branch.</param>
    /// <param name="ifFalse">The expression of the false branch.</param>
    /// <returns>
    /// The common type of the branches.
    /// </returns>
    /// <remarks>
    /// As in C#, the common type is the type of one of the branches, so it is <see cref="object" /> only
    /// when a branch is of that type, for example after an explicit conversion. The null literal takes the type
    /// of the other branch if that type accepts <see langword="null" />. A branch of type <see langword="void" />
    /// is rejected.
    /// </remarks>
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

        // A branch of type void has no value, so the conditional expression would have none either.
        if (ifTrue.Type == typeof(void) || ifFalse.Type == typeof(void))
            Error.NonApplicableConditionalOperator(ifTrue.Type, ifFalse.Type);

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
        return null;

        static bool CanBeNull(Type type) =>
            !type.IsValueType || type.IsNullable();
    }

    /// <summary>
    /// Creates a null-coalescing expression whose operands are converted to its result type.
    /// </summary>
    /// <param name="op">The <see cref="Identifier" /> representing the operator.</param>
    /// <param name="lhs">The expression of the left operand.</param>
    /// <param name="rhs">The expression of the right operand.</param>
    /// <returns>
    /// The null-coalescing expression.
    /// </returns>
    private static BinaryExpression ApplyNullCoalescing(Identifier op, Expression lhs, Expression rhs)
    {
        //
        // 12.15 The null coalescing operator
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1215-the-null-coalescing-operator
        //
        // In order of preference, the type of a ?? b is A₀, A, or B, where A is the type of a, B is the type of b,
        // and A₀ is the underlying type of A if A is a nullable value type, or A otherwise:
        //
        // * If A is a non-nullable value type, a compile-time error occurs.
        // * Otherwise, if A is a nullable value type and an implicit conversion exists from b to A₀,
        //   the result type is A₀.
        // * Otherwise, if an implicit conversion exists from b to A, the result type is A.
        // * Otherwise, if A is a nullable value type and an implicit conversion exists from A₀ to B,
        //   the result type is B.
        //
        var lhsType = lhs.Type;
        var rhsType = rhs.Type;

        if (lhsType.IsValueType && !lhsType.IsNullable())
            Error.NonApplicableBinaryOperator(op, lhsType, rhsType);

        var underlyingType = Nullable.GetUnderlyingType(lhsType);
        if (underlyingType is not null)
        {
            var value = ApplyImplicitConversion(rhs, underlyingType);
            if (value is not null)
                return Expression.Coalesce(lhs, value);
        }

        var converted = ApplyImplicitConversion(rhs, lhsType);
        if (converted is not null)
            return Expression.Coalesce(lhs, converted);

        if (underlyingType is not null)
        {
            // A non-null value of a is unwrapped and converted to B. Convert a to the nullable form of B instead,
            // which keeps its null value. For a reference type B, the conversion is a boxing conversion.
            converted = rhsType.IsValueType
                ? ApplyImplicitConversion(lhs, rhsType.IsNullable() ? rhsType : rhsType.MakeNullableType())
                : rhsType.IsAssignableFrom(underlyingType)
                    ? Expression.Convert(lhs, rhsType)
                    : null;

            if (converted is not null)
                return Expression.Coalesce(converted, rhs);
        }

        Error.NonApplicableBinaryOperator(op, lhsType, rhsType);
        return null;
    }

    /// <summary>
    /// Creates a <c>string.Concat</c> call for the <c>+</c> operator when at least one of its operands is a string.
    /// </summary>
    /// <param name="lhs">The expression of the left operand.</param>
    /// <param name="rhs">The expression of the right operand.</param>
    /// <returns>
    /// The expression that calls <c>string.Concat</c>.
    /// </returns>
    /// <remarks>
    /// A <c>string.Concat</c> call among the operands, such as the one built for <c>a + b</c>, is replaced with its
    /// arguments, so <c>a + b + c</c> becomes a single call. For a call in the expanded form, these are the elements
    /// of its array; a call with a single argument of another kind, such as <c>string.Concat(array)</c>, stays one
    /// operand. If any operand is not a string, all operands are passed as <see cref="object" />.
    /// </remarks>
    private static MethodCallExpression ApplyStringConcatenation(Expression lhs, Expression rhs)
    {
        List<Expression> arguments = [.. GetConcatOperands(lhs), .. GetConcatOperands(rhs)];

        var type = arguments.TrueForAll(a => a.Type == typeof(string)) ? typeof(string) : typeof(object);
        var types = new Type[arguments.Count];
        Array.Fill(types, type);

        var method = (MethodInfo?)MethodResolver.ResolveMethod(s_concatMethods, types);
        Debug.Assert(method is not null);

        return Expression.Call(method, CreateInjectingArguments(method, arguments));

        static IReadOnlyList<Expression> GetConcatOperands(Expression expression)
        {
            if (expression is MethodCallExpression call
                && call.Method.Name == nameof(string.Concat)
                && call.Method.DeclaringType == typeof(string))
            {
                if (call.Arguments.Count != 1)
                    return call.Arguments;

                if (call.Arguments[0] is NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array)
                    return array.Expressions;
            }

            return [expression];
        }
    }

    /// <summary>
    /// Determines whether the specified expression is the null literal, possibly in parentheses.
    /// </summary>
    /// <param name="expr">The expression to test.</param>
    /// <returns>
    /// <see langword="true" /> if the expression is the null literal; otherwise, <see langword="false" />.
    /// </returns>
    private static bool IsNullLiteral(Expr expr) =>
        SkipParentheses(expr) is Expr.Literal { Value: null };

    /// <summary>
    /// Determines whether the specified expression is the integer literal 0, possibly in parentheses.
    /// </summary>
    /// <param name="expr">The expression to test.</param>
    /// <returns>
    /// <see langword="true" /> if the expression is the integer literal 0; otherwise, <see langword="false" />.
    /// </returns>
    private static bool IsZeroLiteral(Expr expr) =>
        SkipParentheses(expr) is Expr.Literal { Value: 0 or 0u or 0L or 0UL };

    /// <summary>
    /// Removes any number of enclosing parentheses from the specified expression.
    /// </summary>
    /// <param name="expr">The expression to unwrap.</param>
    /// <returns>
    /// The expression without enclosing parentheses, or <paramref name="expr" /> if it is not in parentheses.
    /// </returns>
    private static Expr SkipParentheses(Expr expr)
    {
        while (expr is Expr.Parenthesized parenthesized)
            expr = parenthesized.Expression;

        return expr;
    }

    /// <summary>
    /// Converts the null literal opposite an operand of a nullable value type to that type.
    /// For the <c>+</c> operator with an operand of a nullable enum type, the literal is converted
    /// to the nullable form of the underlying type of the enum.
    /// </summary>
    /// <param name="expr">The binary expression.</param>
    /// <param name="lhs">The expression of the left operand.</param>
    /// <param name="rhs">The expression of the right operand.</param>
    private static void ApplyNullLiteralConversion(Expr.Binary expr, ref Expression lhs, ref Expression rhs)
    {
        // The null literal converts to a nullable value type, so opposite an operand of such a type
        // it takes that type, as in 'x == null' or 'x ?? null'. For an enum type E, only E + U and U + E
        // are defined, so in 'x + null' it takes the nullable form of the underlying type U instead.
        if (lhs.Type.IsNullable() && IsNullLiteral(expr.Right))
            rhs = Expression.Constant(null, GetNullLiteralType(expr.Operator, lhs.Type));
        else if (rhs.Type.IsNullable() && IsNullLiteral(expr.Left))
            lhs = Expression.Constant(null, GetNullLiteralType(expr.Operator, rhs.Type));

        static Type GetNullLiteralType(Identifier op, Type operandType) =>
            op.Name == "+" && operandType.GetNonNullableType() is { IsEnum: true } enumType
                ? enumType.GetEnumUnderlyingType().MakeNullableType()
                : operandType;
    }

    /// <summary>
    /// Converts the integer literal 0 opposite an operand of an enum type to that enum type
    /// if the operator is defined for two operands of the enum type. As in the C# compiler, a literal
    /// of the underlying type on the right of <c>-</c> is left unchanged, so <c>x - 0</c> returns
    /// the enum type rather than the underlying type.
    /// </summary>
    /// <param name="expr">The binary expression.</param>
    /// <param name="lhs">The expression of the left operand.</param>
    /// <param name="rhs">The expression of the right operand.</param>
    private static void ApplyEnumZeroConversion(Expr.Binary expr, ref Expression lhs, ref Expression rhs)
    {
        //
        // 10.2.4 Implicit enumeration conversions
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#1024-implicit-enumeration-conversions
        //
        // An implicit enumeration conversion permits a constant expression with any integer type and the value zero
        // to be converted to any enum type and to any nullable value type whose underlying type is an enum type.
        //
        // Of such constant expressions, only the integer literal 0 is recognized.
        //
        if (expr.Operator.Name is not ("-" or "&" or "|" or "^" or "==" or "!=" or "<" or ">" or "<=" or ">="))
            return;

        var lhsType = lhs.Type.GetNonNullableType();
        var rhsType = rhs.Type.GetNonNullableType();

        if (lhsType.IsEnum && IsZeroLiteral(expr.Right))
        {
            // Both E - E and E - U accept 'x - 0'. As in the C# compiler, E - U is selected and returns E
            // if the literal is of the underlying type U; otherwise, E - E is selected and returns U, as in 'x - 0L'.
            if (expr.Operator.Name != "-" || rhsType != lhsType.GetEnumUnderlyingType())
                rhs = Expression.Convert(rhs, lhsType);
        }
        else if (rhsType.IsEnum && IsZeroLiteral(expr.Left))
        {
            // As in the C# compiler, E - E is selected for '0 - x' over U - E, so the result is of the underlying type.
            lhs = Expression.Convert(lhs, rhsType);
        }
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
        // ApplyImplicitConversion does not box values, so handle the boxing conversion here,
        // for example of a number to a branch explicitly converted to object.
        if (expression.Type.IsValueType
            && !conversionType.IsValueType
            && conversionType.IsAssignableFrom(expression.Type))
            return Expression.Convert(expression, conversionType);

        return ApplyImplicitConversion(expression, conversionType);
    }

    /// <summary>
    /// Returns the types of the specified arguments.
    /// </summary>
    /// <param name="arguments">The expressions of the arguments.</param>
    /// <returns>
    /// An array that contains the types of <paramref name="arguments" /> in the same order.
    /// </returns>
    private static Type[] GetArgumentTypes(IReadOnlyList<Expression> arguments)
    {
        if (arguments.Count == 0)
            return [];

        var types = new Type[arguments.Count];
        for (var i = 0; i < types.Length; i++)
            types[i] = arguments[i].Type;

        return types;
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

        // As in C#, a parameter array without arguments gets an empty array,
        // and an omitted optional parameter gets its default value.
        for (var i = list.Count; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
            {
                list.Add(Expression.NewArrayInit(parameter.ParameterType.GetElementType()!));
            }
            else if (parameter.IsOptional)
            {
                list.Add(CreateDefaultArgument(parameter));
            }
            else
            {
                // A required parameter is left without an argument only for a method that does not
                // accept these arguments, so let Expression.Call report the argument count.
                break;
            }
        }

        return list;

        static Expression CreateDefaultArgument(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            var value = parameter.DefaultValue;

            // A parameter declared with [Optional] and without a default value gets Type.Missing
            // if its type is object, and the default value of its type otherwise, as in C#.
            if (value is Missing)
                return type == typeof(object)
                    ? Expression.Constant(value, type)
                    : Expression.Default(type);

            // The default value of a struct, as in 'Guid guid = default', is also stored as null.
            if (value is null)
                return type.IsValueType && !type.IsNullable()
                    ? Expression.Default(type)
                    : Expression.Constant(null, type);

            // The default value of a nullable enum is stored as its underlying integer.
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            if (underlyingType.IsEnum && value.GetType() != underlyingType)
                value = Enum.ToObject(underlyingType, value);

            return Expression.Constant(value, type);
        }

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
        if (op.Name is ">>" or ">>>" or "<<")
            return ApplyShiftOperator(op, apply, lhs, rhs);

        //
        // 12.4.8 Lifted operators
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1248-lifted-operators
        //
        // The operators on non-nullable value types have lifted forms, which take and return the nullable forms
        // of the types. If either operand is of a nullable value type, the operator is selected by the underlying
        // types of the operands, and both operands are converted to the nullable forms of the operator's operand types.
        //
        var lhsType = lhs.Type;
        var rhsType = rhs.Type;

        var lifted = lhsType.IsNullable() || rhsType.IsNullable();
        var lhsNonNullableType = lhsType.GetNonNullableType();
        var rhsNonNullableType = rhsType.GetNonNullableType();

        var enumType = lhsNonNullableType.IsEnum
            ? lhsNonNullableType
            : rhsNonNullableType.IsEnum
                ? rhsNonNullableType
                : null;

        // The type to which the result of an enum operator is converted. It remains null for an enum comparison,
        // whose result is bool, and if neither operand is of an enum type.
        Type? enumResultType = null;

        if (enumType is not null)
        {
            //
            // 12.10.5 Addition operator, 12.10.6 Subtraction operator, 12.12.6 Enumeration comparison operators,
            // and 12.13.3 Enumeration logical operators
            // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12105-addition-operator
            //
            // Every enum type E with the underlying type U implicitly provides the following operators:
            //
            //   E operator +(E x, U y);
            //   E operator +(U x, E y);
            //   U operator -(E x, E y);
            //   E operator -(E x, U y);
            //   E operator &(E x, E y);  and likewise |, ^
            //   bool operator ==(E x, E y);  and likewise !=, <, >, <=, >=
            //
            // Each operator is evaluated on the values converted to U, as in (E)((U)x + (U)y).
            // The C# compiler also accepts U - E and returns E, so it is supported as well.
            //
            var enumUnderlyingType = enumType.GetEnumUnderlyingType();
            var otherType = lhsNonNullableType.IsEnum ? rhsNonNullableType : lhsNonNullableType;

            var isEnumPair = lhsNonNullableType == rhsNonNullableType;
            var isUnderlyingPair = otherType == enumUnderlyingType
                || TypeUtils.CanConvertPrimitive(otherType, enumUnderlyingType);

            switch (op.Name)
            {
                case "+" when isUnderlyingPair:
                case "-" when isUnderlyingPair:
                case "&" or "|" or "^" when isEnumPair:
                    enumResultType = enumType;
                    break;

                case "-" when isEnumPair:
                    enumResultType = enumUnderlyingType;
                    break;

                case "==" or "!=" or "<" or ">" or "<=" or ">=" when isEnumPair:
                    break;

                default:
                    Error.NonApplicableBinaryOperator(op, lhsType, rhsType);
                    break;
            }

            if (lhsNonNullableType.IsEnum)
                lhs = ConvertOperand(lhs, enumUnderlyingType, lifted);

            if (rhsNonNullableType.IsEnum)
                rhs = ConvertOperand(rhs, enumUnderlyingType, lifted);
        }

        ApplyBinaryNumericPromotions(op, ref lhs, ref rhs);

        if (lifted)
        {
            // Expression lifts a user-defined operator, such as DateTime subtraction, only if both operands
            // are of nullable types, so convert the remaining operand of a non-nullable value type.
            if (lhs.Type.IsValueType)
                lhs = ConvertOperand(lhs, lhs.Type.GetNonNullableType(), lifted: true);

            if (rhs.Type.IsValueType)
                rhs = ConvertOperand(rhs, rhs.Type.GetNonNullableType(), lifted: true);
        }

        var result = apply(lhs, rhs);

        return enumResultType is not null
            ? ConvertOperand(result, enumResultType, lifted)
            : result;
    }

    private static Expression ApplyShiftOperator(Identifier op, Func<Expression, Expression, Expression> apply, Expression lhs, Expression rhs)
    {
        // The predefined shift operators take an int, uint, long, or ulong value and an int count,
        // so an operand of a smaller integral type, including char, is converted to int, as in C#.
        var lhsType = lhs.Type;
        var rhsType = rhs.Type;
        var lifted = lhsType.IsNullable() || rhsType.IsNullable();

        lhs = ApplyUnaryNumericPromotion(lhs);
        rhs = ApplyUnaryNumericPromotion(rhs);

        var shiftType = lhs.Type.GetNonNullableType();
        if (!TypeUtils.IsInteger(shiftType) || rhs.Type.GetNonNullableType() != typeof(int))
            Error.NonApplicableBinaryOperator(op, lhsType, rhsType);

        if (op.Name == ">>>")
        {
            if (shiftType == typeof(int))
                return ConvertOperand(
                    apply(ConvertOperand(lhs, typeof(uint), lifted), rhs),
                    typeof(int),
                    lifted);

            if (shiftType == typeof(long))
                return ConvertOperand(
                    apply(ConvertOperand(lhs, typeof(ulong), lifted), rhs),
                    typeof(long),
                    lifted);
        }

        return apply(lhs, rhs);
    }

    /// <summary>
    /// Converts an operand to the specified type, or to the nullable form of the type for a lifted operator.
    /// </summary>
    /// <param name="operand">The expression of the operand.</param>
    /// <param name="type">The non-nullable value type to convert to.</param>
    /// <param name="lifted"><see langword="true" /> to convert to the nullable form of <paramref name="type" />.</param>
    /// <returns>
    /// The converted expression, or <paramref name="operand" /> if it is already of the resulting type.
    /// </returns>
    private static Expression ConvertOperand(Expression operand, Type type, bool lifted)
    {
        if (lifted)
            type = type.MakeNullableType();

        return operand.Type != type
            ? Expression.Convert(operand, type)
            : operand;
    }

    private static Expression ApplyUnaryNumericPromotion(Expression operand)
    {
        //
        // 12.4.7.2 Unary numeric promotions
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12472-unary-numeric-promotions
        //
        // Unary numeric promotion consists of converting operands of type sbyte, byte, short, ushort, or char to type int.
        // For a lifted operator, an operand of the nullable form of these types is converted to int?.
        //
        var type = operand.Type.GetNonNullableType();

        if (type == typeof(sbyte)
            || type == typeof(byte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(char))
            return ConvertOperand(operand, typeof(int), operand.Type.IsNullable());

        return operand;
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
        //
        // For a lifted operator, the rules apply to the underlying types, and both operands are converted
        // to the nullable form of the resulting type.

        var lhsType = lhs.Type.GetNonNullableType();
        var rhsType = rhs.Type.GetNonNullableType();

        if (!TypeUtils.IsNumeric(lhsType) || !TypeUtils.IsNumeric(rhsType))
            return;

        var type = GetPromotedType(lhsType, rhsType);
        if (type is null)
            Error.NonApplicableBinaryOperator(op, lhs.Type, rhs.Type);

        var lifted = lhs.Type.IsNullable() || rhs.Type.IsNullable();

        lhs = ConvertOperand(lhs, type, lifted);
        rhs = ConvertOperand(rhs, type, lifted);

        static Type? GetPromotedType(Type x, Type y)
        {
            if (x == typeof(decimal) || y == typeof(decimal))
                return IsFloatingPoint(x) || IsFloatingPoint(y) ? null : typeof(decimal);

            if (x == typeof(double) || y == typeof(double))
                return typeof(double);

            if (x == typeof(float) || y == typeof(float))
                return typeof(float);

            if (x == typeof(ulong) || y == typeof(ulong))
                return IsSignedInteger(x) || IsSignedInteger(y) ? null : typeof(ulong);

            if (x == typeof(long) || y == typeof(long))
                return typeof(long);

            // Neither operand is of type long here, so IsSignedInteger matches only sbyte, short, or int,
            // as the uint rule requires.
            if (x == typeof(uint) || y == typeof(uint))
                return IsSignedInteger(x) || IsSignedInteger(y) ? typeof(long) : typeof(uint);

            return typeof(int);
        }

        static bool IsFloatingPoint(Type type) =>
            type == typeof(float) || type == typeof(double);

        static bool IsSignedInteger(Type type) =>
            type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long);
    }

    private static Func<Expression, Expression, Expression> ResolveBinaryOperatorFactory(string @operator)
    {
        return @operator switch
        {
            "*" => Expression.Multiply,
            "/" => Expression.Divide,
            "%" => Expression.Modulo,
            "+" => Expression.Add,
            "-" => Expression.Subtract,
            "<<" => Expression.LeftShift,
            ">>" => Expression.RightShift,
            ">>>" => Expression.RightShift,
            "<" => Expression.LessThan,
            ">" => Expression.GreaterThan,
            "<=" => Expression.LessThanOrEqual,
            ">=" => Expression.GreaterThanOrEqual,
            "==" => Expression.Equal,
            "!=" => Expression.NotEqual,
            "&" => Expression.And,
            "^" => Expression.ExclusiveOr,
            "|" => Expression.Or,
            "&&" => Expression.AndAlso,
            "||" => Expression.OrElse,
            _ => throw new NotSupportedException($"Operator '{@operator}' is not supported.")
        };
    }
}
