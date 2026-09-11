using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Soenneker.Gen.EnumValues.Generator.Tests;

public sealed class IncrementalTests
{
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(p => MetadataReference.CreateFromFile(p)).ToArray();
    private const string Source = """
        namespace Probe;
        [Soenneker.Gen.EnumValues.EnumValue<string>]
        [Soenneker.Gen.EnumValues.IncludeEnumValues(typeof(Included))]
        public sealed partial class Codes
        {
            public static readonly Codes A = new(Constants.Current);
        }
        [Soenneker.Gen.EnumValues.EnumValue<string>]
        public sealed partial class Included
        {
            public static readonly Included Other = new("included");
        }
        """;
    private static CSharpCompilation Compilation() => CSharpCompilation.Create("Probe",
        [CSharpSyntaxTree.ParseText(Source), CSharpSyntaxTree.ParseText("namespace Probe; public static class Constants { public const string Current = \"a\"; }")],
        References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    private static GeneratorDriver Driver() => CSharpGeneratorDriver.Create([new EnumValueSourceGenerator().AsSourceGenerator()],
        driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    private static GeneratorDriver Run(GeneratorDriver driver, CSharpCompilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Check(errors.Length == 0, string.Join("\n", errors.Select(d => d.ToString())));
        return driver;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static GeneratedSourceResult Code(GeneratorDriver driver) => driver.GetRunResult().Results.Single().GeneratedSources.Single(s => s.HintName == "Codes.EnumValues.g.cs");

    [Test]
    public void Unrelated_edit_reuses_source_output()
    {
        var compilation = Compilation();
        var before = Run(Driver(), compilation);
        var after = Run(before, compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated {}")));
        var result = after.GetRunResult().Results.Single();
        Check(result.TrackedSteps["EnumModels"].SelectMany(s => s.Outputs).All(o => o.Reason == IncrementalStepRunReason.Unchanged), "Enum models changed after unrelated edit.");
        Check(result.TrackedOutputSteps["SourceOutput"].SelectMany(s => s.Outputs).All(o => o.Reason == IncrementalStepRunReason.Cached), "Source emission was not cached.");
    }

    [Test]
    public void External_constant_change_invalidates_source()
    {
        var compilation = Compilation();
        var before = Run(Driver(), compilation);
        var constants = compilation.SyntaxTrees.Last();
        var edited = compilation.ReplaceSyntaxTree(constants, CSharpSyntaxTree.ParseText(constants.ToString().Replace("\"a\"", "\"b\"")));
        var after = Run(before, edited);
        Check(Code(after).SourceText.ToString().Contains("AValue = \"b\""), "External constants must invalidate the model.");
    }

    [Test]
    public void Included_value_change_invalidates_source()
    {
        var compilation = Compilation();
        var before = Run(Driver(), compilation);
        var source = compilation.SyntaxTrees.First();
        var after = Run(before, compilation.ReplaceSyntaxTree(source, CSharpSyntaxTree.ParseText(Source.Replace("\"included\"", "\"changed\""))));
        Check(Code(after).SourceText.ToString().Contains("OtherValue = \"changed\""), "Included values must invalidate the model.");
    }

    [Test]
    public void Existing_constant_and_inlining_changes_invalidate_source()
    {
        var compilation = Compilation();
        var before = Run(Driver(), compilation);
        var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), CSharpSyntaxTree.ParseText(Source.Replace(
            "public static readonly Codes A", "public const string AName = \"custom\"; public static readonly Codes A")));
        var after = Run(before, edited);
        Check(!Code(after).SourceText.ToString().Contains("public const string AName"), "User constants must not be emitted twice.");
        after = Run(after.WithUpdatedAnalyzerConfigOptions(new OptionsProvider()), edited);
        Check(Code(after).SourceText.ToString().Contains("MethodImplOptions.NoInlining"), "Inlining configuration must invalidate the model.");
    }

    [Test]
    public void Duplicate_value_diagnostic_appears_and_disappears_after_edits()
    {
        var compilation = Compilation();
        var before = Run(Driver(), compilation);
        var source = compilation.SyntaxTrees.First();
        var invalid = compilation.ReplaceSyntaxTree(source, CSharpSyntaxTree.ParseText(Source.Replace(
            "public static readonly Codes A", "public static readonly Codes Duplicate = new(Constants.Current); public static readonly Codes A")));
        var failed = before.RunGenerators(invalid);
        Check(failed.GetRunResult().Diagnostics.Any(d => d.Id == "SEV005"), "Expected duplicate-value diagnostic.");
        Check(!failed.GetRunResult().Results.Single().GeneratedSources.Any(s => s.HintName == "Codes.EnumValues.g.cs"), "Stale source must be removed.");
        var fixedDriver = Run(failed, compilation);
        Check(!fixedDriver.GetRunResult().Diagnostics.Any(d => d.Id == "SEV005"), "Diagnostic must disappear after fixing the source.");
    }

    private sealed class OptionsProvider : AnalyzerConfigOptionsProvider
    {
        private sealed class Options : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value) { value = "NoInlining"; return key == "build_property.EnumValuesInlining"; }
        }
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options();
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
