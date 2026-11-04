using backend.Accounting;
using backend.Person;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<AccountUsage> AccountUsages => Set<AccountUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OpeningBalance>(e =>
        {
            e.Property(o => o.Id).ValueGeneratedNever();
            e.Property(o => o.Amount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<AccountUsage>(e =>
        {
            e.HasKey(u => u.AccountNumber);
            e.Property(u => u.AccountNumber).ValueGeneratedNever();
            e.Property(u => u.Category).HasConversion<string>();
        });
        modelBuilder.Entity<Entry>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Text).HasMaxLength(500);
            e.HasIndex(x => x.Date);
        });
    }
}
