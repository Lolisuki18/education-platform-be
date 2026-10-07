using Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply;
using Application.Features.Statistics.Queries.GetAnalyticsGrowth;
using Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth;
using Application.Features.Statistics.Queries.GetSummaryStatistics;
using Application.Features.Statistics.Queries.GetTopPerformance;
using FluentValidation;

namespace Application.Features.Statistics
{
    /// <summary>
    /// The report parameters arrive straight from the query string, where an enum accepts any number. Rejecting
    /// the nonsense here gives the client a 400 it can show instead of a server error.
    /// </summary>
    internal static class StatisticsRules
    {
        public static IRuleBuilderOptions<T, DateTime?> NotBefore<T>(this IRuleBuilder<T, DateTime?> rule, Func<T, DateTime?> from) =>
            rule.Must((request, to) => to == null || from(request) == null || to >= from(request))
                .WithMessage("'To' must not be earlier than 'From'.");
    }

    public class GetSummaryStatisticsQueryValidator : AbstractValidator<GetSummaryStatisticsQuery>
    {
        public GetSummaryStatisticsQueryValidator()
        {
            RuleFor(x => x.To).NotBefore(x => x.From);
        }
    }

    public class GetAnalyticsGrowthQueryValidator : AbstractValidator<GetAnalyticsGrowthQuery>
    {
        public GetAnalyticsGrowthQueryValidator()
        {
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.GroupBy).IsInEnum();
            RuleFor(x => x.RevenueType).IsInEnum();
            RuleFor(x => x.To).NotBefore(x => x.From);
        }
    }

    public class GetAnalyticsNormalizedGrowthQueryValidator : AbstractValidator<GetAnalyticsNormalizedGrowthQuery>
    {
        public GetAnalyticsNormalizedGrowthQueryValidator()
        {
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.GroupBy).IsInEnum();
            RuleFor(x => x.RevenueType).IsInEnum();
            RuleFor(x => x.To).NotBefore(x => x.From);
        }
    }

    public class GetAnalyticsDemandAndSupplyQueryValidator : AbstractValidator<GetAnalyticsDemandAndSupplyQuery>
    {
        public GetAnalyticsDemandAndSupplyQueryValidator()
        {
            RuleFor(x => x.GroupBy).IsInEnum();
            RuleFor(x => x.To).NotBefore(x => x.From);
        }
    }

    public class GetTopPerformanceQueryValidator : AbstractValidator<GetTopPerformanceQuery>
    {
        public GetTopPerformanceQueryValidator()
        {
            RuleFor(x => x.Top).InclusiveBetween(1, 100).WithMessage("Top must be between 1 and 100.");
            RuleFor(x => x.To).NotBefore(x => x.From);
        }
    }
}
