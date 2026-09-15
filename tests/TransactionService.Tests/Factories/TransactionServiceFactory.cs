using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TransactionService.Data;
using TransactionService.Services;
using TransactionService.Tests.Fakes;

namespace TransactionService.Tests.Factories;

public class TransactionServiceFactory : WebApplicationFactory<Program>
{
    public FakeProviderService FakeProvider { get; } = new();
    private SqliteConnection? _connection;
    private IServiceScope? _scope;
    public bool DisableBackgroundService { get; set; } = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PaymentDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<PaymentDbContext>();

            
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IProviderService>();
            services.AddSingleton<IProviderService>(FakeProvider);
            
            if (DisableBackgroundService) services.RemoveAll<IHostedService>(); 
        });
    }
    
    public async Task InitializeAsync()
    {
        _scope = Services.CreateScope();
        var dbcontext = _scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await dbcontext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        _scope?.Dispose();
        _connection?.Dispose();
        await base.DisposeAsync();
    }
}