namespace backend.Person;

public class Person
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public MembershipType Membership { get; set; } = MembershipType.Unknown;
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly? JoinedOn { get; set; }
    public DateOnly? LeftOn { get; set; }
    public string? Email { get; set; }
    public bool IsMainPayer { get; set; }
    /// <summary>Receives the club news by snail mail even though the Person is not active.</summary>
    public bool ReceivesClubNews { get; set; }
    public string? MobilePhone { get; set; }
    public string? LandlinePhone { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
    public int? PayerId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string FullName => $"{FirstName} {LastName}";
}
