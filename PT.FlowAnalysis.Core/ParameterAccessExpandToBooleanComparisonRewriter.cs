using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PT.FlowAnalysis.Core
{
    public class ParameterAccessExpandToBooleanComparisonRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
        {
            return node.ReplaceNodes(node.DescendantNodes().OfType<ElementAccessExpressionSyntax>(),
                (o, n) =>
                {
                    if (o.Parent.IsKind(SyntaxKind.EqualsExpression))
                    {
                        return o;
                    }
                    return SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, o,
                        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
                });
        }
    }
}