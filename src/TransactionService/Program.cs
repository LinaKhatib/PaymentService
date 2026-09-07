using Microsoft.EntityFrameworkCore;
using TransactionService.Background;
using TransactionService.Data;
using TransactionService.Data.DTOs;
using TransactionService.Exceptions;
using TransactionService.Extensions;
using TransactionService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()  
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHostedService<PaymentBackgroundService>();

builder.Services.AddHttpClient<IProviderService, ProviderService>(client =>
{
    var providerUrl = Environment.GetEnvironmentVariable("PROVIDER_URL") ?? "http://localhost:8081";
    client.BaseAddress = new Uri(providerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/operations", async (OperationRequest request, IOperationService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "POST /operations: {@OperationInfo}",
        new {
            request.OperationId,
            request.Amount,
            request.Currency
        });

    try
    {
        var result = await service.CreateOperationAsync(request, cancellationToken);
        logger.LogInformation(
            "Операция создана. {@OperationInfo}",
            new {
                request.OperationId,
                request.Amount,
                request.Currency
            });
        
        return Results.Created($"/operations/{result.OperationId}", result);
    }
    catch (ConflictException e)
    {
        logger.LogError(e, 
            "Ошибка при создании операции. {@RequestInfo}",
            new {request.OperationId, Error = e.Message});
        
        return Results.Conflict(e.Message);
    }
});

app.MapGet("/operations/{id}", async (string id, IOperationService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation($"GET /operations/{id}");
    try
    {
        var result = await service.GetOperationAsync(id, cancellationToken);
        logger.LogInformation(
            "Возвращен статус перации. {@RequestInfo}", 
            new {result.OperationId, result.Status});
                
        return Results.Ok(result);
    }
    catch (NotFoundException e)
    {
        logger.LogError(e, 
            "Операция не найдена. {@RequestInfo}",
            new {OperationId = id, Error = e.Message});
        
        return Results.NotFound(e.Message);
    }
});

app.MapPost("/operations/{id}/submit", async (string id, IOperationService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation($"POST /operations/{id}/submit");
    try
    {
        var (response, statusChanged) = await service.SubmitOperationAsync(id, cancellationToken);
        
        if (statusChanged)
        {
            logger.LogInformation(
                "Отправлен запрос провайдеру на создание операции. {@RequestInfo}",
                new {OperationId = id});
            
            return Results.Accepted($"/operations/{id}", response);
        }
        
        logger.LogInformation(
            "Запрос на создание операции ранее уже был отправлен провайдеру. {@RequestInfo}",
            new {OperationId = id});
        
        return Results.Ok(response);
    }
    catch (NotFoundException e)
    {
        logger.LogError(e, 
            "Операция не найдена. {@RequestInfo}",
            new {OperationId = id, Error = e.Message});
        
        return Results.NotFound(e.Message);
    }
});

app.MapGet("/operations/{id}/events", async (string id, IEventService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation($"GET /operations/{id}/events");
    try
    {
        var events = await service.GetEventsByOperationIdAsync(id, cancellationToken);
        
        if (events.Count == 0)
        {
            logger.LogInformation(
                "События операции не найдены. {@RequestInfo}",
                new {OperationId = id});
            
            return Results.NotFound($"События операции {id} не найдены.");
        }
        
        logger.LogInformation(
            "Найдено {Count} событий для операции. {@RequestInfo}",
            events.Count, new {OperationId = id, events});
        
        return Results.Ok(events);
    }
    catch (NotFoundException e)
    {
        logger.LogError(e, 
            "При получении событий произошла ошибка. {@RequestInfo}", 
            new {OperationId = id, Error = e.Message});
        
        return Results.NotFound(e.Message);
    }
});

app.MapPost("/receipts", async (ReceiptRequest receipt, IOperationService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation("POST /receipts");
    try
    {
        await service.HandleReceiptAsync(receipt, cancellationToken);
        
        logger.LogInformation(
            "Получел callback от провайдера по операции. {@RequestInfo}",
            new {receipt.OperationId, receipt.Result});
        
        return Results.NoContent();
    }
    catch (NotFoundException e)
    {
        logger.LogError(e, 
            "Операция не найдена. {@RequestInfo}", 
            new {receipt.OperationId, e.Message});
        
        return Results.NotFound(new { error = e.Message });
    }
    catch (ConflictException e)
    {
        logger.LogError(e, 
            "Пришла конфликтная квитанция от провайдера. {@RequestInfo}", 
            new {receipt.OperationId, e.Message});
        
        return Results.Conflict(new { error = e.Message });
    }
    catch (Exception e)
    {
        logger.LogError(e, 
            "Ошибка при обработке квитанции. {@RequestInfo}", 
            new {receipt.OperationId, e.Message});
        
        return Results.StatusCode(500);
    }
});

app.Run();
