using backend.Invoicing;
using backend.Person;

namespace backend.Tests.Invoicing;

public class BulkInvoiceRunnerTests
{
    private static backend.Person.Person P(int id, string first, MembershipType type, string street) =>
        new()
        {
            Id = id,
            FirstName = first,
            LastName = "Muster",
            Membership = type,
            JoinedOn = new DateOnly(2020, 1, 1),
            Street = street,
            Number = "1",
            PostalCode = "8000",
            City = "Zürich",
            Country = "CH",
        };

    [Fact]
    public async Task A_failing_Rechnung_is_reported_and_does_not_stop_the_run()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P(1, "Anna", MembershipType.Single, "A"),
                P(2, "Ben", MembershipType.Single, "B"),
                P(3, "Cleo", MembershipType.Single, "C"),
                P(4, "Dora", MembershipType.Unknown, "D"),
            ],
            new DateOnly(2026, 1, 1)
        );
        var attempted = new List<string>();

        var summary = await BulkInvoiceRunner.RunAsync(
            plan,
            (invoice, ct) =>
            {
                attempted.Add(invoice.Recipient.FirstName);
                return Task.FromResult(
                    invoice.Recipient.FirstName == "Ben"
                        ? new InvoiceCreationResult(null, "Die Rechnung konnte nicht erstellt werden.")
                        : new InvoiceCreationResult(new Invoice { Id = 1 }, null)
                );
            }
        );

        Assert.Equal(["Anna", "Ben", "Cleo"], attempted);
        Assert.Equal(2, summary.Created);
        var failure = Assert.Single(summary.Failed);
        Assert.Contains("Ben Muster", failure);
        Assert.Single(summary.Warnings);
    }
}
