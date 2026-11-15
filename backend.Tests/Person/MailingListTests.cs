using backend.Accounting;
using backend.Person;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Person;

/// <summary>The Mailing List data path behind GET /api/person/mailing-list: recipients, grouping and CSV.</summary>
public class MailingListTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _fixture;

    public MailingListTests(LocalPostgresFixture fixture)
    {
        _fixture = fixture;
        if (!_fixture.Available)
            throw new SkipException(
                "No local PostgreSQL available (README: localhost:5432, postgres/secret)."
            );
    }

    private static readonly DateOnly StartDate = new(2026, 1, 1);

    private static AppDbContext Connect(LocalPostgresFixture fixture) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(
                    fixture.ConnectionString,
                    b => b.MigrationsAssembly(typeof(AppDbContext).Assembly)
                )
                .Options
        );

    private static async Task<AppDbContext> DbAsync(LocalPostgresFixture fixture)
    {
        var db = Connect(fixture);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    private static backend.Person.Person P(
        int id,
        string first,
        MembershipType type = MembershipType.Single,
        string? joined = "2020-01-01",
        string? left = null,
        bool newsletter = false,
        string? street = "Sportweg",
        string? number = "12",
        string? zip = "8000",
        string? city = "Zürich",
        string? country = "CH"
    ) =>
        new()
        {
            Id = id,
            FirstName = first,
            LastName = "Muster",
            Membership = type,
            JoinedOn = joined is null ? null : DateOnly.Parse(joined),
            LeftOn = left is null ? null : DateOnly.Parse(left),
            ReceivesClubNews = newsletter,
            Street = street,
            Number = number,
            PostalCode = zip,
            City = city,
            Country = country,
        };

    // The query the mailing-list endpoint runs: every active Person plus every flagged Person,
    // grouped and rendered to the Mailing List CSV.
    private static async Task<string> MailingListCsvAsync(LocalPostgresFixture fixture)
    {
        await using var db = Connect(fixture);
        var settings = await db.Settings.FindAsync(backend.Settings.Settings.SingletonId);
        var persons = await db.Persons.AsNoTracking().ToListAsync();
        return MailingListCsv.Build(
            MailingListRules.BuildRows(
                ClubNewsRecipientRules.Recipients(persons, settings!.StartDate)
            )
        );
    }

    private static void Seed(AppDbContext db, backend.Person.Person[] persons)
    {
        db.Settings.Add(
            new backend.Settings.Settings
            {
                OpeningBalance = 0,
                StartDate = StartDate,
                Iban = "CH9300000000000000000",
                Name = "SCW",
                Address = "Sportweg 1",
                PostalCode = "8000",
                City = "Zürich",
            }
        );
        db.Persons.AddRange(persons);
    }

    [Fact]
    public async Task The_versandliste_lists_only_the_active_person_and_the_flagged_ones()
    {
        await using (var db = await DbAsync(_fixture))
        {
            Seed(
                db,
                [
                    P(1, "Anna", number: "1"),
                    P(2, "Ben", left: "2025-12-31", number: "2"),
                    P(3, "Clara", left: "2025-12-31", newsletter: true, number: "3"),
                    P(4, "Dante", joined: null, number: "4"),
                    P(5, "Elena", joined: null, newsletter: true, number: "5"),
                    P(6, "Felix", MembershipType.Unknown, number: "6"),
                ]
            );
            await db.SaveChangesAsync();
        }

        var csv = await MailingListCsvAsync(_fixture);
        Assert.Equal(
            "Empfänger;Adresse;Ort;Land\r\n"
                + "\"Anna Muster\";Sportweg 1;8000 Zürich;CH\r\n"
                + "\"Clara Muster\";Sportweg 3;8000 Zürich;CH\r\n"
                + "\"Elena Muster\";Sportweg 5;8000 Zürich;CH\r\n"
                + "\"Felix Muster\";Sportweg 6;8000 Zürich;CH",
            csv
        );
    }

    [Fact]
    public async Task The_recipients_at_the_same_address_share_one_row_with_one_name_per_line()
    {
        await using (var db = await DbAsync(_fixture))
        {
            Seed(
                db,
                [
                    P(1, "Anna"),
                    P(2, "Ben", street: "sportweg", number: "12"),
                    P(3, "Clara", city: "Bern"),
                ]
            );
            await db.SaveChangesAsync();
        }

        var csv = await MailingListCsvAsync(_fixture);
        Assert.Equal(
            "Empfänger;Adresse;Ort;Land\r\n"
                + "\"Anna Muster\nBen Muster\";Sportweg 12;8000 Zürich;CH\r\n"
                + "\"Clara Muster\";Sportweg 12;8000 Bern;CH",
            csv
        );
    }
}
