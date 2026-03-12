using Microsoft.CodeAnalysis;

namespace Soenneker.Gen.EnumValues;

internal sealed class EnumTypeCandidate
{
    public EnumTypeCandidate(INamedTypeSymbol enumType, INamedTypeSymbol valueType)
    {
        EnumType = enumType;
        ValueType = valueType;
    }

    public INamedTypeSymbol EnumType { get; }

    public INamedTypeSymbol ValueType { get; }
}
