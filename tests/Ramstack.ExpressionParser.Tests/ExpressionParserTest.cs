using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using Ramstack.Parsing.Expressions;

namespace Ramstack.Parsing;

[TestFixture]
public class ExpressionParserTest
{
    private static List<object[]> ExpressionSource =>
        GetResource("Expressions.txt");

    private static List<object[]> StringConcatSource =>
        GetResource("StringConcat.txt");

    private static List<object[]> ErrorsSource =>
        GetResource("Errors.txt");

    private static List<object[]> EvaluatesSource
    {
        get
        {
            var list = new List<object[]>();
            list.Add(["""version.parse("1.2.3.4")""", Version.Parse("1.2.3.4")]);
            list.Add(["""Math.Max(1d, 15)""", 15d]);
            list.Add(["""sbyte.MinValue""", sbyte.MinValue]);
            list.Add(["""-0x80000000""", int.MinValue]);
            list.Add([""" 0x80000000""", 0x80000000]);
            list.Add(["""-0x8000000000000000""", long.MinValue]);
            list.Add([""" 0x8000000000000000""", 0x8000000000000000L]);
            list.Add(["""ulong.MaxValue""", ulong.MaxValue]);
            list.Add(["""Guid.Empty""", Guid.Empty]);
            list.Add(["""1 :> DayOfWeek :> object""", DayOfWeek.Monday]);
            list.Add(["""(DayOfWeek.Friday :> object).ToString()""", "Friday"]);
            list.Add(["""-(DayOfWeek.Friday :> int)""", -5]);
            return list;
        }
    }

    private static IEnumerable<TestCaseData> DeepExpressionSource
    {
        get
        {
            const int Count = 200_000;

            yield return Case("prefix operators", new string('-', Count) + "1");
            yield return Case("casts", "1" + Repeat(":>int", Count));
            yield return Case("binary operators", string.Join(" + ", Enumerable.Repeat("1", Count)));
            yield return Case("null-coalescing operators", string.Join(" ?? ", Enumerable.Repeat("\"a\"", Count)));
            yield return Case("member access", "\"a\"" + Repeat(".Length.ToString()", Count));
            yield return Case("indexers", "\"a\"" + Repeat("[0].ToString()", Count));
            yield return Case("calls", "\"a\"" + Repeat("()", Count));
            yield return Case("calls on a member", "\"a\".ToString" + Repeat("()", Count));

            static TestCaseData Case(string name, string expression) =>
                new TestCaseData(expression).SetArgDisplayNames(name);

            static string Repeat(string value, int count) =>
                string.Concat(Enumerable.Repeat(value, count));
        }
    }

    private static IEnumerable<TestCaseData> DeepNestingSource
    {
        get
        {
            const int Count = 200_000;

            foreach (var stackSize in new[] { 256 * 1024, 1024 * 1024 })
            {
                yield return Case("parentheses", new string('(', Count) + "1" + new string(')', Count), stackSize);
                yield return Case("arguments", Repeat("f(", Count) + "1" + Repeat(")", Count), stackSize);
                yield return Case("indexers", Repeat("x[", Count) + "1" + Repeat("]", Count), stackSize);
                yield return Case("conditional true branches", Repeat("1 > 0 ? ", Count) + "1" + Repeat(" : 1", Count), stackSize);
                yield return Case("conditional false branches", Repeat("1 > 0 ? 1 : ", Count) + "1", stackSize);
            }

            static TestCaseData Case(string name, string expression, int stackSize) =>
                new TestCaseData(expression, stackSize).SetArgDisplayNames(name, $"{stackSize / 1024} KB");

            static string Repeat(string value, int count) =>
                string.Concat(Enumerable.Repeat(value, count));
        }
    }

    private static IEnumerable<TestCaseData> StackBudgetSource
    {
        get
        {
            yield return Case("prefix operators", new string('-', 255) + "1");
            yield return Case("casts", "1" + Repeat(":>int", 255));
            yield return Case("binary operators", string.Join(" + ", Enumerable.Repeat("1", 256)));
            yield return Case("null-coalescing operators", string.Join(" ?? ", Enumerable.Repeat("\"a\"", 256)));
            yield return Case("indexers and calls", "\"abc\"" + Repeat("[0].ToString()", 85));
            yield return Case("conditional operators", Repeat("1 > 0 ? 1 : ", 254) + "1");
            yield return Case("parentheses", new string('(', 150) + "1" + new string(')', 150));

            static TestCaseData Case(string name, string expression) =>
                new TestCaseData(expression).SetArgDisplayNames(name);

            static string Repeat(string value, int count) =>
                string.Concat(Enumerable.Repeat(value, count));
        }
    }

    private static IEnumerable<TestCaseData> LiftedBinaryOperatorSource
    {
        get
        {
            yield return Case("NullableInt + 1", typeof(int?), 6);
            yield return Case("NullInt + 1", typeof(int?), null);
            yield return Case("NullableInt + 1L", typeof(long?), 6L);
            yield return Case("NullableInt + Big", typeof(long?), 15L);
            yield return Case("NullableInt * 2.5", typeof(double?), 12.5);
            yield return Case("NullableInt / 2", typeof(int?), 2);
            yield return Case("NullableInt % 2", typeof(int?), 1);
            yield return Case("NullableInt - NullableLong", typeof(long?), -2L);
            yield return Case("NullableByte + NullableByte", typeof(int?), 6);
            yield return Case("NullableChar + 1", typeof(int?), 98);
            yield return Case("NullableDecimal + 1", typeof(decimal?), 3.5m);
            yield return Case("NullableUInt + NullableInt", typeof(long?), 9L);
            yield return Case("NullableULong + 1u", typeof(ulong?), 9UL);
            yield return Case("NullInt + null", typeof(int?), null);

            yield return Case("NullableInt & 1", typeof(int?), 1);
            yield return Case("NullableInt | NullableLong", typeof(long?), 7L);
            yield return Case("NullableInt ^ 1", typeof(int?), 4);
            yield return Case("NullableTrue & true", typeof(bool?), true);
            yield return Case("NullBool & false", typeof(bool?), false);
            yield return Case("NullBool | true", typeof(bool?), true);
            yield return Case("NullBool ^ true", typeof(bool?), null);

            yield return Case("NullableInt << 1", typeof(int?), 10);
            yield return Case("NullableByte << 1", typeof(int?), 6);
            yield return Case("1 << NullableInt", typeof(int?), 32);
            yield return Case("NullableLong << NullableInt", typeof(long?), 224L);
            yield return Case("-NullableInt >>> 28", typeof(int?), 15);
            yield return Case("-NullableLong >>> 60", typeof(long?), 15L);
            yield return Case("NullInt << 1", typeof(int?), null);

            yield return Case("NullableDay | DayOfWeek.Monday", typeof(DayOfWeek?), DayOfWeek.Friday);
            yield return Case("NullableDay & Day", typeof(DayOfWeek?), DayOfWeek.Monday);
            yield return Case("NullableDay + 1", typeof(DayOfWeek?), DayOfWeek.Saturday);
            yield return Case("NullDay | Day", typeof(DayOfWeek?), null);

            yield return Case("NullableDate - Date", typeof(TimeSpan?), TimeSpan.FromDays(1));
            yield return Case("Date + NullableSpan", typeof(DateTime?), new DateTime(2020, 1, 1, 1, 0, 0));

            static TestCaseData Case(string expression, Type type, object? expected) =>
                new(expression, type, expected);
        }
    }

    [OneTimeSetUp]
    public void Setup()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Test]
    public void ContextTest()
    {
        var parameter = Expression.Parameter(typeof(UserInfo), "u");
        var binder = new ContextAwareBinder(parameter);

        var result = ExpressionParser.Parse("name != lastName && name.Length > 0", binder);
        Assert.That(result.ToString(), Is.EqualTo("((u.Name != u.LastName) AndAlso (u.Name.Length > 0))"));
    }

    [TestCaseSource(nameof(EvaluatesSource))]
    public void EvaluateExpression(string expression, object result) =>
        Assert.That(ExpressionParser.Evaluate(expression), Is.EqualTo(result));

    [Test]
    public void Evaluate_DateTimeToday_ReturnsCurrentDate()
    {
        //
        // The date can change during evaluation, so the result is compared with the dates before and after it.
        //
        var before = DateTime.Today;
        var result = ExpressionParser.Evaluate("DateTime.Today");
        var after = DateTime.Today;

        Assert.That(result, Is.EqualTo(before).Or.EqualTo(after));
    }

    [Test]
    public void Evaluate_ContextOfDerivedType_BindsMembersOfDeclaredType()
    {
        var context = new ValueContext();

        Assert.That(ExpressionParser.Evaluate<IValueContext, int>("Value", context), Is.EqualTo(1));
        Assert.That(ExpressionParser.Evaluate<object, string>("ToString()", context), Is.EqualTo(context.ToString()));
    }

    [Test]
    public void Evaluate_MemberOfRuntimeContextTypeOnly_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ExpressionParser.Evaluate<IValueContext, int>("Extra", new ValueContext()));

        Assert.That(exception!.Message, Is.EqualTo("Cannot resolve symbol 'Extra'."));
    }

    [TestCaseSource(nameof(InconvertibleResultSource))]
    public void Evaluate_ResultNotConvertibleToResultType_ThrowsArgumentException(Func<object?> evaluate, string expected)
    {
        var exception = Assert.Throws<ArgumentException>(() => evaluate());

        Assert.That(exception!.Message, Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> InconvertibleResultSource
    {
        get
        {
            const string VoidCall = "TimeZoneInfo.ClearCachedData()";
            var context = new ValueContext();

            yield return Case("Evaluate, void",
                () => ExpressionParser.Evaluate(VoidCall),
                "Cannot convert type 'System.Void' to 'System.Object'.");

            yield return Case("Evaluate with context, void",
                () => ExpressionParser.Evaluate(VoidCall, context),
                "Cannot convert type 'System.Void' to 'System.Object'.");

            yield return Case("Evaluate<TResult>, void",
                () => ExpressionParser.Evaluate<int>(VoidCall),
                "Cannot convert type 'System.Void' to 'System.Int32'.");

            yield return Case("Evaluate<TResult>, no conversion",
                () => ExpressionParser.Evaluate<string>("1"),
                "Cannot convert type 'System.Int32' to 'System.String'.");

            yield return Case("Evaluate<TContext, TResult>, void",
                () => ExpressionParser.Evaluate<IValueContext, object>(VoidCall, context),
                "Cannot convert type 'System.Void' to 'System.Object'.");

            yield return Case("Evaluate<TContext, TResult>, no conversion",
                () => ExpressionParser.Evaluate<IValueContext, string>("Value", context),
                "Cannot convert type 'System.Int32' to 'System.String'.");

            static TestCaseData Case(string name, Func<object?> evaluate, string expected) =>
                new TestCaseData(evaluate, expected).SetArgDisplayNames(name);
        }
    }

    [TestCase("-1u", -1L)]
    [TestCase("-4294967295u", -4294967295L)]
    [TestCase("-3000000000", -3000000000L)]
    [TestCase("-(5 :> uint)", -5L)]
    public void Evaluate_NegatedUInt32_ReturnsNegatedInt64(string expression, long expected) =>
        Assert.That(ExpressionParser.Evaluate(expression), Is.TypeOf<long>().And.EqualTo(expected));

    [TestCase("-'a'", -97)]
    [TestCase("+'a'", 97)]
    [TestCase("~'a'", -98)]
    [TestCase("'a' << 1", 194)]
    [TestCase("'a' >> 1", 48)]
    [TestCase("'a' >>> 1", 48)]
    [TestCase("1 << 'a'", 2)]
    public void Evaluate_CharOperand_PromotesToInt32(string expression, int expected) =>
        Assert.That(ExpressionParser.Evaluate(expression), Is.TypeOf<int>().And.EqualTo(expected));

    [TestCase("Sum(Numbers)", 6)]
    [TestCase("Count(Words)", 2)]
    [TestCase("IsWords(Words)", true)]
    [TestCase("""string.Join(",", Words)""", "a,b")]
    [TestCase("string.Concat(Objects)", "x1")]
    [TestCase("""string.Concat(Objects) + "!" """, "x1!")]
    public void Evaluate_ArrayForParamArray_PassesArray(string expression, object expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new ParamArrayContext()), Is.EqualTo(expected));

    [TestCase("Sum(1, 2, 3)", 6)]
    [TestCase("Sum(5)", 5)]
    [TestCase("Count(Numbers)", 1)]
    [TestCase("Count(Words, Words)", 2)]
    [TestCase("""string.Join(",", "a", "b")""", "a,b")]
    [TestCase("Sum()", 0)]
    [TestCase("Count()", 0)]
    public void Evaluate_ElementsForParamArray_CreatesArray(string expression, object expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new ParamArrayContext()), Is.EqualTo(expected));

    [TestCase("Doubler(2)", 4)]
    [TestCase("Doubler.Invoke(2)", 4)]
    [TestCase("Doubler.ToString()", "System.Func`2[System.Int32,System.Int32]")]
    public void Evaluate_DelegateMember_CallsDelegateAndItsMethods(string expression, object expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new DelegateContext()), Is.EqualTo(expected));

    [TestCase("StaticDelegates.Tripler(2)")]
    [TestCase("StaticDelegates.Tripler.Invoke(2)")]
    public void Evaluate_StaticDelegateMemberOfType_CallsDelegate(string expression)
    {
        var binder = new DefaultBinder();
        binder.RegisterType(typeof(StaticDelegates));

        var result = ExpressionParser.Parse(expression, binder);
        Assert.That(result.Success, Is.True, result.ErrorMessage);

        var value = Expression.Lambda<Func<int>>(result.Value!).Compile()();
        Assert.That(value, Is.EqualTo(6));
    }

    [TestCase("Missing ?? StringOnly", "string")]
    [TestCase("Missing ?? StringAndBool", "string")]
    [TestCase("""Flag ? StringOnly : "x" """, "string")]
    [TestCase("StringAndBool && Flag", true)]
    public void Evaluate_UserDefinedImplicitConversion_UsesOperatorForTargetType(string expression, object expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new ConversionContext()), Is.EqualTo(expected));

    [Test]
    public void Parse_NoUserDefinedImplicitConversionToTargetType_ReturnsError()
    {
        var parameter = Expression.Parameter(typeof(ConversionContext), "c");
        var result = ExpressionParser.Parse("Missing ?? BoolOnly", new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(
            $"Operator '??' cannot be applied to operands of type 'System.String' and '{typeof(ConvertibleToBool)}'."));
    }

    [TestCase("Defaults()", "1 Monday True null")]
    [TestCase("""Defaults(5, DayOfWeek.Friday, Guid.NewGuid(), "a")""", "5 Friday False a")]
    [TestCase("Mixed(1)", "1 2 0")]
    [TestCase("Mixed(1, 3)", "1 3 0")]
    [TestCase("Mixed(1, 3, 4, 5)", "1 3 2")]
    [TestCase("WithMissing()", "Missing 0")]
    [TestCase("""string.Format("x")""", "x")]
    [TestCase(""" "a,b".Split(",").Length """, 2)]
    [TestCase(""" "a,b".Split(',').Length """, 2)]
    public void Evaluate_OmittedOptionalArguments_UsesDefaultValues(string expression, object expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new OptionalContext()), Is.EqualTo(expected));

    [TestCase("Numbers.Length", 3)]
    [TestCase("Matrix.Length", 6)]
    [TestCase("Cube.Length", 8)]
    [TestCase("Jagged.Length", 2)]
    [TestCase("Jagged[1].Length", 3)]
    [TestCase("Untyped.Length", 2)]
    public void Evaluate_ArrayLength_ReturnsNumberOfElements(string expression, int expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new ArrayContext()), Is.EqualTo(expected));

    [TestCase("!Flag", false)]
    [TestCase("!NullableFlag", true)]
    [TestCase("!WithLogicalNot", true)]
    [TestCase("!WithInheritedLogicalNot", true)]
    [TestCase("!NullableWithLogicalNot", true)]
    public void LogicalNotTest(string expression, bool expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new LogicalNotContext()), Is.EqualTo(expected));

    [Test]
    public void LogicalNotOnOnesComplementOnlyTest()
    {
        var parameter = Expression.Parameter(typeof(LogicalNotContext), "c");
        var result = ExpressionParser.Parse("!WithOnesComplement", new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(
            $"Unary operator '!' cannot be applied to operand of type '{typeof(OnesComplementOperand)}'."));
    }

    [TestCase("Pick(97 :> byte)", "int")]
    [TestCase("Pick('a')", "char")]
    public void Evaluate_ByteArgument_DoesNotSelectCharOverload(string expression, string expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new OverloadContext()), Is.EqualTo(expected));

    [TestCase("WithDefault(1)", "(int)")]
    [TestCase("WithDefault(1, 2)", "(int, int = 0)")]
    [TestCase("ParamsOrDefault(1)", "(int, int = 0)")]
    [TestCase("Expanded(1, 2, 3)", "(int, int, params int[])")]
    [TestCase("ExpandedOrDefault(1)", "(int, params int[])")]
    public void Evaluate_OmittedOptionalArguments_SelectsOverloadAsCSharp(string expression, string expected) =>
        Assert.That(ExpressionParser.Evaluate(expression, new OverloadContext()), Is.EqualTo(expected));

    [TestCase("BothDefaults(1)")]
    [TestCase("ExpandedBothDefaults(1)")]
    public void Parse_OverloadsWithDefaultArguments_ReturnsAmbiguityError(string expression)
    {
        var parameter = Expression.Parameter(typeof(OverloadContext), "c");
        var result = ExpressionParser.Parse(expression, new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.StartWith("Ambiguous match found"));
    }

    [Test]
    public void Parse_ByteArgumentForCharParameter_ReturnsError()
    {
        var parameter = Expression.Parameter(typeof(OverloadContext), "c");
        var result = ExpressionParser.Parse("OnlyChar(97 :> byte)", new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Evaluate_TypeWithWriteOnlyProperties_UsesReadableIndexer() =>
        Assert.That(ExpressionParser.Evaluate("Mixed[1]", new IndexerContext()), Is.EqualTo("item 1"));

    [Test]
    public void Parse_TypeWithWriteOnlyIndexer_ReturnsNonIndexingError()
    {
        var parameter = Expression.Parameter(typeof(IndexerContext), "c");
        var result = ExpressionParser.Parse("WriteOnly[1]", new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(
            $"Cannot apply indexing with [] to an expression of type '{typeof(WriteOnlyIndexer)}'."));
    }

    [TestCase("PrivateGetter", "AccessorContext.PrivateGetter")]
    [TestCase("ProtectedGetter", "AccessorContext.ProtectedGetter")]
    [TestCase("InternalGetter", "AccessorContext.InternalGetter")]
    [TestCase("Self.PrivateGetter", "AccessorContext.PrivateGetter")]
    [TestCase("PrivateGetter.Length", "AccessorContext.PrivateGetter")]
    [TestCase("StaticPrivateGetter", "AccessorContext.StaticPrivateGetter")]
    [TestCase("AccessorContext.StaticPrivateGetter", "AccessorContext.StaticPrivateGetter")]
    [TestCase("PrivateDelegate(1)", "AccessorContext.PrivateDelegate")]
    [TestCase("PrivateIndexer[1]", "PrivateGetterIndexer.this[System.Int32]")]
    [TestCase("Overloaded[1]", "OverloadedIndexer.this[System.Int32]")]
    public void Parse_NonPublicGetAccessor_ReturnsError(string expression, string property)
    {
        var parameter = Expression.Parameter(typeof(AccessorContext), "c");
        var result = ExpressionParser.Parse(expression, new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(
            $"The property or indexer '{typeof(ExpressionParserTest)}+{property}' "
            + "cannot be used in this context because the get accessor is inaccessible."));
    }

    [TestCase("WriteOnly")]
    [TestCase("WriteOnly.Length")]
    [TestCase("Self.WriteOnly")]
    public void Parse_PropertyWithoutGetAccessor_ReturnsError(string expression)
    {
        var parameter = Expression.Parameter(typeof(AccessorContext), "c");
        var result = ExpressionParser.Parse(expression, new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(
            $"The property or indexer '{typeof(AccessorContext)}.WriteOnly' cannot be used in this context because it lacks the get accessor."));
    }

    [Test]
    public void Evaluate_IndexerWithPublicGetAccessor_ReturnsValue() =>
        Assert.That(ExpressionParser.Evaluate("Overloaded[1L]", new AccessorContext()), Is.EqualTo("long 1"));

    [TestCase("NullInt ?? 1", typeof(int), 1)]
    [TestCase("NullableInt ?? 1", typeof(int), 5)]
    [TestCase("NullInt ?? NullableInt", typeof(int?), 5)]
    [TestCase("NullInt ?? 1L", typeof(long), 1L)]
    [TestCase("NullInt ?? 1.5", typeof(double), 1.5)]
    [TestCase("NullableByte ?? Count", typeof(int), 3)]
    [TestCase("NullInt ?? NullableLong", typeof(long?), 7L)]
    [TestCase("NullInt ?? (1 :> object)", typeof(object), 1)]
    [TestCase("NullInt ?? NullInt ?? 9", typeof(int), 9)]
    [TestCase("NullBool ?? Flag", typeof(bool), true)]
    [TestCase("NullDay ?? Day", typeof(DayOfWeek), DayOfWeek.Monday)]
    [TestCase("NullableDay ?? DayOfWeek.Sunday", typeof(DayOfWeek), DayOfWeek.Friday)]
    [TestCase("NullInt ?? null", typeof(int?), null)]
    [TestCase("null ?? NullableInt", typeof(int?), 5)]
    public void Evaluate_NullCoalescingOnNullable_ReturnsTypeOfCSharp(string expression, Type type, object? expected) =>
        AssertNullableResult(expression, type, expected);

    [TestCaseSource(nameof(LiftedBinaryOperatorSource))]
    public void Evaluate_LiftedBinaryOperator_ReturnsNullableResult(string expression, Type type, object? expected) =>
        AssertNullableResult(expression, type, expected);

    [TestCase("NullableInt > 1", true)]
    [TestCase("NullInt > 1", false)]
    [TestCase("NullInt <= 1", false)]
    [TestCase("NullableInt >= NullableLong", false)]
    [TestCase("NullableInt < 1.5", false)]
    [TestCase("NullableInt == 5", true)]
    [TestCase("NullInt == 5", false)]
    [TestCase("NullInt != 5", true)]
    [TestCase("NullableInt == NullableLong", false)]
    [TestCase("NullableByte == 3", true)]
    [TestCase("NullableDecimal > 1", true)]
    [TestCase("NullableTrue == true", true)]
    [TestCase("NullBool == false", false)]
    [TestCase("NullableDay == DayOfWeek.Friday", true)]
    [TestCase("NullDay == DayOfWeek.Friday", false)]
    [TestCase("NullableDay > Day", true)]
    [TestCase("NullableDate > Date", true)]
    [TestCase("NullableDate == Date", false)]
    [TestCase("NullInt == null", true)]
    [TestCase("null != NullableInt", true)]
    [TestCase("NullDay == null", true)]
    [TestCase("NullableDay != null", true)]
    [TestCase("NullInt > null", false)]
    public void Evaluate_LiftedComparison_ReturnsBoolean(string expression, bool expected) =>
        AssertNullableResult(expression, typeof(bool), expected);

    [TestCase("-NullableInt", typeof(int?), -5)]
    [TestCase("-NullInt", typeof(int?), null)]
    [TestCase("-NullableByte", typeof(int?), -3)]
    [TestCase("~NullableByte", typeof(int?), -4)]
    [TestCase("-NullableChar", typeof(int?), -97)]
    [TestCase("+NullableChar", typeof(int?), 97)]
    [TestCase("-NullableUInt", typeof(long?), -4L)]
    [TestCase("~NullableDay", typeof(DayOfWeek?), ~DayOfWeek.Friday)]
    [TestCase("~NullDay", typeof(DayOfWeek?), null)]
    public void Evaluate_LiftedUnaryOperator_ReturnsNullableResult(string expression, Type type, object? expected) =>
        AssertNullableResult(expression, type, expected);

    [TestCase("Flag ? NullableInt : 1", typeof(int?), 5)]
    [TestCase("Flag ? 1 : NullableInt", typeof(int?), 1)]
    [TestCase("Flag ? NullableInt : NullableLong", typeof(long?), 5L)]
    [TestCase("Flag ? NullDay : Day", typeof(DayOfWeek?), null)]
    public void Evaluate_ConditionalWithNullableBranch_ReturnsNullableType(string expression, Type type, object? expected) =>
        AssertNullableResult(expression, type, expected);

    [TestCase("NullableDouble + NullableDecimal", "Operator '+' cannot be applied to operands of type 'System.Nullable`1[System.Double]' and 'System.Nullable`1[System.Decimal]'.")]
    [TestCase("NullableULong + NullableInt", "Operator '+' cannot be applied to operands of type 'System.Nullable`1[System.UInt64]' and 'System.Nullable`1[System.Int32]'.")]
    [TestCase("NullableInt << 1L", "Operator '<<' cannot be applied to operands of type 'System.Nullable`1[System.Int32]' and 'System.Int64'.")]
    [TestCase("NullableInt ?? \"x\"", "Operator '??' cannot be applied to operands of type 'System.Nullable`1[System.Int32]' and 'System.String'.")]
    [TestCase("-NullableDay", "Unary operator '-' cannot be applied to operand of type 'System.Nullable`1[System.DayOfWeek]'.")]
    [TestCase("+NullableDay", "Unary operator '+' cannot be applied to operand of type 'System.Nullable`1[System.DayOfWeek]'.")]
    [TestCase("-NullableULong", "Unary operator '-' cannot be applied to operand of type 'System.Nullable`1[System.UInt64]'.")]
    [TestCase("NullBool && true", "Cannot implicitly convert type 'System.Nullable`1[System.Boolean]' to 'System.Boolean'.")]
    [TestCase("Flag ? NullableInt : 1L", "Operator '?:' cannot be applied to operands of type 'System.Nullable`1[System.Int32]' and 'System.Int64'.")]
    public void Parse_NullableOperandsWithoutOperatorInCSharp_ReturnsError(string expression, string expected)
    {
        var parameter = Expression.Parameter(typeof(NullableContext), "c");
        var result = ExpressionParser.Parse(expression, new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(expected));
    }

    [TestCase("Day + ByteValue", typeof(DayOfWeek), DayOfWeek.Saturday)]
    [TestCase("ShortValue + Day", typeof(DayOfWeek), DayOfWeek.Saturday)]
    [TestCase("Day + CharValue", typeof(DayOfWeek), (DayOfWeek)102)]
    [TestCase("Day - ByteValue", typeof(DayOfWeek), DayOfWeek.Thursday)]
    [TestCase("ByteValue - Day", typeof(DayOfWeek), (DayOfWeek)(-4))]
    [TestCase("Day - DayOfWeek.Monday", typeof(int), 4)]
    [TestCase("Day - Day", typeof(int), 0)]
    [TestCase("Day - 0", typeof(DayOfWeek), DayOfWeek.Friday)]
    [TestCase("Day - 0L", typeof(int), 5)]
    [TestCase("0 - Day", typeof(int), -5)]
    [TestCase("Level + ByteValue", typeof(ByteLevel), ByteLevel.High)]
    [TestCase("Level - NextLevel", typeof(byte), (byte)255)]
    [TestCase("Level - 0", typeof(byte), (byte)1)]
    [TestCase("NullableDay - DayOfWeek.Monday", typeof(int?), 4)]
    [TestCase("NullDay - Day", typeof(int?), null)]
    [TestCase("0 - NullableDay", typeof(int?), -5)]
    [TestCase("NullableDay + NullableByteValue", typeof(DayOfWeek?), DayOfWeek.Saturday)]
    [TestCase("NullableDay + null", typeof(DayOfWeek?), null)]
    [TestCase("null + NullableDay", typeof(DayOfWeek?), null)]
    [TestCase("NullableDay - null", typeof(int?), null)]
    public void Evaluate_EnumAdditiveOperator_ReturnsTypeOfCSharp(string expression, Type type, object? expected) =>
        AssertResult(new EnumContext(), expression, type, expected);

    [TestCase("Day & 0", typeof(DayOfWeek), DayOfWeek.Sunday)]
    [TestCase("0u | Day", typeof(DayOfWeek), DayOfWeek.Friday)]
    [TestCase("Day ^ (0)", typeof(DayOfWeek), DayOfWeek.Friday)]
    [TestCase("Level | 0", typeof(ByteLevel), ByteLevel.Low)]
    [TestCase("NullableDay & 0L", typeof(DayOfWeek?), DayOfWeek.Sunday)]
    [TestCase("Day == 0", typeof(bool), false)]
    [TestCase("0x0 < Day", typeof(bool), true)]
    [TestCase("Day != 0ul", typeof(bool), true)]
    [TestCase("Level == 0", typeof(bool), false)]
    [TestCase("NullableDay > 0", typeof(bool), true)]
    [TestCase("NullDay == 0", typeof(bool), false)]
    public void Evaluate_EnumAndZeroLiteral_ConvertsZeroToEnum(string expression, Type type, object? expected) =>
        AssertResult(new EnumContext(), expression, type, expected);

    [TestCase("Level + IntValue", "Operator '+' cannot be applied to operands of type 'Ramstack.Parsing.ExpressionParserTest+ByteLevel' and 'System.Int32'.")]
    [TestCase("Day & ByteValue", "Operator '&' cannot be applied to operands of type 'System.DayOfWeek' and 'System.Byte'.")]
    [TestCase("Day == IntValue", "Operator '==' cannot be applied to operands of type 'System.DayOfWeek' and 'System.Int32'.")]
    [TestCase("Day == Level", "Operator '==' cannot be applied to operands of type 'System.DayOfWeek' and 'Ramstack.Parsing.ExpressionParserTest+ByteLevel'.")]
    [TestCase("NullableDay * 2", "Operator '*' cannot be applied to operands of type 'System.Nullable`1[System.DayOfWeek]' and 'System.Int32'.")]
    [TestCase("NullableDay >>> 1", "Operator '>>>' cannot be applied to operands of type 'System.Nullable`1[System.DayOfWeek]' and 'System.Int32'.")]
    [TestCase("NullableDay + NullableDay", "Operator '+' cannot be applied to operands of type 'System.Nullable`1[System.DayOfWeek]' and 'System.Nullable`1[System.DayOfWeek]'.")]
    [TestCase("NullableDay == 5", "Operator '==' cannot be applied to operands of type 'System.Nullable`1[System.DayOfWeek]' and 'System.Int32'.")]
    public void Parse_EnumOperandsWithoutOperatorInCSharp_ReturnsError(string expression, string expected)
    {
        var parameter = Expression.Parameter(typeof(EnumContext), "c");
        var result = ExpressionParser.Parse(expression, new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(ErrorsSource))]
    public void ParseErrorExpression(string expression, string expected)
    {
        var result = ExpressionParser.Parse(expression, new DefaultBinder());

        Console.WriteLine(result.ToString());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void MaxDepthTest()
    {
        //
        // The literal is the first level, and each operator adds one more level.
        //
        Assert.That(ExpressionParser.Parse(new string('-', 255) + "1").Success, Is.True);
        Assert.That(ExpressionParser.Parse(new string('-', 256) + "1").ToString(),
            Is.EqualTo("(1:258) The expression exceeds the maximum depth of 256."));

        Assert.That(ExpressionParser.Parse(string.Join("+", Enumerable.Repeat("1", 256))).Success, Is.True);
        Assert.That(ExpressionParser.Parse(string.Join("+", Enumerable.Repeat("1", 257))).Success, Is.False);
    }

    [TestCaseSource(nameof(DeepExpressionSource))]
    public void DeepExpressionTest(string expression)
    {
        var result = ExpressionParser.Parse(expression, new DefaultBinder());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.EndWith("The expression exceeds the maximum depth of 256."));
    }

    [TestCaseSource(nameof(DeepNestingSource))]
    public void DeepNestingTest(string expression, int stackSize)
    {
        var result = RunOnThread(stackSize, () => ExpressionParser.Parse(expression, new DefaultBinder()));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.EndWith("The expression is nested too deeply."));
    }

    [TestCaseSource(nameof(StackBudgetSource))]
    public void StackBudgetTest(string expression)
    {
        //
        // The default stack of a thread on Windows is 1 MB, so the expressions at the maximum depth
        // and moderately nested parentheses must be parsed and built on such a stack.
        //
        var result = RunOnThread(1024 * 1024, () => ExpressionParser.Parse(expression, new DefaultBinder()));
        Assert.That(result.Success, Is.True, result.ErrorMessage);
    }

    [Test]
    public void DeepTreeBuildTest()
    {
        //
        // The parser does not produce such a tree, but the tree can be created directly.
        //
        Expr expr = new Expr.Literal(1);
        for (var i = 0; i < 100_000; i++)
            expr = new Expr.Unary(new Identifier("-"), UnaryType.Negate, expr);

        var builder = new ExpressionBuilder(new DefaultBinder());
        var exception = RunOnThread(1024 * 1024, () =>
        {
            try
            {
                builder.Visit(expr);
                return null;
            }
            catch (ParseErrorException e)
            {
                return e;
            }
        });

        Assert.That(exception?.Message, Is.EqualTo("The expression is nested too deeply."));
    }

    [TestCaseSource(nameof(ExpressionSource))]
    public void ParseExpressionTest(string source, string expected)
    {
        var result = ExpressionParser.Parse(source, new DefaultBinder());
        Assert.That(result.ToString(), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(StringConcatSource))]
    public void StringConcatTest(string source, string expected)
    {
        var result = ExpressionParser.Parse(source, new DefaultBinder());
        Assert.That(result.ToString(), Is.EqualTo(expected));
    }

    private static void AssertNullableResult(string expression, Type type, object? expected) =>
        AssertResult(new NullableContext(), expression, type, expected);

    private static void AssertResult<TContext>(TContext context, string expression, Type type, object? expected)
    {
        var parameter = Expression.Parameter(typeof(TContext), "c");
        var result = ExpressionParser.Parse(expression, new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.Value!.Type, Is.EqualTo(type));

        //
        // Boxing loses the nullable type, so the type of the value is checked separately from the type of the expression.
        //
        var body = Expression.Convert(result.Value, typeof(object));
        var value = Expression.Lambda<Func<TContext, object?>>(body, parameter).Compile()(context);

        Assert.That(value, Is.EqualTo(expected));
        Assert.That(value?.GetType(), Is.EqualTo(expected?.GetType()));
    }

    private static T RunOnThread<T>(int maxStackSize, Func<T> func)
    {
        var result = default(T);
        var exception = default(ExceptionDispatchInfo);

        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }
        }, maxStackSize);

        thread.Start();
        thread.Join();

        exception?.Throw();
        return result!;
    }

    private static List<object[]> GetResource(string resourceName)
    {
        using var stream = typeof(ExpressionParserTest).Assembly.GetManifestResourceStream($"Ramstack.Parsing.Data.{resourceName}");
        using var reader = new StreamReader(stream!);

        var result = new List<object[]>();

        while (true)
        {
            string? s;
            string? e;

            while ((s = reader.ReadLine()) is not null)
                if (!string.IsNullOrWhiteSpace(s) && !s.StartsWith('#'))
                    break;

            while ((e = reader.ReadLine()) is not null)
                if (!string.IsNullOrWhiteSpace(e) && !e.StartsWith('#'))
                    break;

            if (s is null || e is null)
                break;

            s = s.Replace(@"\n", "\r\n");
            e = e.Replace(@"\n", "\r\n");

            result.Add([s, e]);
        }

        return result;
    }

    public class UserInfo
    {
        public string? Name { get; set; }
        public string? LastName { get; set; }
    }

    public interface IValueContext
    {
        int Value { get; }
    }

    public class ValueContext : IValueContext
    {
        public int Value { get; } = 1;
        public int Extra { get; } = 2;
    }

    public class LogicalNotContext
    {
        public bool Flag { get; } = true;
        public bool? NullableFlag { get; } = false;
        public LogicalNotOperand WithLogicalNot { get; } = new();
        public DerivedLogicalNotOperand WithInheritedLogicalNot { get; } = new();
        public LogicalNotStruct? NullableWithLogicalNot { get; } = new LogicalNotStruct();
        public OnesComplementOperand WithOnesComplement { get; } = new();
    }

    public class LogicalNotOperand
    {
        public static bool operator !(LogicalNotOperand _) => true;
    }

    public class DerivedLogicalNotOperand : LogicalNotOperand;

    public struct LogicalNotStruct
    {
        public static bool operator !(LogicalNotStruct _) => true;
    }

    public class OnesComplementOperand
    {
        public static bool operator ~(OnesComplementOperand _) => true;
    }

    public class OverloadContext
    {
        public static string Pick(char _) => "char";
        public static string Pick(int _) => "int";
        public static string OnlyChar(char _) => "char";

        public static string WithDefault(int a) => "(int)";
        public static string WithDefault(int a, int b = 0) => "(int, int = 0)";

        public static string ParamsOrDefault(int a, params int[] rest) => "(int, params int[])";
        public static string ParamsOrDefault(int a, int b = 0) => "(int, int = 0)";

        public static string Expanded(int a, params int[] rest) => "(int, params int[])";
        public static string Expanded(int a, int b, params int[] rest) => "(int, int, params int[])";

        public static string ExpandedOrDefault(int a, params int[] rest) => "(int, params int[])";
        public static string ExpandedOrDefault(int a, int b = 0, params int[] rest) => "(int, int = 0, params int[])";

        public static string BothDefaults(int a, int b = 0) => "(int, int = 0)";
        public static string BothDefaults(int a, int b = 0, int c = 0) => "(int, int = 0, int = 0)";

        public static string ExpandedBothDefaults(int a, int b = 0, params int[] rest) => "(int, int = 0, params int[])";
        public static string ExpandedBothDefaults(int a, int b = 0, int c = 0, params int[] rest) => "(int, int = 0, int = 0, params int[])";
    }

    public class IndexerContext
    {
        public MixedAccessors Mixed { get; } = new();
        public WriteOnlyIndexer WriteOnly { get; } = new();
    }

    public class MixedAccessors
    {
        private string? _name;

        public string this[int index] => $"item {index}";
        public string this[string key] { set { } }
        public string Name { set => _name = value; }
    }

    public class WriteOnlyIndexer
    {
        public string this[int index] { set { } }
    }

    public class AccessorContext
    {
        private string _value = "value";
        private Func<int, int> _delegate = x => x;

        public string PrivateGetter { private get => _value; set => _value = value; }
        public string ProtectedGetter { protected get => _value; set => _value = value; }
        public string InternalGetter { internal get => _value; set => _value = value; }
        public string WriteOnly { set => _value = value; }
        public static string StaticPrivateGetter { private get => "static"; set { } }
        public Func<int, int> PrivateDelegate { private get => _delegate; set => _delegate = value; }
        public PrivateGetterIndexer PrivateIndexer { get; } = new();
        public OverloadedIndexer Overloaded { get; } = new();
        public AccessorContext Self => this;
    }

    public class PrivateGetterIndexer
    {
        public string this[int index] { private get => "private"; set { } }
    }

    public class OverloadedIndexer
    {
        public string this[int index] { private get => "private"; set { } }
        public string this[long index] => $"long {index}";
    }

    public class ParamArrayContext
    {
        public string[] Words { get; } = ["a", "b"];
        public int[] Numbers { get; } = [1, 2, 3];
        public object[] Objects { get; } = ["x", 1];

        public static int Sum(params int[] values) => values.Sum();
        public static int Count(params object[] values) => values.Length;
        public bool IsWords(params string[] values) => ReferenceEquals(values, Words);
    }

    public class DelegateContext
    {
        public Func<int, int> Doubler { get; } = x => x * 2;
    }

    public static class StaticDelegates
    {
        public static Func<int, int> Tripler { get; } = x => x * 3;
    }

    public class ConversionContext
    {
        public string? Missing { get; }
        public bool Flag { get; } = true;
        public ConvertibleToString StringOnly { get; } = new();
        public ConvertibleToStringAndBool StringAndBool { get; } = new();
        public ConvertibleToBool BoolOnly { get; } = new();
    }

    public class ConvertibleToString
    {
        public static implicit operator string(ConvertibleToString _) => "string";
    }

    public class ConvertibleToStringAndBool
    {
        public static implicit operator string(ConvertibleToStringAndBool _) => "string";
        public static implicit operator bool(ConvertibleToStringAndBool _) => true;
    }

    public class ConvertibleToBool
    {
        public static implicit operator bool(ConvertibleToBool _) => true;
    }

    public class OptionalContext
    {
        public static string Defaults(int number = 1, DayOfWeek? day = DayOfWeek.Monday, Guid id = default, string? text = null) =>
            $"{number} {day} {id == Guid.Empty} {text ?? "null"}";

        public static string Mixed(int a, int b = 2, params int[] rest) =>
            $"{a} {b} {rest.Length}";

        public static string WithMissing([Optional] object value, [Optional] int number) =>
            $"{value.GetType().Name} {number}";
    }

    public class NullableContext
    {
        public int? NullableInt { get; } = 5;
        public int? NullInt { get; }
        public long? NullableLong { get; } = 7;
        public byte? NullableByte { get; } = 3;
        public char? NullableChar { get; } = 'a';
        public uint? NullableUInt { get; } = 4;
        public ulong? NullableULong { get; } = 8;
        public double? NullableDouble { get; } = 1.5;
        public decimal? NullableDecimal { get; } = 2.5m;
        public bool? NullableTrue { get; } = true;
        public bool? NullBool { get; }
        public DayOfWeek? NullableDay { get; } = DayOfWeek.Friday;
        public DayOfWeek? NullDay { get; }
        public DateTime? NullableDate { get; } = new DateTime(2020, 1, 2);
        public TimeSpan? NullableSpan { get; } = TimeSpan.FromHours(1);
        public int Count { get; } = 3;
        public long Big { get; } = 10;
        public bool Flag { get; } = true;
        public DayOfWeek Day { get; } = DayOfWeek.Monday;
        public DateTime Date { get; } = new DateTime(2020, 1, 1);
    }

    public enum ByteLevel : byte
    {
        Low = 1,
        High = 2
    }

    public class EnumContext
    {
        public DayOfWeek Day { get; } = DayOfWeek.Friday;
        public DayOfWeek? NullableDay { get; } = DayOfWeek.Friday;
        public DayOfWeek? NullDay { get; }
        public ByteLevel Level { get; } = ByteLevel.Low;
        public ByteLevel NextLevel { get; } = ByteLevel.High;
        public byte ByteValue { get; } = 1;
        public byte? NullableByteValue { get; } = 1;
        public short ShortValue { get; } = 1;
        public char CharValue { get; } = 'a';
        public int IntValue { get; } = 1;
    }

    public class ArrayContext
    {
        public int[] Numbers { get; } = [1, 2, 3];
        public int[,] Matrix { get; } = new int[2, 3];
        public int[,,] Cube { get; } = new int[2, 2, 2];
        public int[][] Jagged { get; } = [[1], [1, 2, 3]];
        public Array Untyped { get; } = new[] { "a", "b" };
    }
}
