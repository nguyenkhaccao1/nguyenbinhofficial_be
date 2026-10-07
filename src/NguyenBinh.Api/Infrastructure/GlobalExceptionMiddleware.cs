using System.Text.Json;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Infrastructure;

/// <summary>
/// Chuyen exception thanh ApiResponse voi status dung ngu nghia. Khong bao gio tra stack trace;
/// loi 500 chi tra ma tham chieu (correlation id) de tra log.
/// </summary>
internal sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> jsonOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client da huy request — khong phai loi server.
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            var (status, body) = Map(ex, context);
            if (status >= 500)
                logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                logger.LogInformation("Request failed {Status}: {Message}", status, ex.Message);

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, body, jsonOptions.Value.JsonSerializerOptions);
        }
    }

    private static (int Status, ApiResponse<object?> Body) Map(Exception ex, HttpContext context) => ex switch
    {
        ValidationException v => (StatusCodes.Status422UnprocessableEntity,
            ApiResponse.Fail("Dữ liệu không hợp lệ.", ValidationErrors.From(v.Errors))),
        BusinessValidationException b => (StatusCodes.Status422UnprocessableEntity, ApiResponse.Fail(b.Message, b.Errors)),
        NotFoundException => (StatusCodes.Status404NotFound, ApiResponse.Fail(ex.Message)),
        ConflictException c => (StatusCodes.Status409Conflict, WithDetails(c)),
        ForbiddenException => (StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message)),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, ApiResponse.Fail(ex.Message)),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
            ApiResponse.Fail("Dữ liệu đã bị người khác thay đổi. Vui lòng tải lại trước khi lưu.")),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict,
            ApiResponse.Fail("Dữ liệu bị trùng (slug, mã hoặc tên đã tồn tại).")),
        BadHttpRequestException bad => (bad.StatusCode, ApiResponse.Fail(
            bad.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Dữ liệu gửi lên quá lớn." : "Yêu cầu không hợp lệ.")),
        _ => (StatusCodes.Status500InternalServerError, ApiResponse.Fail(
            $"Đã có lỗi xảy ra. Mã tham chiếu: {context.Items[CorrelationIdMiddleware.ItemKey] ?? context.TraceIdentifier}")),
    };

    private static ApiResponse<object?> WithDetails(AppException ex) =>
        new() { Success = false, Message = ex.Message, Data = ex.Details };
}
