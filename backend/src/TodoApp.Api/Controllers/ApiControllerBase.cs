using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Infrastructure;

namespace TodoApp.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the authenticated user. Every query is scoped to this id.</summary>
    protected int UserId => User.GetUserId();
}
