using Wayd.Common.Application.Dtos;

namespace Wayd.Common.Application.Requests.AppIntegration;

public sealed record GetAIConnectionQuery() : IQuery<EnabledAIConnectionDto>;