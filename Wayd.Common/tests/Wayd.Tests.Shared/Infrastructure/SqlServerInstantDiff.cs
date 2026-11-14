using System.Linq.Expressions;
using NodaTime;

namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>
/// Evaluates SQL Server's instant date-diff in memory. Those methods throw unless EF translates
/// them, and the fake provider compiles the expression instead.
/// </summary>
internal static class SqlServerInstantDiff
{
    public static Expression Evaluate(Expression expression) => new Rewriter().Visit(expression);

    private sealed class Rewriter : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var visited = (MethodCallExpression)base.VisitMethodCall(node)!;
            if (visited.Method.DeclaringType?.Name != "SqlServerNodaTimeDbFunctionsExtensions"
                || visited.Arguments.Count != 3)
            {
                return visited;
            }

            var start = visited.Arguments[1];
            var end = visited.Arguments[2];
            var nullable = visited.Method.ReturnType == typeof(long?) || visited.Method.ReturnType == typeof(int?);
            var methodName = visited.Method.Name switch
            {
                "DateDiffBigMillisecond" when nullable => nameof(MillisecondsNullable),
                "DateDiffBigMillisecond" => nameof(Milliseconds),
                "DateDiffMillisecond" when nullable => nameof(MillisecondsIntNullable),
                "DateDiffMillisecond" => nameof(MillisecondsInt),
                _ => null,
            };

            if (methodName is null)
            {
                return visited;
            }

            return Expression.Call(
                typeof(Rewriter).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!,
                start,
                end);
        }

        /// <summary>Whole milliseconds of <paramref name="end"/> minus <paramref name="start"/>, truncating a fraction.</summary>
        private static long Milliseconds(Instant start, Instant end) =>
            (end - start).BclCompatibleTicks / TimeSpan.TicksPerMillisecond;

        private static long? MillisecondsNullable(Instant? start, Instant? end) =>
            start is Instant from && end is Instant to ? Milliseconds(from, to) : null;

        private static int MillisecondsInt(Instant start, Instant end) =>
            checked((int)Milliseconds(start, end));

        private static int? MillisecondsIntNullable(Instant? start, Instant? end) =>
            MillisecondsNullable(start, end) is long milliseconds ? checked((int)milliseconds) : null;
    }
}
