using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;

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
            list.Add(["""DateTime.Today""", DateTime.Today]);
            list.Add(["""sbyte.MinValue""", sbyte.MinValue]);
            list.Add(["""-0x80000000""", int.MinValue]);
            list.Add([""" 0x80000000""", 0x80000000]);
            list.Add(["""-0x8000000000000000""", long.MinValue]);
            list.Add([""" 0x8000000000000000""", 0x8000000000000000L]);
            list.Add(["""ulong.MaxValue""", ulong.MaxValue]);
            list.Add(["""Guid.Empty""", Guid.Empty]);
            list.Add(["""1 :> DayOfWeek :> object""", DayOfWeek.Monday]);
            list.Add(["""(DayOfWeek.Friday :> object).ToString()""", "Friday"]);
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

    [TestCase("-1u", -1L)]
    [TestCase("-4294967295u", -4294967295L)]
    [TestCase("-3000000000", -3000000000L)]
    [TestCase("-(5 :> uint)", -5L)]
    public void Evaluate_NegatedUInt32_ReturnsNegatedInt64(string expression, long expected) =>
        Assert.That(ExpressionParser.Evaluate(expression), Is.TypeOf<long>().And.EqualTo(expected));

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

    [Test]
    public void Parse_ByteArgumentForCharParameter_ReturnsError()
    {
        var parameter = Expression.Parameter(typeof(OverloadContext), "c");
        var result = ExpressionParser.Parse("OnlyChar(97 :> byte)", new ContextAwareBinder(parameter));

        Assert.That(result.Success, Is.False);
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
    }
}
