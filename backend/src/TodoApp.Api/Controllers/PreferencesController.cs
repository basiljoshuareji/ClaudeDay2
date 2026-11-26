using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Dtos;
using TodoApp.Api.Services;

namespace TodoApp.Api.Controllers;

[Route("api/preferences")]
public class PreferencesController(IPreferenceService preferences) : ApiControllerBase
{
    [HttpGet]
    public Task<PreferenceDto> Get(CancellationToken ct) => preferences.GetAsync(UserId, ct);

    /// <summary>Saves the user's theme (light, dark, ocean, forest, sunset) and UI preferences.</summary>
    [HttpPut]
    public Task<PreferenceDto> Update(UpdatePreferencesRequest request, CancellationToken ct) =>
        preferences.UpdateAsync(UserId, request, ct);
}
