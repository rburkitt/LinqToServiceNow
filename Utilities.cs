using System;
using System.ComponentModel;
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
    class Utilities
    {
        public enum RepoExpressionType
        {
            [Description("=")]
            Equal = ExpressionType.Equal,
            [Description("!=")]
            NotEqual = ExpressionType.NotEqual,
            [Description(">")]
            GreaterThan = ExpressionType.GreaterThan,
            [Description("<")]
            LessThan = ExpressionType.LessThan,
            [Description(">=")]
            GreaterThanOrEqual = ExpressionType.GreaterThanOrEqual,
            [Description("<=")]
            LessThanOrEqual = ExpressionType.LessThanOrEqual,
            @IN = 100,
            @STARTSWITH = 200,
            @CONTAINS = 300,
            @ENDSWITH = 400,
            [Description("LIKE")]
            LIKESTRING = 500
        }

        public enum ContinuationOperator
        {
            [Description("^")]
            @And = ExpressionType.And,
            [Description("^")]
            @AndAlso = ExpressionType.AndAlso,
            [Description("^OR")]
            @Or = ExpressionType.Or,
            [Description("^OR")]
            @OrElse = ExpressionType.OrElse
        }

        private static string GetOperator<T>(T en)
        {
	        Type type = en.GetType();

	        MemberInfo[] memInfo = type.GetMember(en.ToString());

	        if (memInfo != null & memInfo.Length > 0) {
		        object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

		        if (attrs != null & attrs.Length > 0) {
			        return ((DescriptionAttribute)attrs[0]).Description;
		        }
	        }

	        return en.ToString();
        }

        public static string GetContinuationOperator(ContinuationOperator en)
        {
	        return GetOperator<ContinuationOperator>(en);
        }

        public static string GetRepoExpressionType(RepoExpressionType en)
        {
	        return GetOperator<RepoExpressionType>(en);
        }

        public static RepoExpressionType GetRepoExpressionType(string en)
        {
            RepoExpressionType val = (RepoExpressionType)Enum.Parse(typeof(RepoExpressionType), en.ToUpper());
            return val;
        }

        public static string NegateRepoExpressionType(RepoExpressionType en)
        {
            string oper = GetRepoExpressionType(en);

            switch(en)
            {
                case RepoExpressionType.IN:
                    oper = "NOT IN";
                    break;
                case RepoExpressionType.CONTAINS:
                    oper = "DOES NOT CONTAIN";
                    break;
                case RepoExpressionType.STARTSWITH:
                    oper = "NOT STARTSWITH";
                    break;
                case RepoExpressionType.ENDSWITH:
                    oper = "NOT ENDSWITH";
                    break;
                default:
                    oper = GetRepoExpressionType(FlipRepoExpressionType(en));
                    break;
            }

            return oper;

        }

        public static RepoExpressionType FlipRepoExpressionType(RepoExpressionType en)
        {

            RepoExpressionType oper = default(RepoExpressionType);

            switch (en)
            {
                case RepoExpressionType.GreaterThan:
                    oper = RepoExpressionType.LessThan;
                    break;
                case RepoExpressionType.GreaterThanOrEqual:
                    oper = RepoExpressionType.LessThanOrEqual;
                    break;
                case RepoExpressionType.LessThan:
                    oper = RepoExpressionType.GreaterThan;
                    break;
                case RepoExpressionType.LessThanOrEqual:
                    oper = RepoExpressionType.GreaterThanOrEqual;
                    break;
            }

            return oper;

        }
    }
}
