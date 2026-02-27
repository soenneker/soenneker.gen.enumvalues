using AwesomeAssertions;
using Xunit;

namespace Soenneker.Gen.EnumValues.Tests;

public sealed class EnumValuesAdaptTests
{
    public EnumValuesAdaptTests(ITestOutputHelper output)
    {
    }

    [Fact]
    public void Adapt_should_adapt()
    {
        var testObject1 = new TestObject() { ColorCode = ColorCode.Blue };

        var testObject2 = testObject1.Adapt<TestObject>();

        testObject2.ColorCode.Should()
                   .Be(ColorCode.Blue);
    }
}
