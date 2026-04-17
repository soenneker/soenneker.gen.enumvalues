using AwesomeAssertions;
using Soenneker.Gen.EnumValues.Tests.Enums;
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
        var testObject1 = new TestObject() { ColorCode = Enums.ColorCode.Blue };

        var testObject2 = testObject1.Adapt<TestObject>();

        testObject2.ColorCode.Should()
                   .Be(Enums.ColorCode.Blue);
    }
}
