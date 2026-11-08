using backend.Accounting;
using backend.Person;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Entry> JournalEntries => Set<Entry>();
    public DbSet<AccountUsage> AccountUsages => Set<AccountUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Settings>(e =>
        {
            e.Property(o => o.Id).ValueGeneratedNever();
            e.Property(o => o.OpeningBalance).HasPrecision(18, 2);
            e.Property(o => o.Iban).HasMaxLength(21);
        });
        modelBuilder.Entity<AccountUsage>(e =>
        {
            e.HasKey(u => u.AccountNumber);
            e.Property(u => u.AccountNumber).ValueGeneratedNever();
            e.Property(u => u.Category).HasConversion<string>();
        });
        modelBuilder.Entity<Transaction>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Text).HasMaxLength(500);
            e.Property(x => x.BankReference).HasMaxLength(35);
            // Several manual Bewegungen have no BankReference; the database lets them coexist.
            e.HasIndex(x => x.BankReference).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.SetNull);
        });
        // The database does not enforce that a Bewegung has Buchungen or that they sum to its Betrag (ADR 0003).
        modelBuilder.Entity<Entry>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Text).HasMaxLength(500);
            e.HasOne(x => x.Transaction).WithMany(t => t.Entries).HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
