using Ramstack.Parsing.Internal;

namespace Ramstack.Parsing;

[TestFixture]
public class TypeUtilsTest
{
    [TestCase(typeof(char))]
    [TestCase(typeof(sbyte))]
    [TestCase(typeof(byte))]
    [TestCase(typeof(short))]
    [TestCase(typeof(ushort))]
    [TestCase(typeof(int))]
    [TestCase(typeof(uint))]
    [TestCase(typeof(long))]
    [TestCase(typeof(ulong))]
    public void CanConvertPrimitive_IntegerToDecimal_ReturnsTrue(Type source) =>
        Assert.That(TypeUtils.CanConvertPrimitive(source, typeof(decimal)), Is.True);

    [TestCase(typeof(float), typeof(decimal))]
    [TestCase(typeof(double), typeof(decimal))]
    [TestCase(typeof(decimal), typeof(float))]
    [TestCase(typeof(decimal), typeof(double))]
    [TestCase(typeof(decimal), typeof(int))]
    public void CanConvertPrimitive_ExplicitDecimalConversion_ReturnsFalse(Type source, Type target) =>
        Assert.That(TypeUtils.CanConvertPrimitive(source, target), Is.False);

    [TestCase(typeof(int), typeof(DayOfWeek))]
    [TestCase(typeof(DayOfWeek), typeof(int))]
    [TestCase(typeof(DayOfWeek), typeof(DayOfWeek))]
    public void CanConvertPrimitive_Enum_ReturnsFalse(Type source, Type target) =>
        Assert.That(TypeUtils.CanConvertPrimitive(source, target), Is.False);
}
