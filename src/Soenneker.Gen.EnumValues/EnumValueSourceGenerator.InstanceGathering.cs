using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Soenneker.Gen.EnumValues.Dtos;

namespace Soenneker.Gen.EnumValues;

public sealed partial class EnumValueSourceGenerator
{
    private static bool SupportsNewtonsoft(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("Newtonsoft.Json.JsonConverterAttribute") is not null &&
               compilation.GetTypeByMetadataName("Newtonsoft.Json.JsonConverter`1") is not null;
    }

    private static bool IsPartial(INamedTypeSymbol enumType)
    {
        ImmutableArray<SyntaxReference> declarations = enumType.DeclaringSyntaxReferences;

        for (var i = 0; i < declarations.Length; i++)
        {
            if (declarations[i]
                    .GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
                continue;

            if (!typeDeclaration.Modifiers.Any(static token => token.IsKind(SyntaxKind.PartialKeyword)))
                return false;
        }

        return true;
    }

    private static bool HasValueProperty(INamedTypeSymbol enumType, INamedTypeSymbol valueType)
    {
        foreach (ISymbol member in enumType.GetMembers("Value"))
        {
            if (member is not IPropertySymbol propertySymbol)
                continue;

            if (propertySymbol.IsStatic || propertySymbol.IsIndexer)
                continue;

            if (SymbolEqualityComparer.Default.Equals(propertySymbol.Type, valueType))
                return true;
        }

        return false;
    }

    private static bool HasValueIdConstructor(INamedTypeSymbol enumType, INamedTypeSymbol valueType)
    {
        foreach (IMethodSymbol constructor in enumType.InstanceConstructors)
        {
            if (constructor.Parameters.Length != 2)
                continue;

            if (SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, valueType) &&
                constructor.Parameters[1].Type.SpecialType == SpecialType.System_Byte)
                return true;
        }

        return false;
    }

    private static IMethodSymbol? GetInvalidOpenConstructor(INamedTypeSymbol enumType)
    {
        foreach (IMethodSymbol constructor in enumType.InstanceConstructors)
        {
            if (constructor.IsImplicitlyDeclared)
                continue;

            if (constructor.DeclaredAccessibility != Accessibility.Private)
                return constructor;
        }

        return null;
    }

    private static bool HasNameProperty(INamedTypeSymbol enumType)
    {
        foreach (ISymbol member in enumType.GetMembers("Name"))
        {
            if (member is IPropertySymbol propertySymbol && !propertySymbol.IsStatic && !propertySymbol.IsIndexer)
                return true;
            if (member is IFieldSymbol fieldSymbol && !fieldSymbol.IsStatic)
                return true;
        }

        return false;
    }

    private static List<EnumInstance> GatherInstances(SourceProductionContext context, Compilation compilation, INamedTypeSymbol enumType,
        INamedTypeSymbol valueType)
    {
        return GatherInstancesCore(context, compilation, enumType, valueType, sourceTypeName: null);
    }

    private static List<EnumInstance> GatherInstancesFromType(SourceProductionContext context, Compilation compilation, INamedTypeSymbol sourceType,
        INamedTypeSymbol valueType, string sourceTypeName)
    {
        return GatherInstancesCore(context, compilation, sourceType, valueType, sourceTypeName);
    }

    private static List<EnumInstance> GatherInstancesCore(SourceProductionContext context, Compilation compilation, INamedTypeSymbol enumType,
        INamedTypeSymbol valueType, string? sourceTypeName)
    {
        var result = new List<EnumInstance>();

        foreach (ISymbol member in enumType.GetMembers())
        {
            if (member is IFieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.IsStatic || !SymbolEqualityComparer.Default.Equals(fieldSymbol.Type, enumType))
                    continue;

                if (TryGetValueLiteralFromField(compilation, fieldSymbol, valueType, out string? valueLiteral, out string? stringValue, out Location? location,
                        out byte? id, out Location? ordinalErrorLocation, out string? valueJsonString))
                {
                    if (ordinalErrorLocation is not null)
                        context.ReportDiagnostic(Diagnostic.Create(_ordinalNotAllowedDescriptor, ordinalErrorLocation, stringValue ?? valueLiteral ?? ""));
                    result.Add(new EnumInstance(fieldSymbol.Name, valueLiteral!, stringValue,
                        location ?? fieldSymbol.Locations.FirstOrDefault() ?? Location.None, id, sourceTypeName, valueJsonString));
                }
            }
            else if (member is IPropertySymbol propertySymbol)
            {
                if (!propertySymbol.IsStatic || !SymbolEqualityComparer.Default.Equals(propertySymbol.Type, enumType))
                    continue;

                if (TryGetValueLiteralFromProperty(compilation, propertySymbol, valueType, out string? valueLiteral, out string? stringValue,
                        out Location? location, out byte? id, out Location? ordinalErrorLocation, out string? valueJsonString))
                {
                    if (ordinalErrorLocation is not null)
                        context.ReportDiagnostic(Diagnostic.Create(_ordinalNotAllowedDescriptor, ordinalErrorLocation, stringValue ?? valueLiteral ?? ""));
                    result.Add(new EnumInstance(propertySymbol.Name, valueLiteral!, stringValue,
                        location ?? propertySymbol.Locations.FirstOrDefault() ?? Location.None, id, sourceTypeName, valueJsonString));
                }
            }
        }

        result.Sort(static (left, right) => left.Location.SourceSpan.Start.CompareTo(right.Location.SourceSpan.Start));

        return result;
    }

    private static bool TryGetValueLiteralFromField(Compilation compilation, IFieldSymbol symbol, INamedTypeSymbol valueType, out string? valueLiteral,
        out string? stringValue, out Location? location, out byte? id, out Location? ordinalErrorLocation, out string? valueJsonString)
    {
        valueLiteral = null;
        stringValue = null;
        location = null;
        id = null;
        ordinalErrorLocation = null;
        valueJsonString = null;

        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not VariableDeclaratorSyntax variableDeclarator)
                continue;

            EqualsValueClauseSyntax? initializer = variableDeclarator.Initializer;
            if (initializer is null)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(initializer.SyntaxTree);

            if (TryGetValueLiteralFromInitializer(semanticModel, initializer.Value, valueType, out valueLiteral, out stringValue, out id,
                    out bool hasExplicitOrdinal, out valueJsonString))
            {
                location = initializer.GetLocation();
                if (hasExplicitOrdinal)
                    ordinalErrorLocation = initializer.GetLocation();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetValueLiteralFromProperty(Compilation compilation, IPropertySymbol symbol, INamedTypeSymbol valueType, out string? valueLiteral,
        out string? stringValue, out Location? location, out byte? id, out Location? ordinalErrorLocation, out string? valueJsonString)
    {
        valueLiteral = null;
        stringValue = null;
        location = null;
        id = null;
        ordinalErrorLocation = null;
        valueJsonString = null;

        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not PropertyDeclarationSyntax propertyDeclaration)
                continue;

            ExpressionSyntax? expression = propertyDeclaration.Initializer?.Value ?? propertyDeclaration.ExpressionBody?.Expression;

            if (expression is null)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(expression.SyntaxTree);

            if (TryGetValueLiteralFromInitializer(semanticModel, expression, valueType, out valueLiteral, out stringValue, out id, out bool hasExplicitOrdinal,
                    out valueJsonString))
            {
                location = expression.GetLocation();
                if (hasExplicitOrdinal)
                    ordinalErrorLocation = expression.GetLocation();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetValueLiteralFromInitializer(SemanticModel semanticModel, ExpressionSyntax initializerExpression, INamedTypeSymbol valueType,
        out string? valueLiteral, out string? stringValue, out byte? id, out bool hasExplicitOrdinal, out string? valueJsonString)
    {
        valueLiteral = null;
        stringValue = null;
        id = null;
        hasExplicitOrdinal = false;
        valueJsonString = null;

        ArgumentListSyntax? argumentList = initializerExpression switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.ArgumentList,
            ImplicitObjectCreationExpressionSyntax implicitObjectCreation => implicitObjectCreation.ArgumentList,
            _ => null
        };

        if (argumentList is null || argumentList.Arguments.Count == 0)
            return false;

        if (argumentList.Arguments.Count >= 2)
        {
            ITypeSymbol? secondArgType = semanticModel.GetTypeInfo(argumentList.Arguments[1].Expression)
                                                      .Type;
            if (secondArgType?.SpecialType == SpecialType.System_Byte || secondArgType?.SpecialType == SpecialType.System_Int32)
            {
                Optional<object?> idConstant = semanticModel.GetConstantValue(argumentList.Arguments[1].Expression);
                if (idConstant.HasValue && idConstant.Value is not null)
                {
                    if (idConstant.Value is byte b)
                        id = b;
                    else if (idConstant.Value is int i && i >= 0 && i <= 255)
                        id = (byte)i;
                }
            }
            else if (secondArgType?.SpecialType != SpecialType.System_String)
                hasExplicitOrdinal = true;
        }

        ExpressionSyntax valueExpression = argumentList.Arguments[0].Expression;
        Optional<object?> constant = semanticModel.GetConstantValue(valueExpression);

        if (!constant.HasValue || constant.Value is null)
            return false;

        if (!TryConvertConstant(constant.Value, valueType, out object? converted))
            return false;

        if (!TryFormatLiteral(converted, valueType, out valueLiteral))
            return false;

        TryGetValueJsonString(converted, valueType, out valueJsonString);
        stringValue = converted as string;
        return true;
    }

    private static bool TryConvertConstant(object value, ITypeSymbol valueType, out object? converted)
    {
        converted = null;

        try
        {
            switch (valueType.SpecialType)
            {
                case SpecialType.System_Int32:
                    converted = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_Int64:
                    converted = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_Int16:
                    converted = Convert.ToInt16(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_SByte:
                    converted = Convert.ToSByte(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_Byte:
                    converted = Convert.ToByte(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_UInt16:
                    converted = Convert.ToUInt16(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_UInt32:
                    converted = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_UInt64:
                    converted = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_String:
                    converted = Convert.ToString(value, CultureInfo.InvariantCulture);
                    return converted is not null;
                case SpecialType.System_Boolean:
                    converted = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                    return true;
                case SpecialType.System_Char:
                    converted = Convert.ToChar(value, CultureInfo.InvariantCulture);
                    return true;
                default:
                {
                    if (valueType.ToDisplayString() == "System.Guid")
                    {
                        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                        converted = Guid.Parse(text);
                        return true;
                    }

                    return false;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Gets the invariant string form of the value for JSON property names (no C# suffix like L, U).</summary>
    private static bool TryGetValueJsonString(object? value, ITypeSymbol valueType, out string? jsonString)
    {
        jsonString = null;
        if (value is null)
            return false;
        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Int16:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
                jsonString = Convert.ToString(value, CultureInfo.InvariantCulture);
                return jsonString is not null;
            case SpecialType.System_String:
                jsonString = (string?)value;
                return jsonString is not null;
            case SpecialType.System_Boolean:
                jsonString = (bool)value ? "true" : "false";
                return true;
            case SpecialType.System_Char:
                jsonString = new string((char)value, 1);
                return true;
            default:
                if (valueType.ToDisplayString() == "System.Guid")
                {
                    jsonString = ((Guid)value).ToString();
                    return true;
                }

                return false;
        }
    }

    private static bool TryFormatLiteral(object? value, ITypeSymbol valueType, out string? literal)
    {
        literal = null;

        if (value is null)
            return false;

        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
                literal = ((int)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_Int64:
                literal = ((long)value).ToString(CultureInfo.InvariantCulture) + "L";
                return true;
            case SpecialType.System_Int16:
                literal = "(short)" + ((short)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_SByte:
                literal = "(sbyte)" + ((sbyte)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_Byte:
                literal = "(byte)" + ((byte)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_UInt16:
                literal = "(ushort)" + ((ushort)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_UInt32:
                literal = ((uint)value).ToString(CultureInfo.InvariantCulture) + "U";
                return true;
            case SpecialType.System_UInt64:
                literal = ((ulong)value).ToString(CultureInfo.InvariantCulture) + "UL";
                return true;
            case SpecialType.System_String:
                literal = "\"" + EscapeString((string)value) + "\"";
                return true;
            case SpecialType.System_Boolean:
                literal = (bool)value ? "true" : "false";
                return true;
            case SpecialType.System_Char:
                literal = "'" + EscapeChar((char)value) + "'";
                return true;
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                {
                    literal = "new global::System.Guid(\"" + value + "\")";
                    return true;
                }

                return false;
            }
        }
    }
}