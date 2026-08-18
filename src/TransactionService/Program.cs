using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Data.DTOs;
using TransactionService.Extensions;
using TransactionService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDataAccess(builder.Configuration);

builder.Services.AddApplicationServices();

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

// Тестовые эндпонты

app.MapPost("/operations", async (OperationRequest request, IOperationService service, ILogger<Program> logger) =>
{
    logger.LogInformation("--- POST /operations: OperationId={OperationId}, Amount={Amount}, Currency={Currency}", 
        request.OperationId, request.Amount, request.Currency);
    
    try
    {
        var result = await service.CreateOperationAsync(request);
        logger.LogInformation($"--- Операция создана: {result.OperationId}, статус: {result.Status}");
        
        return Results.Created($"/operations/{result.OperationId}", result);
    }
    catch (Exception e)
    {
        logger.LogError(e, $"--- Ошибка при создании операции {request.OperationId}");
        return Results.Conflict(e.Message);
    }
});

app.MapGet("/operations/{id}", async (string id, IOperationService service, ILogger<Program> logger) =>
{
    logger.LogInformation($"--- GET /operations/{id}");
    try
    {
        var result = await service.GetOperationAsync(id);
        logger.LogInformation($"--- Операция {id} имеет статус {result.Status}");
                
        return Results.Ok(result);
    }
    catch (Exception e)
    {
        logger.LogError(e, $"--- Операция с Id {id} не найдена");
        return Results.NotFound();
    }
});

app.MapPost("/operations/{id}/submit", async (string id, IOperationService service, ILogger<Program> logger) =>
{
    logger.LogInformation($"--- POST /operations/{id}/submit");
    try
    {
        var (response, statusChanged) = await service.SubmitOperationAsync(id);
        
        if (statusChanged)
        {
            logger.LogInformation($"--- Отправлен запрос провайдеру на создание операции {id}");
            return Results.Accepted($"/operations/{id}", response);
        }
        
        logger.LogInformation($"--- Запрос на создание операции {id} ранее уже был отправлен провайдеру");
        return Results.Ok(response);
    }
    catch (Exception e)
    {
        logger.LogError(e, $"--- Операция с Id {id} не найдена");
        return Results.NotFound();
    }
});

app.MapGet("/operations/{id}/events", async (string id, IEventService? service, ILogger<Program> logger) =>
{
    logger.LogInformation($"--- GET /operations/{id}/events");
    try
    {
        var events = await service.GetEventsByOperationIdAsync(id);
        if (!events.Any() || events == null)
        {
            logger.LogInformation($"--- События операции {id} не найдены");
            return Results.NotFound($"События операции {id} не найдены.");
        }
        
        logger.LogInformation($"--- Найдено {events.Count()} событий для операции {id}");
        return Results.Ok(events);
    }
    catch (Exception e)
    {
        logger.LogError(e, "--- При получении событий произошла ошибка");
        return Results.Problem("При получении событий произошла ошибка");
    }
});

app.Run();
