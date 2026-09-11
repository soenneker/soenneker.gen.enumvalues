using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace Soenneker.Gen.EnumValues.Dtos;

internal readonly struct EnumSourceBuildContext : System.IEquatable<EnumSourceBuildContext>
{
    public readonly string EnumTypeSimpleName;
    public readonly bool IsReferenceType;
    public readonly SpecialType ValueSpecialType;
    public readonly TypeKind ValueKind;
    public readonly bool CanEmitValueConstant;
    public readonly string ToStringExpression;
    public readonly string StjPropertyNameFallback;
    public readonly string NewtonsoftReadCode;
    public readonly string NewtonsoftWriteCode;
    public readonly string TypeConverterFromBody;
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
    public readonly string? SizeDependentMethodImplOption;
    public readonly byte[][] ValueUtf8Bytes;
    public readonly bool[] ExistingValueConstants;
    public readonly bool[] ExistingNameConstants;
    public readonly bool[] ExistingInstances;

    public EnumSourceBuildContext(INamedTypeSymbol enumType, INamedTypeSymbol valueType, List<EnumInstance> instances, bool hasValueProperty,
        bool hasValueIdConstructor, bool hasNameProperty, bool supportsNewtonsoft, string enumTypeName, string valueTypeName, string ns, string kind,
        string stjConverterTypeName, string newtonsoftConverterTypeName, string typeConverterName, bool isStringValue, bool useIdBacking,
        string valueTryFromSignature, List<(string ConstantName, string TargetName)> valueItems, List<(string ConstantName, string TargetName)> nameItems,
        List<(string Text, string TargetName)> nameSpanItems, List<(string Text, string TargetName)> valueSpanItems, string stjReadRawValueCode,
        string stjWriteValueCode, string? sizeDependentMethodImplOption)
    {
        EnumTypeSimpleName = enumType.Name;
        IsReferenceType = enumType.IsReferenceType;
        ValueSpecialType = valueType.SpecialType;
        ValueKind = valueType.TypeKind;
        CanEmitValueConstant = EnumValueSourceGenerator.CanEmitConstant(valueType);
        ToStringExpression = EnumValueSourceGenerator.BuildToStringExpression(valueType);
        StjPropertyNameFallback = isStringValue ? string.Empty : EnumValueSourceGenerator.BuildStjWritePropertyNameFallback(valueType);
        NewtonsoftReadCode = supportsNewtonsoft ? EnumValueSourceGenerator.BuildNewtonsoftReadRawValueCode(valueType) : string.Empty;
        NewtonsoftWriteCode = supportsNewtonsoft ? EnumValueSourceGenerator.BuildNewtonsoftWriteValueCode(valueType) : string.Empty;
        TypeConverterFromBody = isStringValue ? string.Empty : EnumValueSourceGenerator.BuildTypeConverterConvertFromBody(valueType, enumTypeName);
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
        SizeDependentMethodImplOption = sizeDependentMethodImplOption;
        ValueUtf8Bytes = isStringValue ? new byte[instances.Count][] : System.Array.Empty<byte[]>();
        for (var i = 0; i < ValueUtf8Bytes.Length; i++)
            ValueUtf8Bytes[i] = Encoding.UTF8.GetBytes(instances[i].StringValue ?? string.Empty);
        ExistingValueConstants = new bool[instances.Count];
        ExistingNameConstants = new bool[instances.Count];
        ExistingInstances = new bool[instances.Count];
        for (var i = 0; i < instances.Count; i++)
        {
            ExistingValueConstants[i] = enumType.GetMembers(instances[i].Name + "Value").Length != 0;
            ExistingNameConstants[i] = enumType.GetMembers(instances[i].Name + "Name").Length != 0;
            ExistingInstances[i] = enumType.GetMembers(instances[i].Name).Length != 0;
        }
    }

    public bool Equals(EnumSourceBuildContext other)
    {
        // Compare every fact used by emission, not Roslyn symbol identity. Locations
        // belong to diagnostics and intentionally do not invalidate valid source.
        if (EnumTypeName != other.EnumTypeName || EnumTypeSimpleName != other.EnumTypeSimpleName || IsReferenceType != other.IsReferenceType || ValueTypeName != other.ValueTypeName ||
            Kind != other.Kind || ValueKind != other.ValueKind || ValueSpecialType != other.ValueSpecialType ||
            HasValueProperty != other.HasValueProperty || HasValueIdConstructor != other.HasValueIdConstructor ||
            HasNameProperty != other.HasNameProperty || SupportsNewtonsoft != other.SupportsNewtonsoft ||
            SizeDependentMethodImplOption != other.SizeDependentMethodImplOption || Instances.Count != other.Instances.Count)
            return false;
        for (var i = 0; i < Instances.Count; i++)
        {
            EnumInstance left = Instances[i];
            EnumInstance right = other.Instances[i];
            if (left.Name != right.Name || left.ValueLiteral != right.ValueLiteral || left.StringValue != right.StringValue ||
                left.ValueJsonString != right.ValueJsonString || left.Id != right.Id ||
                ExistingValueConstants[i] != other.ExistingValueConstants[i] || ExistingNameConstants[i] != other.ExistingNameConstants[i] ||
                ExistingInstances[i] != other.ExistingInstances[i])
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EnumSourceBuildContext other && Equals(other);
    public override int GetHashCode() => EnumTypeName.GetHashCode();
}
