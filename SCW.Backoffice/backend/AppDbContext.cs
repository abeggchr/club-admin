using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        string[] firstNames = ["Anna", "Ben", "Clara", "David", "Eva", "Felix", "Greta", "Hans", "Iris", "Jonas"];
        string[] lastNames = ["Meier", "Müller", "Keller", "Huber", "Weber", "Schmid", "Frei", "Graf", "Brunner", "Baumann"];

        modelBuilder.Entity<Person>().HasData(
            Enumerable.Range(0, 10).Select(i => new Person
            {
                Id = i + 1,
                FirstName = firstNames[i],
                LastName = lastNames[i],
                Email = $"{firstNames[i].ToLower()}.{lastNames[i].ToLower()}@example.com"
            }));
    }
}
