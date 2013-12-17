using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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
    public class ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult>
        where TServiceNow_cmdb_ci_ : new()
        where TGetRecords : new()        
    {
        private TServiceNow_cmdb_ci_ proxyUser = new TServiceNow_cmdb_ci_();

        private TGetRecords _filter = new TGetRecords();

        private Func<TGetRecordsResponseGetRecordsResult, dynamic> _selectQuery;

        private string _encodedQuery;
        private string _groupbyQuery;

        private void SetFilterProperty(string prop, string val)
        {
            Type t = _filter.GetType();
            FieldInfo fInfo = t.GetField(prop);
            if (fInfo != null)
            {
                fInfo.SetValue(_filter, val);
            }
            else
            {
                PropertyInfo pInfo = t.GetProperty(prop);
                pInfo.SetValue(_filter, val, null);
            }
        }

        private void CreateExpression(Utilities.ContinuationOperator continuation, Expression expr)
        {
            CreateExpression(continuation, expr, false);
        }

        private void CreateExpression(Utilities.ContinuationOperator continuation, Expression expr, bool neg)
        {
            if (expr.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCall = (expr as MethodCallExpression);

                if (methodCall.Method.Name == "Contains" && methodCall.Arguments[0].NodeType == ExpressionType.NewArrayInit)
                    CreateContainsExpression(continuation, expr as MethodCallExpression, neg);
                else
                    CreateSimpleMethodCall(continuation, methodCall, neg);
            }
            else if (expr.NodeType == ExpressionType.Not)
            {
                UnaryExpression unaryExpr = expr as UnaryExpression;
                CreateExpression(continuation, unaryExpr.Operand, true);
            }
            else
                CreateSimpleExpression(continuation, expr as BinaryExpression, neg);
        }

        private void CreateSimpleExpression(Utilities.ContinuationOperator continuation, BinaryExpression binExpr, bool neg)
        {
            string fieldName = "";
            string fieldValue = "";

            ExpressionType oper = binExpr.NodeType;

            ExpressionType[] binOperators = { ExpressionType.And, ExpressionType.Or, ExpressionType.AndAlso, ExpressionType.OrElse };

            if (binOperators.Contains(binExpr.NodeType))
            {
                CreateExpression(continuation, binExpr.Left, neg);

                if (binOperators.Contains(binExpr.Left.NodeType) | binOperators.Contains(binExpr.Right.NodeType))
                    _encodedQuery += "NQ";

                CreateExpression((Utilities.ContinuationOperator)binExpr.NodeType, binExpr.Right, neg);
            }
            else
            {
                FlipOperator(binExpr.Left, ref oper);
                SetValues(binExpr.Left, ref fieldName, ref fieldValue);
                if (string.IsNullOrEmpty(fieldName) | string.IsNullOrEmpty(fieldValue))
                {
                    SetValues(binExpr.Right, ref fieldName, ref fieldValue);
                }
            }

            EncodeQuery(continuation, fieldName, (Utilities.RepoExpressionType)oper, fieldValue, neg);
        }

        private void CreateContainsExpression(Utilities.ContinuationOperator continuation, MethodCallExpression methodCall, bool neg)
        {
            string fieldName = "";
            string fieldValue = "";

            SetMethodValues(methodCall, out fieldName, out fieldValue);

            if (string.IsNullOrEmpty(fieldName) | string.IsNullOrEmpty(fieldValue))
            {
                CreateExpression(continuation, methodCall, neg);
            }

            EncodeQuery(continuation, fieldName, Utilities.RepoExpressionType.IN, fieldValue, neg);
        }

        private void SetMethodValues(MethodCallExpression methodCall, out string fieldName, out string fieldValue)
        {
            if (methodCall.Arguments[1].NodeType == ExpressionType.MemberAccess)
            {
                fieldName = GetFieldName(methodCall.Arguments[1]);
                fieldValue = GetFieldValue(methodCall.Arguments[0]);
            }
            else
            {
                fieldName = GetFieldName(methodCall.Arguments[0]);
                fieldValue = GetFieldValue(methodCall.Arguments[1]);
            }
        }

        private void SetValues(Expression expr, ref string fieldName, ref string fieldValue)
        {
            if (expr.NodeType == ExpressionType.Constant)
            {
                fieldValue = GetFieldValue(expr);
            }
            else if (expr.NodeType == ExpressionType.MemberAccess)
            {
                fieldName = GetFieldName(expr);
            }
            else if (expr.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCall = (MethodCallExpression)expr;
                if (methodCall.Arguments.Count > 1)
                {
                    SetMethodValues(methodCall, out fieldName, out fieldValue);
                }
                else
                {
                    if (methodCall.Arguments[0].NodeType == ExpressionType.Constant)
                    {
                        fieldValue = GetFieldValue(expr);
                    }
                    if (methodCall.Arguments[0].NodeType == ExpressionType.MemberAccess)
                    {
                        fieldName = GetFieldName(expr);
                    }
                }
            }
        }

        private void FlipOperator(Expression expr, ref ExpressionType oper)
        {
            Func<ExpressionType, ExpressionType> _flipOperator = o =>
            {
                if (o == ExpressionType.Equal)
                    o = ExpressionType.Equal;

                if (o == ExpressionType.GreaterThan)
                    o = ExpressionType.LessThan;

                if (o == ExpressionType.GreaterThanOrEqual)
                    o = ExpressionType.LessThanOrEqual;

                if (o == ExpressionType.LessThan)
                    o = ExpressionType.GreaterThan;

                if (o == ExpressionType.LessThanOrEqual)
                    o = ExpressionType.GreaterThanOrEqual;

                return o;
            };

            if (expr.NodeType == ExpressionType.Constant)
            {
                oper = _flipOperator(oper);
            }
            else if (expr.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCall = (MethodCallExpression)expr;

                if (methodCall.Arguments[0].NodeType == ExpressionType.Constant)
                {
                    oper = _flipOperator(oper);
                }
            }
        }

        private void CreateSimpleMethodCall(Utilities.ContinuationOperator continuation, MethodCallExpression methodCall, bool neg)
        {
            string fieldName = "";
            string fieldValue = "";

            if (methodCall.Object != null)
            {
                fieldName = GetFieldName(methodCall.Object);
                fieldValue = GetFieldValue(methodCall.Arguments[0]);
            }
            else
            {
                SetMethodValues(methodCall, out fieldName, out fieldValue);
            }

            if (string.IsNullOrEmpty(fieldName) | string.IsNullOrEmpty(fieldValue))
            {
                CreateExpression(continuation, methodCall, neg);
            }

            EncodeQuery(continuation, fieldName, Utilities.GetRepoExpressionType(methodCall.Method.Name), fieldValue, neg);
        }

        private string GetFieldName(Expression expr)
        {
            string fieldname = "";

            if (expr.NodeType == ExpressionType.MemberAccess)
                fieldname = GetPropertyName(expr);

            if (expr.NodeType == ExpressionType.Call && (expr as MethodCallExpression).Method.Name == "Parse")
                fieldname = GetPropertyName((expr as MethodCallExpression).Arguments[0]);

            return fieldname;
        }

        private string GetFieldValue(Expression expr)
        {
            string fieldvalue = "";

            if (expr.NodeType == ExpressionType.Constant)
                fieldvalue = expr.ToString().Replace("\"", "");

            if (expr.NodeType == ExpressionType.MemberAccess)
                fieldvalue = GetPropertyName(expr);

            if (expr.NodeType == ExpressionType.Call && (expr as MethodCallExpression).Method.Name == "Parse")
                fieldvalue = (expr as MethodCallExpression).Arguments[0].ToString().Replace("\"", "");

            if (expr.NodeType == ExpressionType.Convert)
                fieldvalue = GetFieldValue((expr as UnaryExpression).Operand);

            if (expr.NodeType == ExpressionType.NewArrayInit)
                fieldvalue = string.Join(",", (expr as NewArrayExpression).Expressions.Select(o => o.ToString().Replace("\"", "")).ToArray());

            return fieldvalue;
        }

        private string GetPropertyName(Expression propertyRefExpr)
        {
            if (propertyRefExpr == null)
            {
                throw new ArgumentNullException("propertyRefExpr", "propertyRefExpr is null.");
            }

            if (propertyRefExpr.NodeType == ExpressionType.Constant)
            {
                return propertyRefExpr.ToString();
            }

            MemberExpression memberExpr = propertyRefExpr as MemberExpression;
            if (memberExpr == null)
            {
                UnaryExpression unaryExpr = propertyRefExpr as UnaryExpression;
                if (unaryExpr != null && unaryExpr.NodeType == ExpressionType.Convert)
                {
                    memberExpr = unaryExpr.Operand as MemberExpression;
                }
            }

            if (memberExpr != null && memberExpr.Member.MemberType == System.Reflection.MemberTypes.Property)
            {
                return memberExpr.Member.Name;
            }

            if (memberExpr != null && memberExpr.Member.MemberType == System.Reflection.MemberTypes.Field)
            {
                return memberExpr.Member.Name;
            }

            throw new ArgumentException("No property reference expression was found.", "propertyRefExpr");
        }

        private void AppendGroupBy()
        {
            if (String.IsNullOrEmpty(_groupbyQuery))
                return;

            _encodedQuery += _groupbyQuery;
            SetFilterProperty("__encoded_query", _encodedQuery);
        }

        private void EncodeQuery(Utilities.ContinuationOperator continuationOperator, string fieldName, Utilities.RepoExpressionType myOperator, string myValue, bool neg)
        {
            if (string.IsNullOrEmpty(fieldName) | string.IsNullOrEmpty(myValue))
            {
                return;
            }

            string oper = Utilities.GetRepoExpressionType(myOperator);

            if (neg)
                oper = Utilities.NegateRepoExpressionType(myOperator);

            string query = string.Format("{0}{1}{2}", fieldName, oper, myValue);

            if (!string.IsNullOrEmpty(_encodedQuery) && !(_encodedQuery.EndsWith("NQ")))
            {
                query = Utilities.GetContinuationOperator(continuationOperator) + query;
            }

            _encodedQuery += query;

            SetFilterProperty("__encoded_query", _encodedQuery);
        }

        private void SetOrdering(string order, Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> field)
        {
            if (field.Body.NodeType == ExpressionType.New)
            {
                SetFilterProperty(order, string.Join(",", (field.Body as NewExpression).Arguments.Select(o => GetPropertyName(o)).ToArray()));
            }
            else
            {
                SetFilterProperty(order, GetPropertyName(field.Body));
            }
        }

        private TGetRecordsResponseGetRecordsResult[] GetRecords()
        {
            Type t = proxyUser.GetType();
            MethodInfo methodInfo = t.GetMethod("getRecords");

            TGetRecordsResponseGetRecordsResult[] ret = (TGetRecordsResponseGetRecordsResult[])methodInfo.Invoke(proxyUser, new object[] { _filter });

            _encodedQuery = string.Empty;
            _filter = new TGetRecords();

            return ret;
        }

        public dynamic First(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            SetFilterProperty("__limit", "1");
            if(stmt.Body.NodeType != ExpressionType.Constant)
                Where(stmt);
            return ToArray().First();
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Take(int count)
        {
            SetFilterProperty("__last_row", count.ToString());
            return this;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Skip(int count)
        {
            SetFilterProperty("__first_row", count.ToString());
            return this;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Range(int start, int last)
        {
            SetFilterProperty("__first_row", (start - 1).ToString());
            SetFilterProperty("__last_row", ((start - 1) + last).ToString());
            return this;
        }

        public dynamic[] ToArray()
        {
            TGetRecordsResponseGetRecordsResult[] ret = GetRecords();

            if (_selectQuery != null)
                return ret.Select(o => _selectQuery(o)).ToArray();
            else
                return ret.Cast<dynamic>().ToArray();
        }

        public Dictionary<U, dynamic> ToDictionary<U>(Func<TGetRecordsResponseGetRecordsResult, U> keySelector)
        {
            TGetRecordsResponseGetRecordsResult[] ret = GetRecords();

            if (_selectQuery != null)
                return ret.ToDictionary(o => keySelector(o), p => _selectQuery(p));
            else
                return ret.ToDictionary(o => keySelector(o), o => (dynamic)o);
        }

        public List<dynamic> ToList()
        {
            return ToArray().ToList();
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> @Select<U>(Func<TGetRecordsResponseGetRecordsResult, U> selector)
        {
            _selectQuery = (s) => selector(s);
            return this;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Where(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            _encodedQuery += "NQ";
            CreateExpression(Utilities.ContinuationOperator.And, stmt.Body);
            return this;
        }

        public dynamic First()
        {
            return First((o) => true);
        }

        public dynamic Single()
        {
            return First();
        }

        public dynamic Single(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            return First(stmt);
        }

        public bool Any()
        {
            return First() != null;
        }

        public bool Any(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            return First(stmt) != null;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> OrderBy(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> field)
        {
            SetOrdering("__order_by", field);

            return this;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> ThenBy(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> source)
        {
            return OrderBy(source);
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> OrderByDescending(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> field)
        {
            SetOrdering("__order_by_desc", field);

            return this;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> ThenByDescending(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> source)
        {
            return OrderByDescending(source);
        }

		public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> GroupBy<U, V>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> keySelector,
			Expression<Func<TGetRecordsResponseGetRecordsResult, V>> elementSelector)
		{
            Select(elementSelector.Compile());
			return GroupBy(keySelector);
		}

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> GroupBy<U, V>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> keySelector,
            Expression<Func<U, IEnumerable<TGetRecordsResponseGetRecordsResult>, V>> resultSelector)
        {
            return GroupBy(keySelector);
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> GroupBy<U, V, W>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> keySelector, 
            Expression<Func<TGetRecordsResponseGetRecordsResult, V>> elementSelector, 
            Expression<Func<U, IEnumerable<TGetRecordsResponseGetRecordsResult>, W>> resultSelector)
        {
            Select(elementSelector.Compile());
            return GroupBy(keySelector);
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> GroupBy<U>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> field)
        {
            Action<Expression> setGroupBy = expr =>
            {
                string query;

                if (expr.NodeType == ExpressionType.New)
                    query = "^GROUPBY" + String.Join(",", (expr as NewExpression).Arguments.Select(o => GetPropertyName(o)).ToArray());
                else
                    query = "^GROUPBY" + GetPropertyName(expr);

                _groupbyQuery += query;
            };

            if(field.Body.NodeType == ExpressionType.Convert)
            {
                UnaryExpression unaryExpression  = (field.Body as UnaryExpression);
                setGroupBy(unaryExpression.Operand);
            }
            else
            {
                setGroupBy(field.Body);
            }

            return this;
        }

        public IEnumerable<dynamic> Join<T, U, V>(ServiceNowRepository<T, U, V> serviceNowRepository,
            Func<TGetRecordsResponseGetRecordsResult, dynamic> outerKeySelector,
            Func<V, object> innerKeySelector,
            Func<TGetRecordsResponseGetRecordsResult, V, dynamic> resultSelector)
            where T : new()
            where U : new()
        {
            dynamic query = this.ToList().Join(serviceNowRepository.ToList(),
                o => outerKeySelector(o),
                o => innerKeySelector(o),
                (o, p) => resultSelector(o, p));

            return query;
        }

        public IEnumerable<dynamic> Join<T, U, V, W>(ServiceNowRepository<T, U, V> serviceNowRepository,
            Func<TGetRecordsResponseGetRecordsResult, dynamic> outerKeySelector,
            Func<V, object> innerKeySelector,
            Func<TGetRecordsResponseGetRecordsResult, V, W> resultSelector)
            where T : new()
            where U : new()
        {
            dynamic query = this.ToList().Join(serviceNowRepository.ToList(),
                o => outerKeySelector(o),
                o => innerKeySelector(o),
                (o, p) => resultSelector(o, p));

            return query;
        }

        public dynamic Insert<TInsert>(TInsert _insert)
        {
            Type t = proxyUser.GetType();
            MethodInfo methodInfo = t.GetMethod("insert");
            return methodInfo.Invoke(proxyUser, new object[]{_insert});
        }

        public dynamic Update<TUpdate>(TUpdate _update) 
        {
            Type t  = proxyUser.GetType();
            MethodInfo methodInfo = t.GetMethod("update");
            return methodInfo.Invoke(proxyUser, new object[]{_update});
        }

        public dynamic Delete<TDelete>(TDelete _delete) where TDelete : new()
        {
            Type t  = proxyUser.GetType();
            MethodInfo methodInfo = t.GetMethod("deleteRecord");
            return methodInfo.Invoke(proxyUser, new object[] { _delete });
        }

        public IEnumerator<ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult>> GetEnumerator()
        {
            return ToList().Cast<ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult>>().GetEnumerator();
        }

        public IEnumerator GetEnumerator1()
        {
            return this.GetEnumerator();
        }
    }
}
