using backend.Accounting;
using backend.Person;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OpeningBalance>(e =>
        {
            e.Property(o => o.Id).ValueGeneratedNever();
            e.Property(o => o.Amount).HasPrecision(18, 2);
        });
    }
}
