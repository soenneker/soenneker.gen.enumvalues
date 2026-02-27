using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Soenneker.Gen.EnumValues.Dtos;

internal readonly struct EnumSourceBuildContext
{
    public readonly INamedTypeSymbol EnumType;
    public readonly INamedTypeSymbol ValueType;
    public readonly List<EnumInstance> Instances;
    public readonly bool HasValueProperty;
    public readonly bool HasValueIdConstructor;
    public readonly bool HasNameProperty;
    public readonly bool SupportsNewtonsoft;
    public readonly string EnumTypeName;
    public readonly string ValueTypeName;
    public readonly string Ns;
    public readonly string Kind;
    public readonly string StjConverterTypeName;
    public readonly string NewtonsoftConverterTypeName;
    public readonly string TypeConverterName;
    public readonly bool IsStringValue;
    public readonly bool UseIdBacking;
    public readonly string ValueTryFromSignature;
    public readonly List<(string ConstantName, string TargetName)> ValueItems;
    public readonly List<(string ConstantName, string TargetName)> NameItems;
    public readonly List<(string Text, string TargetName)> NameSpanItems;
    public readonly List<(string Text, string TargetName)> ValueSpanItems;
    public readonly string StjReadRawValueCode;
    public readonly string StjWriteValueCode;

    public EnumSourceBuildContext(
        INamedTypeSymbol enumType,
        INamedTypeSymbol valueType,
        List<EnumInstance> instances,
        bool hasValueProperty,
        bool hasValueIdConstructor,
        bool hasNameProperty,
        bool supportsNewtonsoft,
        string enumTypeName,
        string valueTypeName,
        string ns,
        string kind,
        string stjConverterTypeName,
        string newtonsoftConverterTypeName,
        string typeConverterName,
        bool isStringValue,
        bool useIdBacking,
        string valueTryFromSignature,
        List<(string ConstantName, string TargetName)> valueItems,
        List<(string ConstantName, string TargetName)> nameItems,
        List<(string Text, string TargetName)> nameSpanItems,
        List<(string Text, string TargetName)> valueSpanItems,
        string stjReadRawValueCode,
        string stjWriteValueCode)
    {
        EnumType = enumType;
        ValueType = valueType;
        Instances = instances;
        HasValueProperty = hasValueProperty;
        HasValueIdConstructor = hasValueIdConstructor;
        HasNameProperty = hasNameProperty;
        SupportsNewtonsoft = supportsNewtonsoft;
        EnumTypeName = enumTypeName;
        ValueTypeName = valueTypeName;
        Ns = ns;
        Kind = kind;
        StjConverterTypeName = stjConverterTypeName;
        NewtonsoftConverterTypeName = newtonsoftConverterTypeName;
        TypeConverterName = typeConverterName;
        IsStringValue = isStringValue;
        UseIdBacking = useIdBacking;
        ValueTryFromSignature = valueTryFromSignature;
        ValueItems = valueItems;
        NameItems = nameItems;
        NameSpanItems = nameSpanItems;
        ValueSpanItems = valueSpanItems;
        StjReadRawValueCode = stjReadRawValueCode;
        StjWriteValueCode = stjWriteValueCode;
    }
}