using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PT.FlowAnalysis.Core
{
    public enum AnalysisType { Static, Dynamic }

    public class SolverResult
    {
        public bool Success { get; set; }
        public IEnumerable<string> CompilationFailures { get; set; }
        public IEnumerable<object> ReturnValues { get; set; }
        public AnalysisType UsedAnalysisType { get; set; }

    }


    public class ProblemSolver
    {
        private const int MaxParamCount = 50;
        private const string TargetMethodName = "Evaluate";
        private const string TargetClass = "Program";

        private readonly AnalysisType? _forceAnalysisType;
        private readonly bool _dryRun;

        public static SolverResult Run(string source, AnalysisType? forceAnalysisType = null, bool dryRun = false)
        {
            var solver = new ProblemSolver(forceAnalysisType, dryRun);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            /* В Roslyn фаза ValueContentAnalysis не выполняет предикатный анализ для выражений parameters[0].
             Экспериментально выяснилось, что для выражения parameters[0] == true предикатный анализ срабатывает.

             В результате в первом случае (parameters[0] && !parameters[0]) не свертывается в false, и наоборот,
             для (parameters[0] == true && !parameters[0] == true) произойдет свертка в значение false.
             Здесь мы модифицируем исходное синтаксическое дерево, добавляя сравнение == true.

             При динамическом выполнtении кода избыточное сравнение будет удалено оптимизациями кодегенератора.
             */
            var rewriter = new ParameterAccessExpandToBooleanComparisonRewriter();
            syntaxTree = rewriter.Visit(syntaxTree.GetRoot()).SyntaxTree;

            var compilation = Compiler.Compile(syntaxTree);
            if (!compilation.Success)
            {
                return new SolverResult
                {
                    Success = false,
                    CompilationFailures = compilation.Diagnostics
                        .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")
                };
            }

            var result = solver.PerformAnalysis(syntaxTree, compilation);

            return result;
        }

        private ProblemSolver(AnalysisType? forceAnalysisType, bool dryRun)
        {
            _forceAnalysisType = forceAnalysisType;
            _dryRun = dryRun;
        }

        private SolverResult PerformAnalysis(SyntaxTree syntaxTree, CompilationResult compilation)
        {
            var paramIndexes =
                syntaxTree
                    .GetRoot().DescendantNodes()
                    .OfType<ElementAccessExpressionSyntax>()
                    .Select(paramAccessNode =>
                        compilation.SemanticModel
                            .GetConstantValue(paramAccessNode.ArgumentList.Arguments.Single().Expression))
                    .ToList();
            var isKnownIndexes = paramIndexes.All(i => i.HasValue);
            var paramCount = !paramIndexes.Any() ? 0
                            : isKnownIndexes ? paramIndexes.Max(i => (int)i.Value) + 1
                            : MaxParamCount;

            IEnumerable<object> returnValues = null;
            if (_forceAnalysisType == AnalysisType.Static ||
                (_forceAnalysisType == null && CanPerformStaticAnalysis()))
            {
                var watch = Stopwatch.StartNew();

                returnValues =
                    !_dryRun
                        ? PerformStaticAnalysis(syntaxTree, compilation.SemanticModel, compilation.Compilation)
                        : new List<object>();

                var elapsed = watch.Elapsed;
            }

            bool isDynamicAnalysis = false;
            if (_forceAnalysisType == AnalysisType.Dynamic || 
                (_forceAnalysisType == null && returnValues == null))
            {
                var watch = Stopwatch.StartNew();

                returnValues = 
                    !_dryRun
                        ? PerformDynamicAnalysis(compilation, paramCount)
                        : new List<object>();
                isDynamicAnalysis = true;

                var elapsed = watch.Elapsed;
            }

            return new SolverResult
            {
                Success = returnValues != null,
                ReturnValues = returnValues,
                UsedAnalysisType = isDynamicAnalysis ? AnalysisType.Dynamic : AnalysisType.Static
            };

            #region Local functions

            bool CanPerformStaticAnalysis()
            {
                /* эмпирически выведено, что статический анализ начинает работать быстрее динамического
                 при количестве параметров от 20. Эта эвристика посчитатна на одной машине и может быть усложнена,
                 чтобы динамически выбираться в зависимости от тестовых прогонов обоих анализов на конкретной машине
                 */
                var isParamCountThresholdHit = paramCount >= 20;

                // если в коде есть обращения за границы массива, то нужно динамически выполнить код, чтобы учитывать исключение
                var isOutOfBoundsAccess = paramIndexes.Any() && isKnownIndexes && paramIndexes.Min(i => (int)i.Value) < 0;

                /* если в коде есть арифметика с обращением к "x", то Value Content Analysis не сможет правильно посчитать предикат
                 Без арифметики анализ работает
                 */
                var hasIntArithmeticWithLocal = syntaxTree
                    .GetRoot().DescendantNodes()
                    .OfType<IfStatementSyntax>()
                    .Any(ifNode => ifNode.Condition
                                       .DescendantNodes()
                                       .OfType<IdentifierNameSyntax>()
                                       .Any(id => id.Identifier.Text == "x")
                                   && ifNode.Condition
                                       .DescendantNodes()
                                       .OfType<BinaryExpressionSyntax>().Any());

                return isKnownIndexes 
                       && !isOutOfBoundsAccess 
                       && isParamCountThresholdHit
                       && !hasIntArithmeticWithLocal; 
            }

            #endregion
        }

        private IEnumerable<object> PerformDynamicAnalysis(CompilationResult compilation, int paramCount)
        {
            var evaluateMethodInfo = EmitTargetMethod(TargetMethodName, compilation.Compilation);
            var returnValues = ReturnValueCodeExecution.ExecuteCodeForAllCases(evaluateMethodInfo, paramCount);
            return returnValues;
        }

        private static IEnumerable<object> PerformStaticAnalysis(
            SyntaxTree syntaxTree, 
            SemanticModel semanticModel, 
            CSharpCompilation compilation)
        {
            var evaluateMethodNode =
                syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Single(i => i.Identifier.Text == TargetMethodName);

            var returnValues =
                ReturnValueFlowAnalysis.TryAnalyze(evaluateMethodNode, semanticModel, compilation);
            return returnValues;
        }

        private MethodInfo EmitTargetMethod(string targetMethodName, CSharpCompilation compilation)
        {
            Assembly assembly;
            using (var peStream = new MemoryStream())
            {
                var emitResult = compilation.Emit(peStream);
                Debug.Assert(emitResult.Success);
                peStream.Seek(0, SeekOrigin.Begin);
                assembly = Assembly.Load(peStream.ToArray());
            }

            var programClassInfo = assembly.GetTypes().Single(t => t.Name.Contains(TargetClass));
            var evaluateMethodInfo = programClassInfo.GetRuntimeMethods().Single(m => m.Name.Contains(targetMethodName));

            return evaluateMethodInfo;
        }
    }

}