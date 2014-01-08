using System.Linq;
using System.Linq.Expressions;

/**
 * LinqToServiceNow
 *
 * @package    LinqToServiceNow
 * @author     Raymond Burkitt
 * @copyright  (c) 2013 Technao
 * @license    http://www.gnu.org/licenses/old-licenses/lgpl-2.1.txt
 */

namespace LinqToServiceNow
{
    public class ExpressionVisitor
    {
        private string _encodedQuery = string.Empty;

        public string VisitExpression(Utilities.ContinuationOperator continuation, Expression expr)
        {
            return VisitExpression(continuation, expr, false);
        }

        public string VisitExpression(Utilities.ContinuationOperator continuation, Expression expr, bool neg)
        {
            if (expr.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCall = (expr as MethodCallExpression);

                if (methodCall.Method.Name == "Contains" & methodCall.Arguments.Count > 1)
                    VisitContainsExpression(continuation, expr as MethodCallExpression, neg);
                else
                    VisitSimpleMethodCall(continuation, methodCall, neg);
            }
            else if (expr.NodeType == ExpressionType.Not)
            {
                UnaryExpression unaryExpr = expr as UnaryExpression;
                VisitExpression(continuation, unaryExpr.Operand, true);
            }
            else
                VisitBinaryExpression(continuation, expr as BinaryExpression, neg);

            return _encodedQuery;
        }

        private void VisitBinaryExpression(Utilities.ContinuationOperator continuation, BinaryExpression binExpr, bool neg)
        {
            ExpressionType oper = binExpr.NodeType;

            ExpressionType[] AndOperators = { ExpressionType.And, ExpressionType.AndAlso };

            ExpressionType[] OrOperators = { ExpressionType.Or, ExpressionType.OrElse };

            ExpressionType[] binOperators = AndOperators.Concat(OrOperators).ToArray();

            if (binOperators.Contains(binExpr.NodeType))
            {
                VisitExpression(continuation, binExpr.Left, neg);

                if (binOperators.Contains(binExpr.Left.NodeType) & binOperators.Contains(binExpr.Right.NodeType))
                {
                    if (AndOperators.Contains(binExpr.NodeType))
                        _encodedQuery += "NQ";

                    if (OrOperators.Contains(binExpr.NodeType))
                        _encodedQuery += "^NQ";
                }

                VisitExpression((Utilities.ContinuationOperator)binExpr.NodeType, binExpr.Right, neg);
            }
            else
                VisitSimpleExpression(continuation, binExpr, neg);
        }

        private void VisitSimpleExpression(Utilities.ContinuationOperator continuation, BinaryExpression binExpr, bool neg)
        {
            EncodedQueryExpression encodedQuery = new EncodedQueryExpression {
                ContinuationOperator = continuation,
                IsNegated = neg,
                Operator = (Utilities.RepoExpressionType)binExpr.NodeType,
                EncodedQuery = _encodedQuery,
                Expression = binExpr
            };

            if(encodedQuery.HasValue)
                _encodedQuery = encodedQuery.Value;
        }

        private void VisitContainsExpression(Utilities.ContinuationOperator continuation, MethodCallExpression methodCall, bool neg)
        {
            EncodedQueryExpression encodedQuery = new EncodedQueryExpression {
                ContinuationOperator = continuation,
                IsNegated = neg,
                Operator = Utilities.RepoExpressionType.IN,
                EncodedQuery = _encodedQuery,
                Expression = methodCall
            };

            if (encodedQuery.HasValue)
                _encodedQuery = encodedQuery.Value;
            else
                VisitExpression(continuation, methodCall, neg);
        }

        private void VisitSimpleMethodCall(Utilities.ContinuationOperator continuation, MethodCallExpression methodCall, bool neg)
        {
           EncodedQueryExpression encodedQuery = new EncodedQueryExpression {
                ContinuationOperator = continuation,
                IsNegated = neg,
                Operator = Utilities.GetRepoExpressionType(methodCall.Method.Name),
                EncodedQuery = _encodedQuery,
                Expression = methodCall
            };

            if(encodedQuery.HasValue)
                _encodedQuery = encodedQuery.Value;
            else
                VisitExpression(continuation, methodCall, neg);
        }
    }
}
