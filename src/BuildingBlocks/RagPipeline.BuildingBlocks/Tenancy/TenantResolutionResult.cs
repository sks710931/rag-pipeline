namespace RagPipeline.BuildingBlocks.Tenancy;

using Microsoft.AspNetCore.Http;

public sealed class TenantResolutionResult
{
    private TenantResolutionResult(bool isSuccess, ITenantContext? context, int statusCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Context = context;
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public ITenantContext? Context { get; }

    public int StatusCode { get; }

    public string? ErrorMessage { get; }

    public static TenantResolutionResult Success(ITenantContext context) =>
        new(true, context, StatusCodes.Status200OK, null);

    public static TenantResolutionResult Denied(int statusCode, string errorMessage) =>
        new(false, null, statusCode, errorMessage);
}
