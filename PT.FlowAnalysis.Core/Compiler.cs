using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PT.FlowAnalysis.Core
{
    public class CompilationResult
    {
        public bool Success { get; set; }
        public IEnumerable<Diagnostic> Diagnostics { get; set; }
        public SyntaxTree SyntaxTree { get; set; }
        public CSharpCompilation Compilation { get; set; }
        public SemanticModel SemanticModel { get; set; }
    }


    public class Compiler
    {
        public static CompilationResult Compile(SyntaxTree syntaxTree)
        {
            var refPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var usingRefNames = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.Name.ToString())
                .ToList();
            var references = usingRefNames.Select(name => MetadataReference.CreateFromFile(Path.Combine(refPath, name + ".dll"))).ToList();
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            var assemblyName = Path.GetRandomFileName();

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            var model = compilation.GetSemanticModel(syntaxTree);

            var diagnostics = 
                compilation
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();
            
            if (diagnostics.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics
                };
            }

            return new CompilationResult
            {
                Success = true,
                SyntaxTree = syntaxTree,
                Compilation = compilation,
                SemanticModel = model
            };
        }
    }
}