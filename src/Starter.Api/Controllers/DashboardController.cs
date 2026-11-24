using Microsoft.AspNetCore.Mvc;
using Starter.Application.Dashboard;

namespace Starter.Api.Controllers;

[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken cancellationToken) => dashboardService.GetAsync(cancellationToken);
}
