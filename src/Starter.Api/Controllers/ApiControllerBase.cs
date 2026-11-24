using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Starter.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public abstract class ApiControllerBase : ControllerBase;
