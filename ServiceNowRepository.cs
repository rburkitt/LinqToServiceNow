﻿using System;
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

        private Func<object, TGetRecordsResponseGetRecordsResult> _selectQuery;

        private string _encodedQuery;
        private string _groupbyQuery;

        protected System.Net.NetworkCredential @Credential { get; set; }

        private void SetFilterProperty(string prop, string val)
        {
            Type t = _filter.GetType();
            FieldInfo fInfo = t.GetField(prop);
            if (fInfo != null)
            {
                if (prop == "__order_by")
                {
                    object existing_value = fInfo.GetValue(_filter);
                    if (existing_value != null)
                        val = existing_value.ToString() + "," + val;
                }

                fInfo.SetValue(_filter, val);
            }
            else
            {
                PropertyInfo pInfo = t.GetProperty(prop);

                if (prop == "__order_by")
                {
                    object existing_value = pInfo.GetValue(_filter);
                    if (existing_value != null)
                        val = existing_value.ToString() + "," + val;
                }

                pInfo.SetValue(_filter, val, null);
            }
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
                return;

            string oper = Utilities.GetRepoExpressionType(myOperator);

            if (neg)
                oper = Utilities.NegateRepoExpressionType(myOperator);

            string query = string.Format("{0}{1}{2}", fieldName, oper, myValue);

            if (!string.IsNullOrEmpty(_encodedQuery) && !(_encodedQuery.EndsWith("NQ")))
                query = Utilities.GetContinuationOperator(continuationOperator) + query;

            _encodedQuery += query;

            SetFilterProperty("__encoded_query", _encodedQuery);
        }

        private void SetOrdering(string order, Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> field)
        {
            SetFilterProperty(order, GetOrdering(field));
        }

        private string GetOrdering(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> field)
        {
            if (field.Body.NodeType == ExpressionType.New)
                return string.Join(",", (field.Body as NewExpression).Arguments.Select(o => Utilities.GetPropertyName(o)).ToArray());
            else
                return Utilities.GetPropertyName(field.Body);
        }

        private bool IsLimited()
        {
            bool retVal;

            Type t = _filter.GetType();

            PropertyInfo[] propInfo = t.GetProperties();

            if (propInfo.Length > 0)
                retVal = propInfo.Any(o => new string[] { "__last_row", "__limit" }.Contains(o.Name) & o.GetValue(_filter) != null);
            else
            {
                FieldInfo[] fieldInfo = t.GetFields();
                retVal = fieldInfo.Any(o => new string[] { "__last_row", "__limit" }.Contains(o.Name) & o.GetValue(_filter) != null);
            }

            return retVal;
        }

        private string GetFirstRow()
        {
            string retVal = "0";

            Type t = _filter.GetType();

            PropertyInfo propInfo = t.GetProperty("__first_row");

            if (propInfo != null)
            {
                object obj = propInfo.GetValue(_filter);

                if (obj != null)
                    retVal = obj.ToString();
            }
            else
            {
                FieldInfo fieldInfo = t.GetField("__first_row");
                object obj = fieldInfo.GetValue(_filter);

                if (obj != null)
                    retVal = obj.ToString();
            }

            return retVal;
        }

        private void SetWebReferenceCredentials(MemberInfo[] info)
        {
            foreach (PropertyInfo p in info.Where(o => o.MemberType == MemberTypes.Property))
                p.SetValue(proxyUser, Credential);

            foreach (FieldInfo f in info.Where(o => o.MemberType == MemberTypes.Field))
                f.SetValue(proxyUser, Credential);
        }

        private void SetServiceReferenceCredentials(MemberInfo[] info)
        {
            foreach (PropertyInfo p in info.Where(o => o.MemberType == MemberTypes.Property))
            {
                var pUserName = p.PropertyType.GetProperty("UserName");
                var userName = pUserName.GetValue(p.GetValue(proxyUser));
                pUserName.PropertyType.GetProperty("UserName").SetValue(userName, Credential.UserName);
                pUserName.PropertyType.GetProperty("Password").SetValue(userName, Credential.Password);
            }

            foreach (FieldInfo f in info.Where(o => o.MemberType == MemberTypes.Field))
            {
                var pUserName = f.FieldType.GetProperty("UserName");
                var userName = pUserName.GetValue(f.GetValue(proxyUser));
                pUserName.PropertyType.GetProperty("UserName").SetValue(userName, Credential.UserName);
                pUserName.PropertyType.GetProperty("Password").SetValue(userName, Credential.Password);
            }
        }

        private void SetCredentials(Type t)
        {
            if (Credential != null)
            {
                try
                {
                    if (t.BaseType == typeof(System.Web.Services.Protocols.SoapHttpClientProtocol))
                        SetWebReferenceCredentials(t.GetMember("Credentials"));
                    else
                        SetServiceReferenceCredentials(t.GetMember("ClientCredentials"));
                }
                catch
                {
                    throw new Exception("Exception while setting security credentials for web service.");
                }
            }
        }

        private TGetRecordsResponseGetRecordsResult[] GetRecords()
        {
            Type t = proxyUser.GetType();

            SetCredentials(t);

            MethodInfo methodInfo = t.GetMethod("getRecords");

            AppendGroupBy();

            SetFilterProperty("__encoded_query", _encodedQuery);

            if (!IsLimited())
            {
                var list = new List<TGetRecordsResponseGetRecordsResult>();
                int first = int.Parse(GetFirstRow());
                int last = 250;

                var rslt = (object[])methodInfo.Invoke(proxyUser, new object[] { Range(first, last)._filter });

                do
                {
                    if (_selectQuery != null)
                        list.AddRange(rslt.Select(o => _selectQuery(o)).ToArray());
                    else
                        list.AddRange(rslt.Cast<TGetRecordsResponseGetRecordsResult>().ToArray());

                    first += 250;
                    rslt = (object[])methodInfo.Invoke(proxyUser, new object[] { Range(first, last)._filter });
                }
                while (rslt.Count() > 0);

                return list.ToArray();
            }
            else
            {
                if (_selectQuery != null)
                {
                    object[] ret = (object[])methodInfo.Invoke(proxyUser, new object[] { _filter });
                    return ret.Select(o => _selectQuery(o)).ToArray();
                }
                else
                {
                    TGetRecordsResponseGetRecordsResult[] ret = (TGetRecordsResponseGetRecordsResult[])methodInfo.Invoke(proxyUser, new object[] { _filter });
                    return ret.ToArray();
                }
            }
        }

        public dynamic First(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retVal = this.DeepCopy();

            retVal.SetFilterProperty("__limit", "1");

            if (stmt.Body.NodeType != ExpressionType.Constant)
                retVal.Where(stmt);

            return retVal.ToArray().FirstOrDefault();
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Take(int count)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retVal = this.DeepCopy();

            int lastRow = count + int.Parse(GetFirstRow());

            retVal.SetFilterProperty("__last_row", lastRow.ToString());

            return retVal;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Skip(int count)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retVal = this.DeepCopy();

            retVal.SetFilterProperty("__first_row", count.ToString());

            return retVal;
        }

        private ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Range(int start, int last)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retVal = this.DeepCopy();

            retVal.SetFilterProperty("__first_row", start.ToString());
            retVal.SetFilterProperty("__last_row", (start + last).ToString());

            return retVal;
        }

        public dynamic ElementAt(int at)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retval = this.DeepCopy();
            return retval.Range(at - 1, at).First();
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> DeepCopy()
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> other =
                this.MemberwiseClone() as ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult>;

            FillCopy(other);

            return other;
        }

        private void FillCopy<TGetRecs, TGetRecsResponseGetRecsResult>(ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecs, TGetRecsResponseGetRecsResult> other)
            where TGetRecs : new()
        {
            other._filter = new TGetRecs();

            var fieldNames = new string[] { "__encoded_query", "__limit", "__first_row", "__last_row", "__order_by", "__order_by_desc" };

            Type t = _filter.GetType();
            t.GetFields().Where(o => fieldNames.Contains(o.Name) && o.GetValue(_filter) != null).ToList()
                .ForEach(o => other.SetFilterProperty(o.Name, o.GetValue(_filter).ToString()));

            t.GetProperties().Where(o => fieldNames.Contains(o.Name) && o.GetValue(_filter) != null).ToList()
                .ForEach(o => other.SetFilterProperty(o.Name, o.GetValue(_filter).ToString()));
        }

        public TGetRecordsResponseGetRecordsResult[] ToArray()
        {
            TGetRecordsResponseGetRecordsResult[] ret = GetRecords();

            return ret;
        }

        public Dictionary<U, TGetRecordsResponseGetRecordsResult> ToDictionary<U>(Func<TGetRecordsResponseGetRecordsResult, U> keySelector)
        {
            TGetRecordsResponseGetRecordsResult[] ret = GetRecords();

            return ret.ToDictionary(o => keySelector(o), o => o);
        }

        public Dictionary<U, V> ToDictionary<U, V>(Func<TGetRecordsResponseGetRecordsResult, U> keySelector, Func<TGetRecordsResponseGetRecordsResult, V> elementSelector)
        {
            TGetRecordsResponseGetRecordsResult[] ret = GetRecords();

            return ret.ToDictionary(o => keySelector(o), p => elementSelector(p));
        }

        public List<TGetRecordsResponseGetRecordsResult> ToList()
        {
            return ToArray().ToList();
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, U> @Select<U>(Func<TGetRecordsResponseGetRecordsResult, U> selector)
        {
            var other = new ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, U>();
            other._selectQuery = (s) => selector((TGetRecordsResponseGetRecordsResult)s);
            other.Credential = Credential;
            other._encodedQuery = _encodedQuery;
            other._groupbyQuery = _groupbyQuery;

            FillCopy(other);

            return other;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> Where(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            if (!(string.IsNullOrEmpty(_encodedQuery)) && !(_encodedQuery.EndsWith("NQ")))
                _encodedQuery += "NQ";

            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retval = this.DeepCopy();

            retval._encodedQuery += (new ExpressionVisitor()).VisitExpression(Utilities.ContinuationOperator.And, stmt.Body);
            return retval;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> SkipWhile(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retval = this.DeepCopy();
            retval._encodedQuery += "^" + (new ExpressionVisitor()).VisitExpression(Utilities.ContinuationOperator.And, stmt.Body, true);
            return retval;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> TakeWhile(Expression<Func<TGetRecordsResponseGetRecordsResult, bool>> stmt)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retval = this.DeepCopy();
            retval._encodedQuery += "^" + (new ExpressionVisitor()).VisitExpression(Utilities.ContinuationOperator.And, stmt.Body);
            return retval;
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
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retVal = this.DeepCopy();

            retVal.SetOrdering("__order_by", field);

            return retVal;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> ThenBy(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> source)
        {
            return OrderBy(source);
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> OrderByDescending(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> field)
        {
            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retVal = this.DeepCopy();

            retVal.SetOrdering("__order_by_desc", field);

            return retVal;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> ThenByDescending(Expression<Func<TGetRecordsResponseGetRecordsResult, dynamic>> source)
        {
            return OrderByDescending(source);
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, V> GroupBy<U, V>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> keySelector,
            Expression<Func<TGetRecordsResponseGetRecordsResult, V>> elementSelector)
        {
            var selected = Select(elementSelector.Compile());
            selected._groupbyQuery = GroupBy(keySelector)._groupbyQuery;
            return selected;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> GroupBy<U, V>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> keySelector,
            Expression<Func<U, IEnumerable<TGetRecordsResponseGetRecordsResult>, V>> resultSelector)
        {
            return GroupBy(keySelector);
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, V> GroupBy<U, V, W>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> keySelector,
            Expression<Func<TGetRecordsResponseGetRecordsResult, V>> elementSelector,
            Expression<Func<U, IEnumerable<TGetRecordsResponseGetRecordsResult>, W>> resultSelector)
        {
            var selected = Select(elementSelector.Compile());
            selected._groupbyQuery = GroupBy(keySelector)._groupbyQuery;
            return selected;
        }

        public ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> GroupBy<U>(Expression<Func<TGetRecordsResponseGetRecordsResult, U>> field)
        {
            Action<Expression> setGroupBy = expr =>
            {
                string query;

                if (expr.NodeType == ExpressionType.New)
                    query = "^GROUPBY" + String.Join(",", (expr as NewExpression).Arguments.Select(o => Utilities.GetPropertyName(o)).ToArray());
                else
                    query = "^GROUPBY" + Utilities.GetPropertyName(expr);

                _groupbyQuery += query;
            };

            if (field.Body.NodeType == ExpressionType.Convert)
            {
                UnaryExpression unaryExpression = (field.Body as UnaryExpression);
                setGroupBy(unaryExpression.Operand);
            }
            else
                setGroupBy(field.Body);

            ServiceNowRepository<TServiceNow_cmdb_ci_, TGetRecords, TGetRecordsResponseGetRecordsResult> retval = this.DeepCopy();

            return retval;
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

        public string Insert<TInsert>(TInsert _insert)
        {
            Type t = proxyUser.GetType();
            SetCredentials(t);
            MethodInfo methodInfo = t.GetMethod("insert");
            return ((dynamic)methodInfo.Invoke(proxyUser, new object[] { _insert })).sys_id;
        }

        public string Update<TUpdate>(TUpdate _update)
        {
            Type t = proxyUser.GetType();
            SetCredentials(t);
            MethodInfo methodInfo = t.GetMethod("update");
            return ((dynamic)methodInfo.Invoke(proxyUser, new object[] { _update })).sys_id;
        }

        public string Delete<TDelete>(TDelete _delete) where TDelete : new()
        {
            Type t = proxyUser.GetType();
            SetCredentials(t);
            MethodInfo methodInfo = t.GetMethod("deleteRecord");
            return ((dynamic)methodInfo.Invoke(proxyUser, new object[] { _delete })).count;
        }

        public IEnumerator<TGetRecordsResponseGetRecordsResult> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        public IEnumerator GetEnumerator1()
        {
            return this.GetEnumerator();
        }

        public ServiceNowRepository() { }

        public ServiceNowRepository(System.Net.NetworkCredential credential)
        {
            this.Credential = credential;
        }
    }
}