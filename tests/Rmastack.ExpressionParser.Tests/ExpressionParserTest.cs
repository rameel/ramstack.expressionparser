using System.Globalization;
using System.Linq.Expressions;

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
            return list;
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

    [TestCaseSource(nameof(ErrorsSource))]
    public void ParseErrorExpression(string expression, string expected)
    {
        var result = ExpressionParser.Parse(expression, new DefaultBinder());

        Console.WriteLine(result.ToString());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ToString(), Is.EqualTo(expected));
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
                if (!string.IsNullOrWhiteSpace(s))
                    break;

            while ((e = reader.ReadLine()) is not null)
                if (!string.IsNullOrWhiteSpace(e))
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
}
