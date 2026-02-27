using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Soenneker.Gen.EnumValues.Dtos;

namespace Soenneker.Gen.EnumValues;

[Generator]
public sealed partial class EnumValueSourceGenerator : IIncrementalGenerator
{

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

    private static readonly DiagnosticDescriptor _ordinalNotAllowedDescriptor = new(
        id: "SEV006",
        title: "Ordinal argument not allowed",
        messageFormat: "Do not specify an ordinal; use new(\"{0}\") not new(\"{0}\", id). Ordinals are assigned automatically by the generator.",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _duplicateValueFromIncludedDescriptor = new(
        id: "SEV007",
        title: "Duplicate enum value from included type",
        messageFormat: "Duplicate enum value '{0}' in {1} from {2}",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _nameCollisionWithIncludedDescriptor = new(
        id: "SEV008",
        title: "Member name conflicts with included type",
        messageFormat: "Member name '{0}' in {1} conflicts with included member from {2}",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _includeEnumValuesTypeInvalidDescriptor = new(
        id: "SEV009",
        title: "IncludeEnumValues source type is not valid",
        messageFormat: "[IncludeEnumValues] source type '{0}' must be an [EnumValue] or [EnumValue<T>] type with the same value type as '{1}'",
        category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _invalidConstructorDescriptor = new(
        id: "SEV010",
        title: "Invalid constructor on enum value type",
        messageFormat: "Type '{0}' declares constructor '{1}' which would make generated enum values open. Remove custom constructors and let the generator emit the private (value, id) constructor.",
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

    private static ImmutableArray<INamedTypeSymbol> GetIncludeEnumValuesSourceTypes(INamedTypeSymbol enumType)
    {
        var list = new List<INamedTypeSymbol>();
        foreach (AttributeData attribute in enumType.GetAttributes())
        {
            if (attribute.AttributeClass?.Name != "IncludeEnumValuesAttribute")
                continue;
            if (attribute.ConstructorArguments.Length == 0)
                continue;
            TypedConstant arg = attribute.ConstructorArguments[0];
            if (arg.Kind != TypedConstantKind.Type || arg.Value is not INamedTypeSymbol sourceType)
                continue;
            list.Add(sourceType);
        }
        return list.ToImmutableArray();
    }

    private static bool TryGetEnumValueValueType(INamedTypeSymbol type, Compilation compilation, out INamedTypeSymbol? valueType)
    {
        valueType = null;
        foreach (AttributeData attribute in type.GetAttributes())
        {
            INamedTypeSymbol? attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
                continue;
            if (attributeClass.Name == "EnumValueAttribute" && attributeClass.Arity == 0)
            {
                valueType = compilation.GetTypeByMetadataName("System.Int32") as INamedTypeSymbol;
                return valueType is not null;
            }
            if (attributeClass.Name == "EnumValueAttribute" && attributeClass.Arity == 1 && attributeClass.TypeArguments.Length == 1 &&
                attributeClass.TypeArguments[0] is INamedTypeSymbol genericValueType)
            {
                valueType = genericValueType;
                return true;
            }
        }
        return false;
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

        IMethodSymbol? invalidCtor = GetInvalidOpenConstructor(enumType);
        if (invalidCtor is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_invalidConstructorDescriptor, invalidCtor.Locations.FirstOrDefault(),
                enumType.Name, invalidCtor.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        List<EnumInstance> ownInstances = GatherInstances(context, compilation, enumType, valueType);

        List<EnumInstance> instances = new List<EnumInstance>(ownInstances);
        ImmutableArray<INamedTypeSymbol> includeTypes = GetIncludeEnumValuesSourceTypes(enumType);
        foreach (INamedTypeSymbol sourceType in includeTypes)
        {
            if (!TryGetEnumValueValueType(sourceType, compilation, out INamedTypeSymbol? sourceValueType) ||
                !SymbolEqualityComparer.Default.Equals(sourceValueType, valueType))
            {
                context.ReportDiagnostic(Diagnostic.Create(_includeEnumValuesTypeInvalidDescriptor,
                    enumType.Locations.FirstOrDefault(), sourceType.Name, enumType.Name));
                return;
            }

            List<EnumInstance> included = GatherInstancesFromType(context, compilation, sourceType, valueType, sourceType.Name);
            foreach (EnumInstance inst in included)
                instances.Add(new EnumInstance(inst.Name, inst.ValueLiteral, inst.StringValue, inst.Location, id: null, inst.SourceTypeName, inst.ValueJsonString));
        }

        if (instances.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(_noInstancesDescriptor, enumType.Locations.FirstOrDefault(), enumType.Name));
            return;
        }

        var seenValues = new HashSet<string>(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (EnumInstance instance in instances)
        {
            if (!seenValues.Add(instance.ValueLiteral))
            {
                if (instance.SourceTypeName is { } fromType)
                    context.ReportDiagnostic(Diagnostic.Create(_duplicateValueFromIncludedDescriptor, instance.Location, instance.ValueLiteral, enumType.Name, fromType));
                else
                    context.ReportDiagnostic(Diagnostic.Create(_duplicateValueDescriptor, instance.Location, enumType.Name, instance.ValueLiteral));
                return;
            }

            if (!seenNames.Add(instance.Name))
            {
                if (instance.SourceTypeName is { } fromType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(_nameCollisionWithIncludedDescriptor, instance.Location, instance.Name, enumType.Name, fromType));
                    return;
                }
            }
        }

        bool hasValueProperty = HasValueProperty(enumType, valueType);
        bool hasValueIdConstructor = HasValueIdConstructor(enumType, valueType);
        bool hasNameProperty = HasNameProperty(enumType);

        bool supportsNewtonsoft = SupportsNewtonsoft(compilation);
        string source = BuildSource(enumType, valueType, instances, hasValueProperty, hasValueIdConstructor, hasNameProperty, supportsNewtonsoft);
        context.AddSource($"{enumType.Name}.EnumValues.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static void AppendXmlSummary(StringBuilder source, string indent, string text)
    {
        source.Append(indent).AppendLine("/// <summary>");
        source.Append(indent).Append("/// ").AppendLine(text);
        source.Append(indent).AppendLine("/// </summary>");
    }

    private static string BuildSource(INamedTypeSymbol enumType, INamedTypeSymbol valueType, List<EnumInstance> instances, bool hasValueProperty, bool hasValueIdConstructor, bool hasNameProperty, bool supportsNewtonsoft)
    {
        string enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string valueTypeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string ns = enumType.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : enumType.ContainingNamespace.ToDisplayString();
        bool isStringValue = valueType.SpecialType == SpecialType.System_String;
        bool useIdBacking = isStringValue;

        var ctx = new EnumSourceBuildContext(
            enumType,
            valueType,
            instances,
            hasValueProperty,
            hasValueIdConstructor,
            hasNameProperty,
            supportsNewtonsoft,
            enumTypeName,
            valueTypeName,
            ns,
            enumType.TypeKind == TypeKind.Struct ? "struct" : "class",
            enumType.Name + "JsonConverter",
            enumType.Name + "NewtonsoftJsonConverter",
            enumType.Name + "TypeConverter",
            isStringValue,
            useIdBacking,
            isStringValue ? "string? value" : valueTypeName + " value",
            isStringValue
                ? instances.Select(static instance => (instance.Name + "Value", instance.Name)).ToList()
                : instances.Select(static instance => (instance.StringValue ?? string.Empty, instance.Name)).ToList(),
            instances.Select(static instance => (instance.Name + "Name", instance.Name)).ToList(),
            instances.Select(static instance => (instance.Name, instance.Name)).ToList(),
            isStringValue
                ? instances.Select(static instance => (instance.StringValue ?? string.Empty, instance.Name)).ToList()
                : new List<(string Text, string TargetName)>(),
            BuildReadRawValueCode(valueType),
            BuildWriteValueCode(valueType));

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        if (!string.IsNullOrEmpty(ns))
        {
            source.Append("namespace ").Append(ns).AppendLine(";");
            source.AppendLine();
        }

        AppendTypeDeclaration(source, ctx);
        AppendValueNameConstructorsAndName(source, ctx);
        AppendConstantsAllAndList(source, ctx);
        AppendParsingMethods(source, ctx);
        AppendEqualityAndOperators(source, ctx);
        AppendThrowHelperAndConverters(source, ctx);

        return source.ToString();
    }

    private static void AppendIsDefinedIsNameDefined(StringBuilder source, string enumTypeName, string valueTypeName, bool isStringValue)
    {
        if (isStringValue)
        {
            AppendXmlSummary(source, "    ", "Returns whether a value is defined for the given string.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool IsDefined(string? value) => ").Append(enumTypeName).AppendLine(".TryFromValue(value, out _);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns whether a value is defined for the given span.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool IsDefined(global::System.ReadOnlySpan<char> value) => ").Append(enumTypeName).AppendLine(".TryFromValue(value, out _);");
            source.AppendLine();
        }
        else
        {
            AppendXmlSummary(source, "    ", "Returns whether the given value is defined.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool IsDefined(").Append(valueTypeName).Append(" value) => ").Append(enumTypeName).AppendLine(".TryFromValue(value, out _);");
            source.AppendLine();
        }
        AppendXmlSummary(source, "    ", "Returns whether a name is defined.");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static bool IsNameDefined(string? name) => ").Append(enumTypeName).AppendLine(".TryFromName(name, out _);");
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Returns whether a name is defined for the given span.");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static bool IsNameDefined(global::System.ReadOnlySpan<char> name) => ").Append(enumTypeName).AppendLine(".TryFromName(name, out _);");
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
        string lenExpression = lengthExpression ?? inputIdentifier + ".Length";
        const string memExt = "global::System.MemoryExtensions";

        IEnumerable<IGrouping<int, (string Text, string TargetName)>> lengthGroups = items
            .GroupBy(static item => item.Text.Length)
            .OrderBy(static group => group.Key);

        if (maxLength.HasValue)
            lengthGroups = lengthGroups.Where(group => group.Key <= maxLength.Value);

        source.Append(indent).Append("switch (").Append(lenExpression).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach (IGrouping<int, (string Text, string TargetName)> lengthGroup in lengthGroups)
        {
            int len = lengthGroup.Key;
            source.Append(innerIndent).Append("case ").Append(len).AppendLine(":");
            source.Append(branchIndent).AppendLine("{");
            List<(string Text, string TargetName)> lengthCandidates = lengthGroup.ToList();

            if (lengthCandidates.Count == 1)
            {
                (string text, string targetName) = lengthCandidates[0];
                if (len == 1)
                    source.Append(branchIndent).Append("    if (").Append(inputIdentifier).Append("[0] == '").Append(EscapeChar(text[0])).AppendLine("')");
                else
                    source.Append(branchIndent).Append("    if (").Append(memExt).Append(".SequenceEqual(").Append(inputIdentifier).Append(", ").Append(memExt).Append(".AsSpan(\"").Append(EscapeString(text)).AppendLine("\")))");
                source.Append(branchIndent).AppendLine("    {");
                source.Append(branchIndent).Append("        result = ").Append(targetName).AppendLine(";");
                source.Append(branchIndent).AppendLine("        return true;");
                source.Append(branchIndent).AppendLine("    }");
                source.Append(branchIndent).AppendLine("    break;");
                source.Append(branchIndent).AppendLine("}");
                continue;
            }

            source.Append(branchIndent).Append("    char c0 = ").Append(inputIdentifier).AppendLine("[0];");
            foreach ((string text, string targetName) in lengthCandidates)
            {
                if (len == 1)
                    source.Append(branchIndent).Append("    if (c0 == '").Append(EscapeChar(text[0])).AppendLine("')");
                else
                    source.Append(branchIndent).Append("    if (c0 == '").Append(EscapeChar(text[0])).Append("' && ").Append(memExt).Append(".SequenceEqual(").Append(inputIdentifier).Append(", ").Append(memExt).Append(".AsSpan(\"").Append(EscapeString(text)).AppendLine("\")))");
                source.Append(branchIndent).AppendLine("    {");
                source.Append(branchIndent).Append("        result = ").Append(targetName).AppendLine(";");
                source.Append(branchIndent).AppendLine("        return true;");
                source.Append(branchIndent).AppendLine("    }");
            }
            source.Append(branchIndent).AppendLine("    break;");
            source.Append(branchIndent).AppendLine("}");
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

    private static string BuildToStringExpression(ITypeSymbol valueType)
    {
        // Guid and other non-primitives typically don't have ToString(IFormatProvider)
        if (valueType.ToDisplayString() == "System.Guid")
            return "Value.ToString()";
        return "Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)!";
    }

    /// <summary>Returns the default-case body for WriteAsPropertyName (unknown value): Utf8Formatter or ToString().</summary>
    private static string BuildStjWritePropertyNameFallback(ITypeSymbol valueType)
    {
        int bufferSize = valueType.SpecialType switch
        {
            SpecialType.System_Int32 => 11,
            SpecialType.System_Int64 => 20,
            SpecialType.System_Int16 => 6,
            SpecialType.System_Byte => 3,
            SpecialType.System_SByte => 4,
            SpecialType.System_UInt16 => 5,
            SpecialType.System_UInt32 => 10,
            SpecialType.System_UInt64 => 20,
            _ => 0
        };
        if (bufferSize > 0)
        {
            return "            default:\n" +
                   "                global::System.Span<byte> buf = stackalloc byte[" + bufferSize + "];\n" +
                   "                if (!global::System.Buffers.Text.Utf8Formatter.TryFormat(value.Value, buf, out int written))\n" +
                   "                    throw new global::System.Text.Json.JsonException(\"Unknown enum value.\");\n" +
                   "                writer.WritePropertyName(buf[..written]);\n" +
                   "                return;";
        }
        return "            default:\n" +
               "                throw new global::System.Text.Json.JsonException(\"Unknown enum value.\");";
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

    private const string _attributeSource = """
                                           // <auto-generated/>
                                           #nullable enable

                                           namespace Soenneker.Gen.EnumValues;

                                           /// <summary>
                                           /// Marks a class or struct for source generation of enum value helpers (names, values, try-from methods).
                                           /// </summary>
                                           [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                                           public sealed class EnumValueAttribute : global::System.Attribute
                                           {
                                           }

                                           /// <summary>
                                           /// Marks a class or struct for source generation of enum value helpers with a specific value type <typeparamref name="TValue"/>.
                                           /// </summary>
                                           /// <typeparam name="TValue">The type of the enum's underlying value (e.g. int, long, string).</typeparam>
                                           [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                                           public sealed class EnumValueAttribute<TValue> : global::System.Attribute
                                           {
                                           }

                                           /// <summary>
                                           /// Includes enum members from another type in the generated values for the attributed type.
                                           /// </summary>
                                           [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
                                           public sealed class IncludeEnumValuesAttribute : global::System.Attribute
                                           {
                                               /// <summary>
                                               /// The type whose enum values are included (e.g. another enum or enum-value type).
                                               /// </summary>
                                               public global::System.Type SourceType { get; }

                                               /// <summary>
                                               /// Includes enum values from the specified type.
                                               /// </summary>
                                               /// <param name="sourceType">The type to include values from.</param>
                                               public IncludeEnumValuesAttribute(global::System.Type sourceType) => SourceType = sourceType;
                                           }
                                           """;
}
