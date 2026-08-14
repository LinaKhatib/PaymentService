using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Data.Interfaces;
using TransactionService.Data.Repositories;
using TransactionService.Models;

namespace TransactionService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IOperationRepository, OperationRepository>();
        
        services.AddScoped<IEventRepository, EventRepository>();

        services.AddDbContext<PaymentDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Строка подключения 'DefaultConnection' не найдена в файле конфигурации!");
            }

            options.UseSqlite(connectionString);
        });
        
        return services;
    }
}