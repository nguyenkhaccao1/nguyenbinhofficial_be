using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Api.Infrastructure;
using NguyenBinh.Application.Leads;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers;

/// <summary>Form lien he / bao gia / demo tren website. Gioi han 5 lan/phut/IP; chong bot bang honeypot + thoi gian dien form.</summary>
[Route("api/v1/leads")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicForms)]
public sealed class PublicLeadsController(ILeadService leads) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<LeadSubmitResult>>> Submit(SubmitLeadRequest request, CancellationToken ct)
    {
        var result = await leads.SubmitAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(), ct);
        return Success(result, result.Message);
    }
}

[Route("api/v1/admin/leads")]
public sealed class LeadsAdminController(ILeadService leads) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Leads.View)]
    public async Task<ActionResult<ApiResponse<PagedResult<LeadListItem>>>> List([FromQuery] LeadListQuery query, CancellationToken ct) =>
        Success(await leads.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Leads.View)]
    public async Task<ActionResult<ApiResponse<LeadDto>>> Get(Guid id, CancellationToken ct) => Success(await leads.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Leads.Update)]
    public async Task<ActionResult<ApiResponse<LeadDto>>> Update(Guid id, UpdateLeadRequest request, CancellationToken ct) =>
        Success(await leads.UpdateAsync(id, request, ct), "Đã lưu.");

    [HttpPost("{id:guid}/notify")]
    [HasPermission(Permissions.Leads.Update)]
    public async Task<ActionResult<ApiResponse<object?>>> Resend(Guid id, CancellationToken ct)
    {
        await leads.ResendNotificationAsync(id, ct);
        return Success("Đã đưa vào hàng đợi gửi lại email.");
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Leads.Delete)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken ct)
    {
        await leads.DeleteAsync(id, ct);
        return Success("Đã xoá.");
    }
}
