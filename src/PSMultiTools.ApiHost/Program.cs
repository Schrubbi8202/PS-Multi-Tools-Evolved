using Microsoft.AspNetCore.Mvc;
using PSMultiTools.Core;
using PSMultiTools.Infrastructure;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("local-dev", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton<IConfigService, IniConfigService>();
builder.Services.AddSingleton<IEntryRoutingService, EntryRoutingService>();
builder.Services.AddSingleton<IJobService, JobService>();
builder.Services.AddSingleton<IToolProcessRunner, ToolProcessRunner>();
builder.Services.AddSingleton<IPS3LibraryService, PS3LibraryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("local-dev");

app.MapGet("/api/system/health", () => Results.Ok(new
{
    status = "ok",
    service = "PSMultiTools.ApiHost",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/config", async ([FromServices] IConfigService configService, CancellationToken ct) =>
{
    var config = await configService.GetConfigAsync(ct);
    return Results.Ok(config);
});

app.MapPut("/api/config", async ([FromBody] ConfigDto dto, [FromServices] IConfigService configService, CancellationToken ct) =>
{
    var updated = await configService.UpdateConfigAsync(dto, ct);
    return Results.Ok(updated);
});

app.MapPost("/api/entry/open-file", ([FromBody] EntryRouteRequestDto request, [FromServices] IEntryRoutingService entryRoutingService) =>
{
    var resolved = entryRoutingService.Resolve(request.Path);
    return Results.Ok(resolved);
});

app.MapGet("/api/platforms", () =>
{
    PlatformSummaryDto[] platforms =
    [
        new("ps1", "PlayStation 1", "PS1 tools and converters", 2),
        new("ps2", "PlayStation 2", "PS2 toolchain", 5),
        new("psx", "PSX", "PSX HDD and XMB tools", 4),
        new("ps3", "PlayStation 3", "PS3 library, tools, and webMAN integration", 20),
        new("ps4", "PlayStation 4", "PS4 exploit and package tools", 11),
        new("ps5", "PlayStation 5", "PS5 management and package tools", 25),
        new("psp", "PlayStation Portable", "PSP conversion tools", 4),
        new("psv", "PlayStation Vita", "PS Vita package and filesystem tools", 5)
    ];
    return Results.Ok(platforms);
});

app.MapGet("/api/ps3/library/items", ([FromServices] IPS3LibraryService ps3Service) =>
{
    return Results.Ok(ps3Service.GetItems());
});

app.MapPost("/api/ps3/library/scan/local", async ([FromBody] LocalScanRequestDto request, [FromServices] IPS3LibraryService ps3Service, CancellationToken ct) =>
{
    var job = await ps3Service.ScanLocalAsync(request, ct);
    return Results.Accepted($"/api/jobs/{job.JobId}", job);
});

app.MapPost("/api/ps3/library/scan/ftp", async ([FromBody] FtpScanRequestDto request, [FromServices] IPS3LibraryService ps3Service, CancellationToken ct) =>
{
    var job = await ps3Service.ScanFtpAsync(request, ct);
    return Results.Accepted($"/api/jobs/{job.JobId}", job);
});

app.MapPost("/api/ps3/library/filter", ([FromBody] LibraryFilterRequestDto request, [FromServices] IPS3LibraryService ps3Service) =>
{
    return Results.Ok(ps3Service.Filter(request));
});

app.MapGet("/api/ps3/actions", ([FromQuery] string? itemId, [FromServices] IPS3LibraryService ps3Service) =>
{
    return Results.Ok(ps3Service.GetActions(itemId));
});

app.MapPost("/api/ps3/actions/{actionId}", async ([FromRoute] string actionId, [FromBody] ExecuteActionRequestDto request, [FromServices] IPS3LibraryService ps3Service, CancellationToken ct) =>
{
    var job = await ps3Service.ExecuteActionAsync(actionId, request, ct);
    return Results.Accepted($"/api/jobs/{job.JobId}", job);
});

app.MapGet("/api/jobs/{jobId:guid}", ([FromRoute] Guid jobId, [FromServices] IJobService jobService) =>
{
    try
    {
        return Results.Ok(jobService.Get(jobId));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapGet("/api/jobs", ([FromServices] IJobService jobService) =>
{
    return Results.Ok(jobService.GetAll());
});

app.MapGet("/api/jobs/{jobId:guid}/events", async ([FromRoute] Guid jobId, HttpContext context, [FromServices] IJobService jobService, CancellationToken ct) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    await foreach (var evt in jobService.Stream(jobId, ct))
    {
        var payload = JsonSerializer.Serialize(evt);
        await context.Response.WriteAsync($"data: {payload}\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
    }
});

app.Run();
