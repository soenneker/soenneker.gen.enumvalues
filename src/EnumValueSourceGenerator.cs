using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Soenneker.Gen.EnumValues;

[Generator]
public sealed class EnumValueSourceGenerator : IIncrementalGenerator
{
    private const int _valueFrozenThreshold = 128;
    private const int _nameFrozenThreshold = 256;

    private static readonly DiagnosticDescriptor _typeMustBePartialDescriptor = new(
        id: "SEV001",
        title: "EnumValue type must be partial",
        messageFormat: "Type '{0}' must be declared partial to use [EnumValue]",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _typeMustBeTopLevelDescriptor = new(
        id: "SEV002",
        title: "EnumValue type must be top-level",
        messageFormat: "Type '{0}' is nested and not supported by [EnumValue]",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _typeMustNotBeGenericDescriptor = new(
        id: "SEV003",
        title: "EnumValue type must not be generic",
        messageFormat: "Type '{0}' is generic and not supported by [EnumValue]",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _noInstancesDescriptor = new(
        id: "SEV004",
        title: "No enum value instances found",
        messageFormat: "Type '{0}' has [EnumValue] but no static instances with compile-time constant values were discovered",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _duplicateValueDescriptor = new(
        id: "SEV005",
        title: "Duplicate enum value detected",
        messageFormat: "Type '{0}' has duplicate enum value '{1}'",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("EnumValueAttributes.g.cs", SourceText.From(_attributeSource, Encoding.UTF8));
        });

        IncrementalValuesProvider<EnumTypeCandidate?> typeCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => TryGetCandidate(syntaxContext))
            .Where(static candidate => candidate is not null);

        IncrementalValueProvider<(Compilation compilation, ImmutableArray<EnumTypeCandidate?> candidates)> combined =
            context.CompilationProvider.Combine(typeCandidates.Collect());

        context.RegisterSourceOutput(combined, static (sourceProductionContext, tuple) =>
        {
            Compilation compilation = tuple.compilation;
            ImmutableArray<EnumTypeCandidate?> candidates = tuple.candidates;

            if (candidates.IsDefaultOrEmpty)
                return;

            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (EnumTypeCandidate? candidate in candidates)
            {
                if (candidate is null)
                    continue;

                INamedTypeSymbol enumType = candidate.EnumType;

                if (!seen.Add(enumType))
                    continue;

                ProcessCandidate(sourceProductionContext, compilation, enumType, candidate.ValueType);
            }
        });
    }

    private static EnumTypeCandidate? TryGetCandidate(GeneratorSyntaxContext syntaxContext)
    {
        if (syntaxContext.Node is not TypeDeclarationSyntax typeDeclaration)
            return null;

        if (syntaxContext.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
            return null;

        ImmutableArray<AttributeData> attributes = typeSymbol.GetAttributes();

        for (var i = 0; i < attributes.Length; i++)
        {
            AttributeData attribute = attributes[i];
            INamedTypeSymbol? attributeClass = attribute.AttributeClass;

            if (attributeClass is null)
                continue;

            if (attributeClass.Name == "EnumValueAttribute" && attributeClass.Arity == 0)
            {
                INamedTypeSymbol? intType = syntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("System.Int32");
                if (intType is null)
                    return null;

                return new EnumTypeCandidate(typeSymbol, intType);
            }

            if (attributeClass.Name == "EnumValueAttribute" && attributeClass.Arity == 1 && attributeClass.TypeArguments.Length == 1 &&
                attributeClass.TypeArguments[0] is INamedTypeSymbol genericValueType)
            {
                return new EnumTypeCandidate(typeSymbol, genericValueType);
            }
        }

        return null;
    }

    private static void ProcessCandidate(SourceProductionContext context, Compilation compilation, INamedTypeSymbol enumType, INamedTypeSymbol valueType)
    {
        if (enumType.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_typeMustBeTopLevelDescriptor, enumType.Locations.FirstOrDefault(), enumType.Name));
            return;
        }

        if (enumType.TypeParameters.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(_typeMustNotBeGenericDescriptor, enumType.Locations.FirstOrDefault(), enumType.Name));
            return;
        }

        if (!IsPartial(enumType))
        {
            context.ReportDiagnostic(Diagnostic.Create(_typeMustBePartialDescriptor, enumType.Locations.FirstOrDefault(), enumType.Name));
            return;
        }

        List<EnumInstance> instances = GatherInstances(compilation, enumType, valueType);

        if (instances.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(_noInstancesDescriptor, enumType.Locations.FirstOrDefault(), enumType.Name));
            return;
        }

        var seenValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (EnumInstance instance in instances)
        {
            if (!seenValues.Add(instance.ValueLiteral))
            {
                context.ReportDiagnostic(Diagnostic.Create(_duplicateValueDescriptor, instance.Location, enumType.Name, instance.ValueLiteral));
                return;
            }
        }

        bool hasValueProperty = HasValueProperty(enumType, valueType);
        bool hasValueConstructor = HasValueConstructor(enumType, valueType);

        bool supportsNewtonsoft = SupportsNewtonsoft(compilation);
        string source = BuildSource(enumType, valueType, instances, hasValueProperty, hasValueConstructor, supportsNewtonsoft);
        context.AddSource($"{enumType.Name}.EnumValues.g.cs", SourceText.From(source, Encoding.UTF8));
    }

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
            if (declarations[i].GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
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

    private static bool HasValueConstructor(INamedTypeSymbol enumType, INamedTypeSymbol valueType)
    {
        foreach (IMethodSymbol constructor in enumType.InstanceConstructors)
        {
            if (constructor.Parameters.Length != 1)
                continue;

            if (SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, valueType))
                return true;
        }

        return false;
    }

    private static List<EnumInstance> GatherInstances(Compilation compilation, INamedTypeSymbol enumType, INamedTypeSymbol valueType)
    {
        var result = new List<EnumInstance>();

        foreach (ISymbol member in enumType.GetMembers())
        {
            if (member is IFieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.IsStatic || !SymbolEqualityComparer.Default.Equals(fieldSymbol.Type, enumType))
                    continue;

                if (TryGetValueLiteralFromField(compilation, fieldSymbol, valueType, out string? valueLiteral, out string? stringValue, out Location? location))
                {
                    result.Add(new EnumInstance(fieldSymbol.Name, valueLiteral!, stringValue, location ?? fieldSymbol.Locations.FirstOrDefault() ?? Location.None));
                }
            }
            else if (member is IPropertySymbol propertySymbol)
            {
                if (!propertySymbol.IsStatic || !SymbolEqualityComparer.Default.Equals(propertySymbol.Type, enumType))
                    continue;

                if (TryGetValueLiteralFromProperty(compilation, propertySymbol, valueType, out string? valueLiteral, out string? stringValue, out Location? location))
                {
                    result.Add(new EnumInstance(propertySymbol.Name, valueLiteral!, stringValue, location ?? propertySymbol.Locations.FirstOrDefault() ?? Location.None));
                }
            }
        }

        result.Sort(static (left, right) => left.Location.SourceSpan.Start.CompareTo(right.Location.SourceSpan.Start));

        return result;
    }

    private static bool TryGetValueLiteralFromField(Compilation compilation, IFieldSymbol symbol, INamedTypeSymbol valueType, out string? valueLiteral, out string? stringValue, out Location? location)
    {
        valueLiteral = null;
        stringValue = null;
        location = null;

        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not VariableDeclaratorSyntax variableDeclarator)
                continue;

            EqualsValueClauseSyntax? initializer = variableDeclarator.Initializer;
            if (initializer is null)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(initializer.SyntaxTree);

            if (TryGetValueLiteralFromInitializer(semanticModel, initializer.Value, valueType, out valueLiteral, out stringValue))
            {
                location = initializer.GetLocation();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetValueLiteralFromProperty(Compilation compilation, IPropertySymbol symbol, INamedTypeSymbol valueType, out string? valueLiteral, out string? stringValue, out Location? location)
    {
        valueLiteral = null;
        stringValue = null;
        location = null;

        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not PropertyDeclarationSyntax propertyDeclaration)
                continue;

            ExpressionSyntax? expression = propertyDeclaration.Initializer?.Value ?? propertyDeclaration.ExpressionBody?.Expression;

            if (expression is null)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(expression.SyntaxTree);

            if (TryGetValueLiteralFromInitializer(semanticModel, expression, valueType, out valueLiteral, out stringValue))
            {
                location = expression.GetLocation();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetValueLiteralFromInitializer(SemanticModel semanticModel, ExpressionSyntax initializerExpression, INamedTypeSymbol valueType, out string? valueLiteral, out string? stringValue)
    {
        valueLiteral = null;
        stringValue = null;

        ArgumentListSyntax? argumentList = initializerExpression switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.ArgumentList,
            ImplicitObjectCreationExpressionSyntax implicitObjectCreation => implicitObjectCreation.ArgumentList,
            _ => null
        };

        if (argumentList is null || argumentList.Arguments.Count == 0)
            return false;

        ExpressionSyntax valueExpression = argumentList.Arguments[0].Expression;
        Optional<object?> constant = semanticModel.GetConstantValue(valueExpression);

        if (!constant.HasValue || constant.Value is null)
            return false;

        if (!TryConvertConstant(constant.Value, valueType, out object? converted))
            return false;

        if (!TryFormatLiteral(converted, valueType, out valueLiteral))
            return false;

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

    private static string BuildSource(INamedTypeSymbol enumType, INamedTypeSymbol valueType, List<EnumInstance> instances, bool hasValueProperty, bool hasValueConstructor, bool supportsNewtonsoft)
    {
        string enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string valueTypeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string ns = enumType.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : enumType.ContainingNamespace.ToDisplayString();

        string kind = enumType.TypeKind == TypeKind.Struct ? "struct" : "class";
        string stjConverterTypeName = enumType.Name + "JsonConverter";
        string newtonsoftConverterTypeName = enumType.Name + "NewtonsoftJsonConverter";
        string stjReadRawValueCode = BuildReadRawValueCode(valueType);
        string stjWriteValueCode = BuildWriteValueCode(valueType);
        bool isStringValue = valueType.SpecialType == SpecialType.System_String;
        bool useValueFrozen = isStringValue && instances.Count > _valueFrozenThreshold;
        bool useNameFrozen = instances.Count > _nameFrozenThreshold;
        string valueTryFromSignature = isStringValue ? "string? value" : valueTypeName + " value";
        List<(string, string Name)> valueItems = instances.Select(static instance => (instance.StringValue ?? string.Empty, instance.Name)).ToList();
        List<(string, string)> nameItems = instances.Select(static instance => (instance.Name, instance.Name)).ToList();

        var source = new StringBuilder();

        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            source.Append("namespace ").Append(ns).AppendLine(";");
            source.AppendLine();
        }

        source.Append("[global::System.Text.Json.Serialization.JsonConverter(typeof(").Append(stjConverterTypeName).AppendLine("))]");
        if (supportsNewtonsoft)
            source.Append("[global::Newtonsoft.Json.JsonConverter(typeof(").Append(newtonsoftConverterTypeName).AppendLine("))]");
        source.Append("public partial ").Append(kind).Append(" ").Append(enumType.Name).AppendLine();
        source.AppendLine("{");

        if (!hasValueProperty)
        {
            source.Append("    public readonly ").Append(valueTypeName).AppendLine(" Value;");
            source.AppendLine();
        }

        if (!hasValueConstructor)
        {
            source.Append("    private ").Append(enumType.Name).Append("(").Append(valueTypeName).AppendLine(" value)");
            source.AppendLine("    {");
            source.AppendLine("        Value = value;");
            source.AppendLine("    }");
            source.AppendLine();
        }

        bool emittedValueConstant = false;
        bool canEmitConstant = CanEmitConstant(valueType);

        for (var i = 0; i < instances.Count; i++)
        {
            string valueFieldName = instances[i].Name + "Value";
            if (!canEmitConstant || enumType.GetMembers(valueFieldName).Length > 0)
                continue;

            emittedValueConstant = true;
            source.Append("    public const ").Append(valueTypeName).Append(" ").Append(valueFieldName).Append(" = ").Append(instances[i].ValueLiteral).AppendLine(";");
        }

        if (emittedValueConstant)
            source.AppendLine();

        source.Append("    private static readonly ").Append(enumTypeName).AppendLine("[] __all =");
        source.AppendLine("    {");

        for (var i = 0; i < instances.Count; i++)
        {
            source.Append("        ").Append(instances[i].Name).AppendLine(",");
        }

        source.AppendLine("    };");
        source.AppendLine();

        if (useValueFrozen)
        {
            source.Append("    private static readonly global::System.Collections.Frozen.FrozenDictionary<string, ").Append(enumTypeName).AppendLine("> __valueMap =");
            source.AppendLine("        global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(");
            source.Append("            new global::System.Collections.Generic.Dictionary<string, ").Append(enumTypeName).Append(">(").Append(instances.Count)
                .AppendLine(", global::System.StringComparer.Ordinal)");
            source.AppendLine("            {");
            for (var i = 0; i < instances.Count; i++)
            {
                source.Append("                [").Append(instances[i].ValueLiteral).Append("] = ").Append(instances[i].Name).AppendLine(",");
            }
            source.AppendLine("            });");
            source.AppendLine();
        }

        if (useNameFrozen)
        {
            source.Append("    private static readonly global::System.Collections.Frozen.FrozenDictionary<string, ").Append(enumTypeName).AppendLine("> __nameMap =");
            source.AppendLine("        global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(");
            source.Append("            new global::System.Collections.Generic.Dictionary<string, ").Append(enumTypeName).Append(">(").Append(instances.Count)
                .AppendLine(", global::System.StringComparer.Ordinal)");
            source.AppendLine("            {");
            for (var i = 0; i < instances.Count; i++)
            {
                source.Append("                [\"").Append(instances[i].Name).Append("\"] = ").Append(instances[i].Name).AppendLine(",");
            }
            source.AppendLine("            });");
            source.AppendLine();
        }

        source.Append("    public static global::System.Collections.Generic.IReadOnlyList<").Append(enumTypeName).AppendLine("> List => __all;");
        source.AppendLine();
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static bool TryFromValue(").Append(isStringValue ? "string value" : valueTryFromSignature).Append(", out ").Append(enumTypeName).AppendLine(" result)");
        source.AppendLine("    {");
        if (isStringValue)
        {
            if (useValueFrozen)
            {
                source.AppendLine("        if (value is not null && __valueMap.TryGetValue(value, out result))");
                source.AppendLine("            return true;");
                source.AppendLine();
                source.AppendLine("        result = default!;");
                source.AppendLine("        return false;");
            }
            else
            {
                AppendStringSwitchBody(source, valueItems, "value", 2);
            }
        }
        else
        {
            source.AppendLine("        switch (value)");
            source.AppendLine("        {");
            for (var i = 0; i < instances.Count; i++)
            {
                source.Append("            case ").Append(instances[i].ValueLiteral).Append(": result = ").Append(instances[i].Name).AppendLine("; return true;");
            }
            source.AppendLine("            default:");
            source.AppendLine("                result = default!;");
            source.AppendLine("                return false;");
            source.AppendLine("        }");
        }
        source.AppendLine("    }");
        source.AppendLine();

        if (isStringValue)
        {
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool TryFromValue(global::System.ReadOnlySpan<char> value, out ").Append(enumTypeName).AppendLine(" result)");
            source.AppendLine("    {");
            AppendSpanSwitchBody(source, valueItems, "value", 2);
            source.AppendLine("    }");
            source.AppendLine();
        }

        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static ").Append(enumTypeName).Append(" FromValue(").Append(valueTypeName).AppendLine(" value)");
        source.AppendLine("    {");
        source.AppendLine("        if (TryFromValue(value, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.AppendLine("        throw new global::System.ArgumentOutOfRangeException(nameof(value), value, \"Unknown enum value.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static bool TryFromName(string name, out ").Append(enumTypeName).AppendLine(" result)");
        source.AppendLine("    {");
        if (useNameFrozen)
        {
            source.AppendLine("        if (name is not null && __nameMap.TryGetValue(name, out result))");
            source.AppendLine("            return true;");
            source.AppendLine();
            source.AppendLine("        result = default!;");
            source.AppendLine("        return false;");
        }
        else
        {
            AppendStringSwitchBody(source, nameItems, "name", 2);
        }
        source.AppendLine("    }");
        source.AppendLine();

        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static ").Append(enumTypeName).AppendLine(" FromName(string name)");
        source.AppendLine("    {");
        source.AppendLine("        if (TryFromName(name, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.AppendLine("        throw new global::System.ArgumentOutOfRangeException(nameof(name), name, \"Unknown enum name.\");");
        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine();
        source.Append("file sealed class ").Append(stjConverterTypeName).Append(" : global::System.Text.Json.Serialization.JsonConverter<").Append(enumTypeName).AppendLine(">");
        source.AppendLine("{");
        source.Append("    public override ").Append(enumTypeName)
            .AppendLine(" Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");
        if (isStringValue)
        {
            source.AppendLine("        if (reader.TokenType != global::System.Text.Json.JsonTokenType.String)");
            source.AppendLine("            throw new global::System.Text.Json.JsonException(\"Expected string value.\");");
            source.AppendLine();

            for (var i = 0; i < instances.Count; i++)
            {
                source.Append("        if (reader.ValueTextEquals(").Append(instances[i].ValueLiteral).Append("u8)) return ")
                    .Append(enumTypeName).Append(".").Append(instances[i].Name).AppendLine(";");
            }
        }
        else
        {
            source.Append(stjReadRawValueCode);
            source.AppendLine();
            source.Append("        if (").Append(enumTypeName).AppendLine(".TryFromValue(rawValue, out var result))");
            source.AppendLine("            return result;");
        }
        source.AppendLine();
        source.AppendLine("        throw new global::System.Text.Json.JsonException(\"Unknown enum value.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void Write(global::System.Text.Json.Utf8JsonWriter writer, ").Append(enumTypeName)
            .AppendLine(" value, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");

        source.Append("        ").Append(stjWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value")).AppendLine();
        source.AppendLine("    }");
        source.AppendLine("}");

        if (supportsNewtonsoft)
        {
            string newtonsoftReadRawValueCode = BuildNewtonsoftReadRawValueCode(valueType);
            string newtonsoftWriteValueCode = BuildNewtonsoftWriteValueCode(valueType);
            string readReturnType = enumType.IsReferenceType ? enumTypeName + "?" : enumTypeName;
            string existingValueType = enumType.IsReferenceType ? enumTypeName + "?" : enumTypeName;
            string writeValueType = enumType.IsReferenceType ? enumTypeName + "?" : enumTypeName;

            source.AppendLine();
            source.Append("file sealed class ").Append(newtonsoftConverterTypeName).Append(" : global::Newtonsoft.Json.JsonConverter<").Append(enumTypeName).AppendLine(">");
            source.AppendLine("{");
            source.Append("    public override ").Append(readReturnType).Append(" ReadJson(global::Newtonsoft.Json.JsonReader reader, global::System.Type objectType, ").Append(existingValueType)
                .AppendLine(" existingValue, bool hasExistingValue, global::Newtonsoft.Json.JsonSerializer serializer)");
            source.AppendLine("    {");
            if (enumType.IsReferenceType)
            {
                source.AppendLine("        if (reader.TokenType == global::Newtonsoft.Json.JsonToken.Null)");
                source.AppendLine("            return null;");
                source.AppendLine();
            }
            else
            {
                source.AppendLine("        if (reader.TokenType == global::Newtonsoft.Json.JsonToken.Null)");
                source.AppendLine("            throw new global::Newtonsoft.Json.JsonSerializationException(\"Cannot convert null to a value type enum value.\");");
                source.AppendLine();
            }
            source.Append(newtonsoftReadRawValueCode);
            source.AppendLine();
            source.Append("        if (").Append(enumTypeName).AppendLine(".TryFromValue(rawValue, out var result))");
            source.AppendLine("            return result;");
            source.AppendLine();
            source.AppendLine("        throw new global::Newtonsoft.Json.JsonSerializationException(\"Unknown enum value.\");");
            source.AppendLine("    }");
            source.AppendLine();
            source.Append("    public override void WriteJson(global::Newtonsoft.Json.JsonWriter writer, ").Append(writeValueType)
                .AppendLine(" value, global::Newtonsoft.Json.JsonSerializer serializer)");
            source.AppendLine("    {");
            if (enumType.IsReferenceType)
            {
                source.AppendLine("        if (value is null)");
                source.AppendLine("        {");
                source.AppendLine("            writer.WriteNull();");
                source.AppendLine("            return;");
                source.AppendLine("        }");
                source.AppendLine();
            }
            source.Append("        ").Append(newtonsoftWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value")).AppendLine();
            source.AppendLine("    }");
            source.AppendLine("}");
        }

        return source.ToString();
    }

    private static string BuildReadRawValueCode(ITypeSymbol valueType)
    {
        string typeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
                return "        if (!reader.TryGetInt32(out int rawValue)) throw new global::System.Text.Json.JsonException(\"Expected int value.\");";
            case SpecialType.System_Int64:
                return "        if (!reader.TryGetInt64(out long rawValue)) throw new global::System.Text.Json.JsonException(\"Expected long value.\");";
            case SpecialType.System_Int16:
                return "        if (!reader.TryGetInt16(out short rawValue)) throw new global::System.Text.Json.JsonException(\"Expected short value.\");";
            case SpecialType.System_Byte:
                return "        if (!reader.TryGetByte(out byte rawValue)) throw new global::System.Text.Json.JsonException(\"Expected byte value.\");";
            case SpecialType.System_SByte:
                return "        if (!reader.TryGetSByte(out sbyte rawValue)) throw new global::System.Text.Json.JsonException(\"Expected sbyte value.\");";
            case SpecialType.System_UInt16:
                return "        if (!reader.TryGetUInt16(out ushort rawValue)) throw new global::System.Text.Json.JsonException(\"Expected ushort value.\");";
            case SpecialType.System_UInt32:
                return "        if (!reader.TryGetUInt32(out uint rawValue)) throw new global::System.Text.Json.JsonException(\"Expected uint value.\");";
            case SpecialType.System_UInt64:
                return "        if (!reader.TryGetUInt64(out ulong rawValue)) throw new global::System.Text.Json.JsonException(\"Expected ulong value.\");";
            case SpecialType.System_String:
                return "        string? rawValue = reader.GetString(); if (rawValue is null) throw new global::System.Text.Json.JsonException(\"Expected string value.\");";
            case SpecialType.System_Char:
                return "        string? charText = reader.GetString(); if (string.IsNullOrEmpty(charText) || charText.Length != 1) throw new global::System.Text.Json.JsonException(\"Expected char value.\"); char rawValue = charText[0];";
            case SpecialType.System_Boolean:
                return "        if (reader.TokenType != global::System.Text.Json.JsonTokenType.True && reader.TokenType != global::System.Text.Json.JsonTokenType.False) throw new global::System.Text.Json.JsonException(\"Expected bool value.\"); bool rawValue = reader.GetBoolean();";
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                    return "        if (reader.TokenType != global::System.Text.Json.JsonTokenType.String) throw new global::System.Text.Json.JsonException(\"Expected Guid string value.\"); global::System.Guid rawValue = reader.GetGuid();";

                return "        " + typeName + " rawValue = global::System.Text.Json.JsonSerializer.Deserialize<" + typeName +
                       ">(ref reader, options)!;";
            }
        }
    }

    private static void AppendStringDecisionTreeBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel,
        bool writeNoMatchTail = true, int? maxLength = null)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);
        string branchIndent = new(' ', (indentLevel + 2) * 4);

        IEnumerable<IGrouping<int, (string Text, string TargetName)>> groups = items
            .Where(static item => item.Text.Length >= 0)
            .GroupBy(static item => item.Text.Length)
            .OrderBy(static group => group.Key);

        if (maxLength.HasValue)
            groups = groups.Where(group => group.Key <= maxLength.Value);

        source.Append(indent).Append("switch (").Append(inputIdentifier).AppendLine(".Length)");
        source.Append(indent).AppendLine("{");

        foreach (IGrouping<int, (string Text, string TargetName)> group in groups)
        {
            source.Append(innerIndent).Append("case ").Append(group.Key).AppendLine(":");

            if (group.Key == 1)
            {
                source.Append(branchIndent).Append("char c0 = ").Append(inputIdentifier).AppendLine("[0];");
            }

            foreach ((string text, string targetName) in group)
            {
                if (group.Key == 0)
                {
                    source.Append(branchIndent).Append("result = ").Append(targetName).AppendLine(";");
                    source.Append(branchIndent).AppendLine("return true;");
                    continue;
                }

                source.Append(branchIndent).Append("if (");

                for (var i = 0; i < text.Length; i++)
                {
                    if (i > 0)
                        source.Append(" && ");

                    if (group.Key == 1 && i == 0)
                        source.Append("c0 == '").Append(EscapeChar(text[i])).Append("'");
                    else
                        source.Append(inputIdentifier).Append("[").Append(i).Append("] == '").Append(EscapeChar(text[i])).Append("'");
                }

                source.AppendLine(")");
                source.Append(branchIndent).AppendLine("{");
                source.Append(branchIndent).Append("    result = ").Append(targetName).AppendLine(";");
                source.Append(branchIndent).AppendLine("    return true;");
                source.Append(branchIndent).AppendLine("}");
            }

            source.Append(branchIndent).AppendLine("break;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(branchIndent).AppendLine("break;");
        source.Append(indent).AppendLine("}");

        if (writeNoMatchTail)
        {
            source.AppendLine();
            source.Append(indent).AppendLine("result = default!;");
            source.Append(indent).AppendLine("return false;");
        }
    }

    private static void AppendStringSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);

        source.Append(indent).Append("switch (").Append(inputIdentifier).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach ((string text, string targetName) in items)
        {
            source.Append(innerIndent).Append("case \"").Append(EscapeString(text)).Append("\": result = ").Append(targetName).AppendLine("; return true;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(innerIndent).AppendLine("    result = default!;");
        source.Append(innerIndent).AppendLine("    return false;");
        source.Append(indent).AppendLine("}");
    }

    private static void AppendSpanSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);

        source.Append(indent).Append("switch (").Append(inputIdentifier).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach ((string text, string targetName) in items)
        {
            source.Append(innerIndent).Append("case \"").Append(EscapeString(text)).Append("\": result = ").Append(targetName).AppendLine("; return true;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(innerIndent).AppendLine("    result = default!;");
        source.Append(innerIndent).AppendLine("    return false;");
        source.Append(indent).AppendLine("}");
    }

    private static void AppendStringFirstCharSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel,
        bool writeNoMatchTail = true, int? maxLength = null, string? lengthExpression = null)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);
        string branchIndent = new(' ', (indentLevel + 2) * 4);
        string leafIndent = new(' ', (indentLevel + 3) * 4);
        string lenExpression = lengthExpression ?? inputIdentifier + ".Length";

        IEnumerable<IGrouping<int, (string Text, string TargetName)>> lengthGroups = items
            .GroupBy(static item => item.Text.Length)
            .OrderBy(static group => group.Key);

        if (maxLength.HasValue)
            lengthGroups = lengthGroups.Where(group => group.Key <= maxLength.Value);

        source.Append(indent).Append("switch (").Append(lenExpression).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach (IGrouping<int, (string Text, string TargetName)> lengthGroup in lengthGroups)
        {
            source.Append(innerIndent).Append("case ").Append(lengthGroup.Key).AppendLine(":");
            List<(string Text, string TargetName)> lengthCandidates = lengthGroup.ToList();

            if (lengthCandidates.Count == 1)
            {
                (string text, string targetName) = lengthCandidates[0];
                source.Append(branchIndent).Append("if (").Append(inputIdentifier).Append(" == \"").Append(EscapeString(text)).AppendLine("\")");
                source.Append(branchIndent).AppendLine("{");
                source.Append(branchIndent).Append("    result = ").Append(targetName).AppendLine(";");
                source.Append(branchIndent).AppendLine("    return true;");
                source.Append(branchIndent).AppendLine("}");
                source.Append(branchIndent).AppendLine("break;");
                continue;
            }

            source.Append(branchIndent).Append("switch (").Append(inputIdentifier).AppendLine("[0])");
            source.Append(branchIndent).AppendLine("{");

            IEnumerable<IGrouping<char, (string Text, string TargetName)>> firstCharGroups = lengthGroup
                .GroupBy(static item => item.Text[0])
                .OrderBy(static group => group.Key);

            foreach (IGrouping<char, (string Text, string TargetName)> firstCharGroup in firstCharGroups)
            {
                source.Append(leafIndent).Append("case '").Append(EscapeChar(firstCharGroup.Key)).AppendLine("':");

                if (lengthGroup.Key == 1)
                {
                    (_, string targetName) = firstCharGroup.First();
                    source.Append(leafIndent).Append("    result = ").Append(targetName).AppendLine(";");
                    source.Append(leafIndent).AppendLine("    return true;");
                }
                else
                {
                    foreach ((string text, string targetName) in firstCharGroup)
                    {
                        source.Append(leafIndent).Append("    if (").Append(inputIdentifier).Append(" == \"").Append(EscapeString(text)).AppendLine("\")");
                        source.Append(leafIndent).Append("    {");
                        source.AppendLine();
                        source.Append(leafIndent).Append("        result = ").Append(targetName).AppendLine(";");
                        source.Append(leafIndent).AppendLine("        return true;");
                        source.Append(leafIndent).AppendLine("    }");
                    }
                    source.Append(leafIndent).AppendLine("    break;");
                }
            }

            source.Append(leafIndent).AppendLine("default:");
            source.Append(leafIndent).AppendLine("    break;");
            source.Append(branchIndent).AppendLine("}");
            source.Append(branchIndent).AppendLine("break;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(branchIndent).AppendLine("break;");
        source.Append(indent).AppendLine("}");

        if (writeNoMatchTail)
        {
            source.AppendLine();
            source.Append(indent).AppendLine("result = default!;");
            source.Append(indent).AppendLine("return false;");
        }
    }

    private static void AppendSpanFirstCharSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel,
        bool writeNoMatchTail = true, int? maxLength = null, string? lengthExpression = null)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);
        string branchIndent = new(' ', (indentLevel + 2) * 4);
        string leafIndent = new(' ', (indentLevel + 3) * 4);
        string lenExpression = lengthExpression ?? inputIdentifier + ".Length";

        IEnumerable<IGrouping<int, (string Text, string TargetName)>> lengthGroups = items
            .GroupBy(static item => item.Text.Length)
            .OrderBy(static group => group.Key);

        if (maxLength.HasValue)
            lengthGroups = lengthGroups.Where(group => group.Key <= maxLength.Value);

        source.Append(indent).Append("switch (").Append(lenExpression).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach (IGrouping<int, (string Text, string TargetName)> lengthGroup in lengthGroups)
        {
            source.Append(innerIndent).Append("case ").Append(lengthGroup.Key).AppendLine(":");
            List<(string Text, string TargetName)> lengthCandidates = lengthGroup.ToList();

            if (lengthCandidates.Count == 1)
            {
                (string text, string targetName) = lengthCandidates[0];
                source.Append(branchIndent).Append("if (global::System.MemoryExtensions.SequenceEqual(").Append(inputIdentifier)
                    .Append(", global::System.MemoryExtensions.AsSpan(\"").Append(EscapeString(text)).AppendLine("\")))");
                source.Append(branchIndent).AppendLine("{");
                source.Append(branchIndent).Append("    result = ").Append(targetName).AppendLine(";");
                source.Append(branchIndent).AppendLine("    return true;");
                source.Append(branchIndent).AppendLine("}");
                source.Append(branchIndent).AppendLine("break;");
                continue;
            }

            source.Append(branchIndent).Append("switch (").Append(inputIdentifier).AppendLine("[0])");
            source.Append(branchIndent).AppendLine("{");

            IEnumerable<IGrouping<char, (string Text, string TargetName)>> firstCharGroups = lengthGroup
                .GroupBy(static item => item.Text[0])
                .OrderBy(static group => group.Key);

            foreach (IGrouping<char, (string Text, string TargetName)> firstCharGroup in firstCharGroups)
            {
                source.Append(leafIndent).Append("case '").Append(EscapeChar(firstCharGroup.Key)).AppendLine("':");

                if (lengthGroup.Key == 1)
                {
                    (_, string targetName) = firstCharGroup.First();
                    source.Append(leafIndent).Append("    result = ").Append(targetName).AppendLine(";");
                    source.Append(leafIndent).AppendLine("    return true;");
                }
                else
                {
                    foreach ((string text, string targetName) in firstCharGroup)
                    {
                        source.Append(leafIndent).Append("    if (global::System.MemoryExtensions.SequenceEqual(").Append(inputIdentifier)
                            .Append(", global::System.MemoryExtensions.AsSpan(\"").Append(EscapeString(text)).AppendLine("\")))");
                        source.Append(leafIndent).Append("    {");
                        source.AppendLine();
                        source.Append(leafIndent).Append("        result = ").Append(targetName).AppendLine(";");
                        source.Append(leafIndent).AppendLine("        return true;");
                        source.Append(leafIndent).AppendLine("    }");
                    }
                    source.Append(leafIndent).AppendLine("    break;");
                }
            }

            source.Append(leafIndent).AppendLine("default:");
            source.Append(leafIndent).AppendLine("    break;");
            source.Append(branchIndent).AppendLine("}");
            source.Append(branchIndent).AppendLine("break;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(branchIndent).AppendLine("break;");
        source.Append(indent).AppendLine("}");

        if (writeNoMatchTail)
        {
            source.AppendLine();
            source.Append(indent).AppendLine("result = default!;");
            source.Append(indent).AppendLine("return false;");
        }
    }

    private static string BuildWriteValueCode(ITypeSymbol valueType)
    {
        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Int16:
            case SpecialType.System_Int64:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
                return "writer.WriteNumberValue({VALUE_EXPRESSION});";
            case SpecialType.System_String:
                return "writer.WriteStringValue({VALUE_EXPRESSION});";
            case SpecialType.System_Char:
                return "writer.WriteStringValue({VALUE_EXPRESSION}.ToString());";
            case SpecialType.System_Boolean:
                return "writer.WriteBooleanValue({VALUE_EXPRESSION});";
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                    return "writer.WriteStringValue({VALUE_EXPRESSION});";

                return "global::System.Text.Json.JsonSerializer.Serialize(writer, " + "{VALUE_EXPRESSION}" + ", options);";
            }
        }
    }

    private static string BuildNewtonsoftReadRawValueCode(ITypeSymbol valueType)
    {
        string typeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
                return "        int rawValue = global::System.Convert.ToInt32(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_Int64:
                return "        long rawValue = global::System.Convert.ToInt64(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_Int16:
                return "        short rawValue = global::System.Convert.ToInt16(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_Byte:
                return "        byte rawValue = global::System.Convert.ToByte(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_SByte:
                return "        sbyte rawValue = global::System.Convert.ToSByte(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_UInt16:
                return "        ushort rawValue = global::System.Convert.ToUInt16(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_UInt32:
                return "        uint rawValue = global::System.Convert.ToUInt32(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_UInt64:
                return "        ulong rawValue = global::System.Convert.ToUInt64(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            case SpecialType.System_String:
                return "        if (reader.TokenType != global::Newtonsoft.Json.JsonToken.String) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected string value.\"); string rawValue = (string?)reader.Value ?? throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected string value.\");";
            case SpecialType.System_Char:
                return "        if (reader.TokenType != global::Newtonsoft.Json.JsonToken.String) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected char value.\"); string charText = (string?)reader.Value ?? throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected char value.\"); if (charText.Length != 1) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected single-character value.\"); char rawValue = charText[0];";
            case SpecialType.System_Boolean:
                return "        bool rawValue = global::System.Convert.ToBoolean(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                    return "        if (reader.TokenType != global::Newtonsoft.Json.JsonToken.String) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected Guid string value.\"); string guidText = (string?)reader.Value ?? throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected Guid string value.\"); global::System.Guid rawValue = global::System.Guid.Parse(guidText);";

                return "        " + typeName + " rawValue = serializer.Deserialize<" + typeName + ">(reader)!;";
            }
        }
    }

    private static string BuildNewtonsoftWriteValueCode(ITypeSymbol valueType)
    {
        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Int16:
            case SpecialType.System_Int64:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_String:
            case SpecialType.System_Char:
            case SpecialType.System_Boolean:
                return "writer.WriteValue({VALUE_EXPRESSION});";
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                    return "writer.WriteValue({VALUE_EXPRESSION});";

                return "serializer.Serialize(writer, " + "{VALUE_EXPRESSION}" + ");";
            }
        }
    }

    private static bool CanEmitConstant(ITypeSymbol valueType)
    {
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
            case SpecialType.System_String:
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
                return true;
            default:
                return false;
        }
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static string EscapeChar(char value)
    {
        return value switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            _ => value.ToString()
        };
    }

    private const string _attributeSource = """
                                           // <auto-generated/>
                                           #nullable enable

                                           namespace Soenneker.Gen.EnumValues;

                                           [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                                           public sealed class EnumValueAttribute : global::System.Attribute
                                           {
                                           }

                                           [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                                           public sealed class EnumValueAttribute<TValue> : global::System.Attribute
                                           {
                                           }
                                           """;
}
