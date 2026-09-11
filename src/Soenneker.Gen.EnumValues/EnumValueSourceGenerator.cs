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

/// <summary>
/// Represents the enum value source generator.
/// </summary>
[Generator]
public sealed partial class EnumValueSourceGenerator : IIncrementalGenerator
{
    private const string _inliningPropertyName = "build_property.EnumValuesInlining";

    private static readonly DiagnosticDescriptor _typeMustBePartialDescriptor = new(id: "SEV001", title: "EnumValue type must be partial",
        messageFormat: "Type '{0}' must be declared partial to use [EnumValue]", category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _typeMustBeTopLevelDescriptor = new(id: "SEV002", title: "EnumValue type must be top-level",
        messageFormat: "Type '{0}' is nested and not supported by [EnumValue]", category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _typeMustNotBeGenericDescriptor = new(id: "SEV003", title: "EnumValue type must not be generic",
        messageFormat: "Type '{0}' is generic and not supported by [EnumValue]", category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _noInstancesDescriptor = new(id: "SEV004", title: "No enum value instances found",
        messageFormat: "Type '{0}' has [EnumValue] but no static instances with compile-time constant values were discovered", category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _duplicateValueDescriptor = new(id: "SEV005", title: "Duplicate enum value detected",
        messageFormat: "Type '{0}' has duplicate enum value '{1}'", category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _ordinalNotAllowedDescriptor = new(id: "SEV006", title: "Ordinal argument not allowed",
        messageFormat: "Do not specify an ordinal; use new(\"{0}\") not new(\"{0}\", id). Ordinals are assigned automatically by the generator.",
        category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _duplicateValueFromIncludedDescriptor = new(id: "SEV007", title: "Duplicate enum value from included type",
        messageFormat: "Duplicate enum value '{0}' in {1} from {2}", category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _nameCollisionWithIncludedDescriptor = new(id: "SEV008", title: "Member name conflicts with included type",
        messageFormat: "Member name '{0}' in {1} conflicts with included member from {2}", category: "EnumValueGenerator",
        defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _includeEnumValuesTypeInvalidDescriptor = new(id: "SEV009",
        title: "IncludeEnumValues source type is not valid",
        messageFormat: "[IncludeEnumValues] source type '{0}' must be an [EnumValue] or [EnumValue<T>] type with the same value type as '{1}'",
        category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _invalidConstructorDescriptor = new(id: "SEV010", title: "Invalid constructor on enum value type",
        messageFormat:
        "Type '{0}' declares constructor '{1}' which would make generated enum values open. Remove custom constructors and let the generator emit the private (value, id) constructor.",
        category: "EnumValueGenerator", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var attributePresence = context.CompilationProvider.Select(static (compilation, _) => (
            HasTypeInCurrentAssembly(compilation, "Soenneker.Gen.EnumValues.EnumValueAttribute"),
            HasTypeInCurrentAssembly(compilation, "Soenneker.Gen.EnumValues.EnumValueAttribute`1"),
            HasTypeInCurrentAssembly(compilation, "Soenneker.Gen.EnumValues.IncludeEnumValuesAttribute")));
        context.RegisterSourceOutput(attributePresence, static (spc, presence) =>
        {
            if (!presence.Item1 || !presence.Item2 || !presence.Item3)
                spc.AddSource("EnumValueAttributes.g.cs", SourceText.From(BuildAttributeSource(presence.Item1, presence.Item2, presence.Item3), Encoding.UTF8));
        });

        IncrementalValuesProvider<EnumTypeCandidate?> typeCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax typeDeclaration && HasEnumValueAttributeSyntax(typeDeclaration),
                static (syntaxContext, _) => TryGetCandidate(syntaxContext))
            .Where(static candidate => candidate is not null);

        IncrementalValueProvider<string?> sizeDependentMethodImplOption = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            GetSizeDependentMethodImplOption(provider.GlobalOptions.TryGetValue(_inliningPropertyName, out string? value) ? value : null));

        IncrementalValueProvider<((Compilation compilation, ImmutableArray<EnumTypeCandidate?> candidates) source, string? sizeDependentMethodImplOption)>
            combined = context.CompilationProvider.Combine(typeCandidates.Collect())
                              .Combine(sizeDependentMethodImplOption);

        var results = combined.SelectMany(static (tuple, cancellationToken) =>
        {
            var results = ImmutableArray.CreateBuilder<EnumGenerationResult>();
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (EnumTypeCandidate? candidate in tuple.source.candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate is null || !seen.Add(candidate.EnumType))
                    continue;
                var result = new EnumGenerationResult();
                ProcessCandidate(result, tuple.source.compilation, candidate.EnumType, candidate.ValueType, tuple.sizeDependentMethodImplOption);
                results.Add(result);
            }
            return results.ToImmutable();
        }).WithTrackingName("EnumModels");
        // Equality of the emission model stops unrelated edits from rebuilding
        // every generated source. Semantic discovery still observes changes to included
        // types, constants, references, and analyzer configuration.
        context.RegisterSourceOutput(results, static (spc, result) =>
        {
            if (result.BuildContext is { } buildContext)
                spc.AddSource(result.HintName!, SourceText.From(BuildSource(buildContext), Encoding.UTF8));
            if (result.Diagnostics is not null)
                foreach (Diagnostic diagnostic in result.Diagnostics)
                    spc.ReportDiagnostic(diagnostic);
        });
    }

    private static string? GetSizeDependentMethodImplOption(string? value)
    {
        if (string.Equals(value, "AggressiveInlining", StringComparison.OrdinalIgnoreCase))
            return "AggressiveInlining";

        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.Equals(value, "NoInlining", StringComparison.OrdinalIgnoreCase))
            return "NoInlining";

        return "Auto";
    }

    private static bool HasEnumValueAttributeSyntax(TypeDeclarationSyntax typeDeclaration)
    {
        foreach (AttributeListSyntax attributeList in typeDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (TryGetEnumValueAttributeTypeSyntax(attribute, out _))
                    return true;
            }
        }

        return false;
    }

    private static EnumTypeCandidate? TryGetCandidate(GeneratorSyntaxContext syntaxContext)
    {
        if (syntaxContext.Node is not TypeDeclarationSyntax typeDeclaration)
            return null;

        if (syntaxContext.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
            return null;

        SyntaxList<AttributeListSyntax> attributeLists = typeDeclaration.AttributeLists;

        for (var i = 0; i < attributeLists.Count; i++)
        {
            SeparatedSyntaxList<AttributeSyntax> attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                AttributeSyntax attribute = attributes[j];

                if (!TryGetEnumValueAttributeTypeSyntax(attribute, out TypeSyntax? genericArgTypeSyntax))
                    continue;

                if (genericArgTypeSyntax is null)
                {
                    INamedTypeSymbol? intType = syntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("System.Int32");
                    if (intType is null)
                        return null;

                    return new EnumTypeCandidate(typeSymbol, intType);
                }

                ITypeSymbol? resolvedType = syntaxContext.SemanticModel.GetTypeInfo(genericArgTypeSyntax)
                                                         .Type;
                if (resolvedType is INamedTypeSymbol genericValueType)
                    return new EnumTypeCandidate(typeSymbol, genericValueType);
            }
        }

        return null;
    }

    private static bool TryGetEnumValueAttributeTypeSyntax(AttributeSyntax attribute, out TypeSyntax? genericArgTypeSyntax)
    {
        genericArgTypeSyntax = null;

        SimpleNameSyntax? simpleName = attribute.Name switch
        {
            SimpleNameSyntax simple => simple,
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name,
            _ => null
        };

        if (simpleName is null)
            return false;

        string identifier = simpleName.Identifier.ValueText;

        if (identifier.EndsWith("Attribute", StringComparison.Ordinal))
            identifier = identifier.Substring(0, identifier.Length - "Attribute".Length);

        if (!string.Equals(identifier, "EnumValue", StringComparison.Ordinal))
            return false;

        if (simpleName is GenericNameSyntax genericName && genericName.TypeArgumentList.Arguments.Count == 1)
            genericArgTypeSyntax = genericName.TypeArgumentList.Arguments[0];

        return true;
    }

    private static bool IsIncludeEnumValuesAttributeSyntax(AttributeSyntax attribute)
    {
        SimpleNameSyntax? simpleName = attribute.Name switch
        {
            SimpleNameSyntax simple => simple,
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name,
            _ => null
        };

        if (simpleName is null)
            return false;

        string identifier = simpleName.Identifier.ValueText;
        if (identifier.EndsWith("Attribute", StringComparison.Ordinal))
            identifier = identifier.Substring(0, identifier.Length - "Attribute".Length);

        return string.Equals(identifier, "IncludeEnumValues", StringComparison.Ordinal);
    }

    private static bool HasTypeInCurrentAssembly(Compilation compilation, string metadataName)
    {
        return compilation.Assembly.GetTypeByMetadataName(metadataName) is not null;
    }

    private static string BuildAttributeSource(bool hasEnumValue, bool hasGenericEnumValue, bool hasIncludeEnumValues)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Soenneker.Gen.EnumValues;");
        source.AppendLine();

        if (!hasEnumValue)
        {
            source.AppendLine("/// <summary>");
            source.AppendLine("/// Marks a class or struct for source generation of enum value helpers (names, values, try-from methods).");
            source.AppendLine("/// </summary>");
            source.AppendLine(
                "[global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]");
            source.AppendLine("internal sealed class EnumValueAttribute : global::System.Attribute");
            source.AppendLine("{");
            source.AppendLine("}");
            source.AppendLine();
        }

        if (!hasGenericEnumValue)
        {
            source.AppendLine("/// <summary>");
            source.AppendLine(
                "/// Marks a class or struct for source generation of enum value helpers with a specific value type <typeparamref name=\"TValue\"/>.");
            source.AppendLine("/// </summary>");
            source.AppendLine("/// <typeparam name=\"TValue\">The type of the enum's underlying value (e.g. int, long, string).</typeparam>");
            source.AppendLine(
                "[global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]");
            source.AppendLine("internal sealed class EnumValueAttribute<TValue> : global::System.Attribute");
            source.AppendLine("{");
            source.AppendLine("}");
            source.AppendLine();
        }

        if (!hasIncludeEnumValues)
        {
            source.AppendLine("/// <summary>");
            source.AppendLine("/// Includes enum members from another type in the generated values for the attributed type.");
            source.AppendLine("/// </summary>");
            source.AppendLine(
                "[global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]");
            source.AppendLine("internal sealed class IncludeEnumValuesAttribute : global::System.Attribute");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>");
            source.AppendLine("    /// The type whose enum values are included (e.g. another enum or enum-value type).");
            source.AppendLine("    /// </summary>");
            source.AppendLine("    public global::System.Type SourceType { get; }");
            source.AppendLine();
            source.AppendLine("    /// <summary>");
            source.AppendLine("    /// Includes enum values from the specified type.");
            source.AppendLine("    /// </summary>");
            source.AppendLine("    /// <param name=\"sourceType\">The type to include values from.</param>");
            source.AppendLine("    public IncludeEnumValuesAttribute(global::System.Type sourceType) => SourceType = sourceType;");
            source.AppendLine("}");
        }

        return source.ToString();
    }

    private static ImmutableArray<INamedTypeSymbol> GetIncludeEnumValuesSourceTypes(INamedTypeSymbol enumType, Compilation compilation)
    {
        var list = new List<INamedTypeSymbol>();
        foreach (SyntaxReference syntaxReference in enumType.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            SyntaxList<AttributeListSyntax> attributeLists = typeDeclaration.AttributeLists;

            for (var i = 0; i < attributeLists.Count; i++)
            {
                SeparatedSyntaxList<AttributeSyntax> attributes = attributeLists[i].Attributes;
                for (var j = 0; j < attributes.Count; j++)
                {
                    AttributeSyntax attribute = attributes[j];

                    if (!IsIncludeEnumValuesAttributeSyntax(attribute))
                        continue;

                    if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
                        continue;

                    ExpressionSyntax expression = attribute.ArgumentList.Arguments[0].Expression;
                    if (expression is not TypeOfExpressionSyntax typeOfExpression)
                        continue;

                    ITypeSymbol? resolvedType = semanticModel.GetTypeInfo(typeOfExpression.Type)
                                                             .Type;
                    if (resolvedType is INamedTypeSymbol sourceType)
                        list.Add(sourceType);
                }
            }
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

        foreach (SyntaxReference syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            SyntaxList<AttributeListSyntax> attributeLists = typeDeclaration.AttributeLists;
            for (var i = 0; i < attributeLists.Count; i++)
            {
                SeparatedSyntaxList<AttributeSyntax> attributes = attributeLists[i].Attributes;
                for (var j = 0; j < attributes.Count; j++)
                {
                    AttributeSyntax attribute = attributes[j];
                    if (!TryGetEnumValueAttributeTypeSyntax(attribute, out TypeSyntax? genericArgTypeSyntax))
                        continue;

                    if (genericArgTypeSyntax is null)
                    {
                        valueType = compilation.GetTypeByMetadataName("System.Int32") as INamedTypeSymbol;
                        return valueType is not null;
                    }

                    ITypeSymbol? resolvedType = semanticModel.GetTypeInfo(genericArgTypeSyntax)
                                                             .Type;
                    if (resolvedType is INamedTypeSymbol genericValueType)
                    {
                        valueType = genericValueType;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void ProcessCandidate(EnumGenerationResult context, Compilation compilation, INamedTypeSymbol enumType, INamedTypeSymbol valueType,
        string? sizeDependentMethodImplOption)
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
            context.ReportDiagnostic(Diagnostic.Create(_invalidConstructorDescriptor, invalidCtor.Locations.FirstOrDefault(), enumType.Name,
                invalidCtor.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        List<EnumInstance> ownInstances = GatherInstances(context, compilation, enumType, valueType);

        var instances = new List<EnumInstance>(ownInstances);
        ImmutableArray<INamedTypeSymbol> includeTypes = GetIncludeEnumValuesSourceTypes(enumType, compilation);
        foreach (INamedTypeSymbol sourceType in includeTypes)
        {
            if (!TryGetEnumValueValueType(sourceType, compilation, out INamedTypeSymbol? sourceValueType) ||
                !SymbolEqualityComparer.Default.Equals(sourceValueType, valueType))
            {
                context.ReportDiagnostic(Diagnostic.Create(_includeEnumValuesTypeInvalidDescriptor, enumType.Locations.FirstOrDefault(), sourceType.Name,
                    enumType.Name));
                return;
            }

            List<EnumInstance> included = GatherInstancesFromType(context, compilation, sourceType, valueType, sourceType.Name);
            foreach (EnumInstance inst in included)
                instances.Add(new EnumInstance(inst.Name, inst.ValueLiteral, inst.StringValue, inst.Location, id: null, inst.SourceTypeName,
                    inst.ValueJsonString));
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
                    context.ReportDiagnostic(Diagnostic.Create(_duplicateValueFromIncludedDescriptor, instance.Location, instance.ValueLiteral, enumType.Name,
                        fromType));
                else
                    context.ReportDiagnostic(Diagnostic.Create(_duplicateValueDescriptor, instance.Location, enumType.Name, instance.ValueLiteral));
                return;
            }

            if (!seenNames.Add(instance.Name))
            {
                if (instance.SourceTypeName is { } fromType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(_nameCollisionWithIncludedDescriptor, instance.Location, instance.Name, enumType.Name,
                        fromType));
                    return;
                }
            }
        }

        // Valid emission models must not retain syntax trees through locations.
        for (var i = 0; i < instances.Count; i++)
        {
            EnumInstance instance = instances[i];
            instances[i] = new EnumInstance(instance.Name, instance.ValueLiteral, instance.StringValue, Location.None,
                instance.Id, instance.SourceTypeName, instance.ValueJsonString);
        }

        bool hasValueProperty = HasValueProperty(enumType, valueType);
        bool hasValueIdConstructor = HasValueIdConstructor(enumType, valueType);
        bool hasNameProperty = HasNameProperty(enumType);

        bool supportsNewtonsoft = SupportsNewtonsoft(compilation);
        context.BuildContext = BuildContext(enumType, valueType, instances, hasValueProperty, hasValueIdConstructor, hasNameProperty, supportsNewtonsoft,
            sizeDependentMethodImplOption);
        context.HintName = $"{enumType.Name}.EnumValues.g.cs";
    }

    private static void AppendXmlSummary(StringBuilder source, string indent, string text)
    {
        source.Append(indent)
              .AppendLine("/// <summary>");
        source.Append(indent)
              .Append("/// ")
              .AppendLine(text);
        source.Append(indent)
              .AppendLine("/// </summary>");
    }

    private static EnumSourceBuildContext BuildContext(INamedTypeSymbol enumType, INamedTypeSymbol valueType, List<EnumInstance> instances, bool hasValueProperty,
        bool hasValueIdConstructor, bool hasNameProperty, bool supportsNewtonsoft, string? sizeDependentMethodImplOption)
    {
        string enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string valueTypeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string ns = enumType.ContainingNamespace.IsGlobalNamespace ? string.Empty : enumType.ContainingNamespace.ToDisplayString();
        bool isStringValue = valueType.SpecialType == SpecialType.System_String;
        bool useIdBacking = isStringValue;

        var ctx = new EnumSourceBuildContext(enumType, valueType, instances, hasValueProperty, hasValueIdConstructor, hasNameProperty, supportsNewtonsoft,
            enumTypeName, valueTypeName, ns, enumType.TypeKind == TypeKind.Struct ? "struct" : "class", enumType.Name + "JsonConverter",
            enumType.Name + "NewtonsoftJsonConverter", enumType.Name + "TypeConverter", isStringValue, useIdBacking,
            isStringValue ? "string? value" : valueTypeName + " value", isStringValue
                ? instances.Select(static instance => (instance.Name + "Value", instance.Name))
                           .ToList()
                : instances.Select(static instance => (instance.StringValue ?? string.Empty, instance.Name))
                           .ToList(), instances.Select(static instance => (instance.Name + "Name", instance.Name))
                                               .ToList(), instances.Select(static instance => (instance.Name, instance.Name))
                                                                   .ToList(), isStringValue
                ? instances.Select(static instance => (instance.StringValue ?? string.Empty, instance.Name))
                           .ToList()
                : new List<(string Text, string TargetName)>(), BuildReadRawValueCode(valueType), BuildWriteValueCode(valueType), sizeDependentMethodImplOption);

        return ctx;
    }

    private static string BuildSource(in EnumSourceBuildContext ctx)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        if (!string.IsNullOrEmpty(ctx.Ns))
        {
            source.Append("namespace ")
                  .Append(ctx.Ns)
                  .AppendLine(";");
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

    private static void AppendIsDefinedIsNameDefined(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        if (ctx.IsStringValue)
        {
            AppendXmlSummary(source, "    ", "Returns whether a value is defined for the given string.");
            AppendSizeDependentMethodImplAttribute(source, ctx);
            source.AppendLine("    public static bool IsDefined(string? value) => value switch");
            source.AppendLine("    {");
            for (var i = 0; i < ctx.Instances.Count; i++)
                source.Append("        ")
                      .Append(ctx.Instances[i].Name)
                      .AppendLine("Value => true,");
            source.AppendLine("        _ => false");
            source.AppendLine("    };");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns whether a value is defined for the given span.");
            AppendSizeDependentMethodImplAttribute(source, ctx);
            source.Append("    public static bool IsDefined(global::System.ReadOnlySpan<char> value) => ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(".TryFromValue(value, out _);");
            source.AppendLine();
        }
        else
        {
            AppendXmlSummary(source, "    ", "Returns whether the given value is defined.");
            AppendSizeDependentMethodImplAttribute(source, ctx);
            source.Append("    public static bool IsDefined(")
                  .Append(ctx.ValueTypeName)
                  .AppendLine(" value) => value switch");
            source.AppendLine("    {");
            for (var i = 0; i < ctx.Instances.Count; i++)
                source.Append("        ")
                      .Append(ctx.Instances[i].ValueLiteral)
                      .AppendLine(" => true,");
            source.AppendLine("        _ => false");
            source.AppendLine("    };");
            source.AppendLine();
        }

        AppendXmlSummary(source, "    ", "Returns whether a name is defined.");
        AppendSizeDependentMethodImplAttribute(source, ctx);
        source.AppendLine("    public static bool IsNameDefined(string? name) => name switch");
        source.AppendLine("    {");
        for (var i = 0; i < ctx.Instances.Count; i++)
            source.Append("        ")
                  .Append(ctx.Instances[i].Name)
                  .AppendLine("Name => true,");
        source.AppendLine("        _ => false");
        source.AppendLine("    };");
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Returns whether a name is defined for the given span.");
        AppendSizeDependentMethodImplAttribute(source, ctx);
        source.Append("    public static bool IsNameDefined(global::System.ReadOnlySpan<char> name) => ")
              .Append(ctx.EnumTypeName)
              .AppendLine(".TryFromName(name, out _);");
    }

    private static void AppendSpanFirstCharSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier,
        int indentLevel)
    {
        // Let Roslyn choose the length/character decision tree for constant spans.
        // A first-character chain degenerates when values share a prefix.
        string indent = new(' ', indentLevel * 4);
        source.Append(indent).Append("switch (").Append(inputIdentifier).AppendLine(")");
        source.Append(indent).AppendLine("{");
        foreach ((string text, string targetName) in items)
        {
            source.Append(indent).Append("    case \"").Append(EscapeString(text)).Append("\": result = ")
                  .Append(targetName).AppendLine("; return true;");
        }
        source.Append(indent).AppendLine("    default: result = default!; return false;");
        source.Append(indent).AppendLine("}");
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

    internal static string BuildNewtonsoftReadRawValueCode(ITypeSymbol valueType)
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
                return
                    "        if (reader.TokenType != global::Newtonsoft.Json.JsonToken.String) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected string value. Token type: \" + reader.TokenType + \".\"); string rawValue = (string?)reader.Value ?? throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected non-null string value. Token type: \" + reader.TokenType + \".\");";
            case SpecialType.System_Char:
                return
                    "        if (reader.TokenType != global::Newtonsoft.Json.JsonToken.String) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected char value. Token type: \" + reader.TokenType + \".\"); string charText = (string?)reader.Value ?? throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected char value.\"); if (charText == null || charText.Length != 1) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected single-character value. Got: \" + (charText ?? \"(null)\") + \".\"); char rawValue = charText[0];";
            case SpecialType.System_Boolean:
                return "        bool rawValue = global::System.Convert.ToBoolean(reader.Value, global::System.Globalization.CultureInfo.InvariantCulture);";
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                    return
                        "        if (reader.TokenType != global::Newtonsoft.Json.JsonToken.String) throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected Guid string value. Token type: \" + reader.TokenType + \".\"); string guidText = (string?)reader.Value ?? throw new global::Newtonsoft.Json.JsonSerializationException(\"Expected Guid string value.\"); global::System.Guid rawValue = global::System.Guid.Parse(guidText);";

                return "        " + typeName + " rawValue = serializer.Deserialize<" + typeName + ">(reader)!;";
            }
        }
    }

    internal static string BuildNewtonsoftWriteValueCode(ITypeSymbol valueType)
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

    internal static string BuildToStringExpression(ITypeSymbol valueType)
    {
        // Guid and other non-primitives typically don't have ToString(IFormatProvider)
        if (valueType.ToDisplayString() == "System.Guid")
            return "Value.ToString()";
        return "Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)!";
    }

    /// <summary>Returns the default-case body for WriteAsPropertyName (unknown value): Utf8Formatter or ToString().</summary>
    internal static string BuildStjWritePropertyNameFallback(ITypeSymbol valueType)
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
            return "            default:\n" + "                global::System.Span<byte> buf = stackalloc byte[" + bufferSize + "];\n" +
                   "                if (!global::System.Buffers.Text.Utf8Formatter.TryFormat(value.Value, buf, out int written))\n" +
                   "                    throw new global::System.Text.Json.JsonException($\"Unknown enum value for property name: '\" + value.Value + \"'.\");\n" +
                   "                writer.WritePropertyName(buf[..written]);\n" + "                return;";
        }

        return "            default:\n" +
               "                throw new global::System.Text.Json.JsonException($\"Unknown enum value for property name: '\" + value.Value + \"'.\");";
    }

    internal static bool CanEmitConstant(ITypeSymbol valueType)
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
}
