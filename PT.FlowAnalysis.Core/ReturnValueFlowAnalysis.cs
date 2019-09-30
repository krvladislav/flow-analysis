using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Linq;

namespace PT.FlowAnalysis.Core
{
    internal class ReturnValueFlowAnalysis
    {
        internal static IEnumerable<object> TryAnalyze(
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            CSharpCompilation compilation)
        {
            var cfg = ControlFlowGraph.Create(methodNode, semanticModel);
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
            var owningSymbol = semanticModel.GetDeclaredSymbol(methodNode);

            var valueContentAnalysis = ValueContentAnalysis.TryGetOrComputeResult(cfg,
                owningSymbol,
                wellKnownTypeProvider,
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                null,
                CancellationToken.None
            );

            var returnValues = valueContentAnalysis.ReturnValueAndPredicateKindOpt?.Value.LiteralValues;

            return returnValues;
        }
    }
}