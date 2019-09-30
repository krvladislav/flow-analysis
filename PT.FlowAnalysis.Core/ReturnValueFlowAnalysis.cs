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

            if (HasLiteralStateLoss(valueContentAnalysis))
            {
                return null;
            }

            var returnValues = valueContentAnalysis.ReturnValueAndPredicateKindOpt?.Value.LiteralValues;

            return returnValues;
        }

        private static bool HasLiteralStateLoss(DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue> valueContentAnalysis)
        {
            /*
             Проверяем, что в ходе анализа не было потери списка возможных литералов.
             Например, проявляется при достижении ограничения на количество возможных литералов
             https://github.com/dotnet/roslyn-analyzers/blob/2a72252ddcd7c3ebbbb842239e67116a3678f610/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAbstractValue.cs#L171
             */
            foreach (var block in valueContentAnalysis.ControlFlowGraph.Blocks)
            {
                foreach (var keyValue in valueContentAnalysis[block].Data)
                {
                    var key = keyValue.Key;
                    var value = keyValue.Value;
                    if (value.NonLiteralState == ValueContainsNonLiteralState.Maybe)
                    {
                        if (block.Predecessors.All(pred =>
                            valueContentAnalysis[pred.Source].Data.TryGetValue(key, out var predValue)
                            && predValue.NonLiteralState == ValueContainsNonLiteralState.No))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}