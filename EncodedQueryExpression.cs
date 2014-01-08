using System;
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
    public class EncodedQueryExpression
    {
        private string _query = string.Empty;

        public Utilities.ContinuationOperator ContinuationOperator { get; set; }
        public string FieldName {get; set;}
        public Utilities.RepoExpressionType @Operator {get; set;} 
        public string FieldValue {get; set;}
        public bool IsNegated {get; set;}
        public string EncodedQuery {get; set;}
        public Expression @Expression {get; set;}

        public string @Value
        {
            get
            {
                if (String.IsNullOrEmpty(_query))
                    return EncodedQuery;
                else
                    return _query;
            }
        }

        public bool IsNameOrValueMissing 
        {
            get
            {
                return String.IsNullOrEmpty(FieldName) | String.IsNullOrEmpty(FieldValue);
            }
        }

       public bool HasValue
       {
            get
            {
                return !String.IsNullOrEmpty(EncodeValue());
            }
       }

        public override string ToString()
        {
            return EncodeValue();
        }

        private void GetTerms()
        {
            if (Expression.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCall = Expression as MethodCallExpression;
                if (methodCall.Object != null)
                {
                    FieldName = GetFieldName(methodCall.Object);
                    FieldValue = GetFieldValue(methodCall.Arguments[0]);
                }
                else
                    SetMethodValues(methodCall);
            }
            else
                SetValues(Expression);
        }

        private string BuildExpression()
        {
            GetTerms();

            if (String.IsNullOrEmpty(FieldName) | String.IsNullOrEmpty(FieldValue))
            {
                if (String.IsNullOrEmpty(EncodedQuery))
                    return String.Empty;
                else
                    return EncodedQuery;
            }

            string oper = Utilities.GetRepoExpressionType(Operator);
            if (IsNegated)
                oper = Utilities.NegateRepoExpressionType(Operator);

            return String.Format("{0}{1}{2}", FieldName, oper, FieldValue);
        }

        private string EncodeValue()
        {
            _query = BuildExpression();

            if(!String.IsNullOrEmpty(EncodedQuery) && !EncodedQuery.EndsWith("NQ"))
                _query = Utilities.GetContinuationOperator(ContinuationOperator) + _query;

            if(!String.IsNullOrEmpty(_query))
            {
                _query = EncodedQuery + _query;
            }

            return _query;
        }

        private string GetFieldName(Expression expr)
        {
            string fieldname = "";

            if (expr.NodeType == ExpressionType.MemberAccess)
                fieldname = Utilities.GetPropertyName(expr);

            if (expr.NodeType == ExpressionType.Call && (expr as MethodCallExpression).Method.Name == "Parse")
                fieldname = Utilities.GetPropertyName((expr as MethodCallExpression).Arguments[0]);

            return fieldname;
        }

        private string GetFieldValue(Expression expr)
        {
            string fieldvalue = "";

            if (expr.NodeType == ExpressionType.Constant)
                fieldvalue = expr.ToString().Replace("\"", "");

            if (expr.NodeType == ExpressionType.MemberAccess)
                fieldvalue = Utilities.GetPropertyName(expr);

            if (expr.NodeType == ExpressionType.Call && (expr as MethodCallExpression).Method.Name == "Parse")
                fieldvalue = (expr as MethodCallExpression).Arguments[0].ToString().Replace("\"", "");

            if (expr.NodeType == ExpressionType.Convert)
                fieldvalue = GetFieldValue((expr as UnaryExpression).Operand);

            if (expr.NodeType == ExpressionType.NewArrayInit)
                fieldvalue = string.Join(",", (expr as NewArrayExpression).Expressions.Select(o => o.ToString().Replace("\"", "")).ToArray());

            return fieldvalue;
        }

        private void SetMethodValues(MethodCallExpression methodCall)
        {
            if (methodCall.Arguments.Count > 1)
            {
                if (methodCall.Arguments[1].NodeType == ExpressionType.MemberAccess)
                {
                    FieldName = GetFieldName(methodCall.Arguments[1]);
                    FieldValue = GetFieldValue(methodCall.Arguments[0]);
                }
                else
                {
                    FieldName = GetFieldName(methodCall.Arguments[0]);
                    FieldValue = GetFieldValue(methodCall.Arguments[1]);
                }
            }
            else
            {
                if (methodCall.Arguments[0].NodeType == ExpressionType.Constant)
                    FieldValue = GetFieldValue(methodCall);
                if (methodCall.Arguments[0].NodeType == ExpressionType.MemberAccess)
                    FieldName = GetFieldName(methodCall);
            }
        }

        private void SetBinaryValues(BinaryExpression binExpr)
        {            
            FlipOperator(binExpr.Left);
            SetValues(binExpr.Left);
            if (IsNameOrValueMissing)
                SetValues(binExpr.Right);
        }

        private void SetValues(Expression expr)
        {
            if (expr.NodeType == ExpressionType.Constant)
                FieldValue = GetFieldValue(expr);
            else if (expr.NodeType == ExpressionType.MemberAccess)
                FieldName = GetFieldName(expr);
            else if (expr.NodeType == ExpressionType.Call)
            {
                SetMethodValues((MethodCallExpression)expr);
            }
            else
            {
                SetBinaryValues(expr as BinaryExpression);
            }
        }

        private void FlipOperator(Expression expr)
        {
            if (expr.NodeType == ExpressionType.Constant)
                Operator = Utilities.FlipRepoExpressionType(Operator);
            else if (expr.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCall = (MethodCallExpression)expr;
                if (methodCall.Arguments[0].NodeType == ExpressionType.Constant)
                    Operator = Utilities.FlipRepoExpressionType(Operator);
            }
        }
    }
}
