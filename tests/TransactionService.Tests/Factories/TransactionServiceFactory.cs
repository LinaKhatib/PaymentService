using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionService.Data;

namespace TransactionService.Tests.Factories;

public class TransactionServiceFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PaymentDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<PaymentDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
            
            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            dbContext.Database.Migrate();
        });
    }

    protected override void Dispose(bool builder)
    {
        base.Dispose(builder);
        _connection?.Dispose();
    }
}