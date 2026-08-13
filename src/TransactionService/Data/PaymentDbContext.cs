using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionService.Models;

namespace TransactionService.Data;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Operation> Operations { get; set; }
    public DbSet<Event> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OperationsConfiguration());
        modelBuilder.ApplyConfiguration(new EventsConfiguration());
    }
}

class OperationsConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.HasKey(o => o.OperationId);
        
        builder.HasMany(o => o.Events)
            .WithOne(e => e.Operation)
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(o => o.Amount)
            .HasMaxLength(100);
        builder.Property(o => o.Currency)
            .HasMaxLength(50);
        builder.Property(o => o.Description)
            .HasMaxLength(500);
    }
}

class EventsConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.Message)
            .HasMaxLength(500);
    }
}