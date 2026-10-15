namespace RagPipeline.TenantManagement;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.BuildingBlocks.Authorization;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed record TenantSettingsResponse(Guid TenantId, string DisplayName, string SettingsJson);
public sealed record UpdateTenantSettingsRequest(string DisplayName, string SettingsJson);
public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string ActorType,
    Guid? TenantId,
    string Action,
    string TargetResource,
    string? TargetId,
    string Outcome,
    string? CorrelationId,
    string? DetailsJson);
public sealed record PagedAuditEventsResponse(
    IReadOnlyList<AuditEventResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed class TenantManagementModule : IModule
{
    public string Name => "TenantManagement";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var tenantGroup = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .AddEndpointFilter<TenantContextEndpointFilter>();

        tenantGroup.MapGet("/profile", async (
            Guid tenantId,
            [FromServices] ITenantProfileStore profileStore,
            CancellationToken ct) =>
        {
            var profile = await profileStore.GetProfileAsync(tenantId, ct);
            if (profile == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Tenant profile not found.",
                    type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
            }

            return Results.Ok(profile);
        })
        .WithName("GetTenantProfile")
        .WithTags("Tenants")
        .WithSummary("Get tenant profile")
        .Produces<TenantProfileDto>(StatusCodes.Status200OK, "application/json")
        .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
        .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
        .ProducesProblem(StatusCodes.Status404NotFound, "application/problem+json")
        .RequireAuthorization(Policies.TenantSettingsRead);

        tenantGroup.MapGet("/settings", async (
            Guid tenantId,
            [FromServices] ITenantProfileStore profileStore,
            CancellationToken ct) =>
        {
            var profile = await profileStore.GetProfileAsync(tenantId, ct);
            if (profile == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Tenant settings not found.",
                    type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
            }

            return Results.Ok(new TenantSettingsResponse(profile.TenantId, profile.DisplayName, profile.SettingsJson));
        })
        .WithName("GetTenantSettings")
        .WithTags("Tenants")
        .WithSummary("Get tenant settings")
        .Produces<TenantSettingsResponse>(StatusCodes.Status200OK, "application/json")
        .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
        .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
        .ProducesProblem(StatusCodes.Status404NotFound, "application/problem+json")
        .RequireAuthorization(Policies.TenantSettingsRead);

        tenantGroup.MapPut("/settings", async (
            Guid tenantId,
            [FromBody] UpdateTenantSettingsRequest request,
            [FromServices] ITenantProfileStore profileStore,
            CancellationToken ct) =>
        {
            var updated = await profileStore.UpdateProfileAsync(tenantId, request.DisplayName, request.SettingsJson, ct);
            return Results.Ok(new TenantSettingsResponse(updated.TenantId, updated.DisplayName, updated.SettingsJson));
        })
        .WithName("UpdateTenantSettings")
        .WithTags("Tenants")
        .WithSummary("Update tenant settings")
        .Produces<TenantSettingsResponse>(StatusCodes.Status200OK, "application/json")
        .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
        .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
        .ProducesProblem(StatusCodes.Status404NotFound, "application/problem+json")
        .RequireAuthorization(Policies.TenantSettingsWrite);

        tenantGroup.MapGet("/audit", async (
            Guid tenantId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] IAuditEventStore auditStore,
            CancellationToken ct) =>
        {
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 50, 1, 100);
            var skip = (p - 1) * ps;

            var events = await auditStore.GetTenantEventsAsync(tenantId, skip, ps, ct);
            var totalCount = await auditStore.GetTenantEventsCountAsync(tenantId, ct);

            var items = events.Select(e => new AuditEventResponse(
                e.Id,
                e.OccurredAt,
                e.ActorUserId,
                e.ActorType,
                e.TenantId,
                e.Action,
                e.TargetResource,
                e.TargetId,
                e.Outcome,
                e.CorrelationId,
                e.DetailsJson)).ToList();

            return Results.Ok(new PagedAuditEventsResponse(items, p, ps, totalCount));
        })
        .WithName("GetTenantAuditEvents")
        .WithTags("Tenants")
        .WithSummary("Get tenant audit events")
        .Produces<PagedAuditEventsResponse>(StatusCodes.Status200OK, "application/json")
        .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
        .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
        .ProducesProblem(StatusCodes.Status404NotFound, "application/problem+json")
        .RequireAuthorization(Policies.TenantAuditRead);
    }
}
