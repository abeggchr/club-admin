using backend.Person;

namespace backend.Tests.Person;

public class PayerRulesTests
{
    // (Id, PayerId, IsMainPayer) of all Persons: 2 pays for 3, 5 is Hauptzahler.
    private static readonly PayerLink[] Links =
    [
        new(1, null, false),
        new(2, null, false),
        new(3, 2, false),
        new(4, null, false),
        new(5, null, true),
    ];

    [Fact]
    public void No_Zahler_is_valid() => Assert.Null(PayerRules.ValidateSet(1, null, false, Links));

    [Fact]
    public void A_Person_can_get_a_Zahler_without_own_Zahler() => Assert.Null(PayerRules.ValidateSet(1, 4, false, Links));

    [Fact]
    public void A_Person_cannot_be_their_own_Zahler() => Assert.NotNull(PayerRules.ValidateSet(1, 1, false, Links));

    [Fact]
    public void A_Zahler_must_exist() => Assert.NotNull(PayerRules.ValidateSet(1, 99, false, Links));

    [Fact]
    public void A_Zahler_cannot_have_a_Zahler_themselves() => Assert.NotNull(PayerRules.ValidateSet(1, 3, false, Links));

    [Fact]
    public void A_Person_who_is_already_a_Zahler_cannot_get_a_Zahler() => Assert.NotNull(PayerRules.ValidateSet(2, 4, false, Links));

    [Fact]
    public void Keeping_the_current_Zahler_stays_valid() => Assert.Null(PayerRules.ValidateSet(3, 2, false, Links));

    [Fact]
    public void A_Hauptzahler_can_be_chosen_as_Zahler() => Assert.Null(PayerRules.ValidateSet(1, 5, false, Links));

    [Fact]
    public void A_Person_without_Zahler_can_become_Hauptzahler() => Assert.Null(PayerRules.ValidateSet(1, null, true, Links));

    [Fact]
    public void A_Person_cannot_have_a_Zahler_and_be_Hauptzahler() =>
        Assert.Equal("Eine Person mit Zahler kann nicht Hauptzahler sein.", PayerRules.ValidateSet(1, 4, true, Links));

    [Fact]
    public void A_Person_who_is_someones_Zahler_cannot_become_Hauptzahler() =>
        Assert.Equal(
            "Diese Person ist selbst Zahler und kann nicht Hauptzahler werden.",
            PayerRules.ValidateSet(2, null, true, Links)
        );

    // The Payer picker is locked for a Zahler, so a flag they already carry must not block saving them.
    [Fact]
    public void A_Zahler_who_already_is_Hauptzahler_keeps_the_flag()
    {
        PayerLink[] links = [new(1, null, true), new(2, 1, false)];
        Assert.Null(PayerRules.ValidateSet(1, null, true, links));
    }

    [Fact]
    public void A_new_Person_is_checked_like_one_who_was_not_Hauptzahler() =>
        Assert.Null(PayerRules.ValidateSet(0, null, true, Links));

    [Fact]
    public void A_Person_who_is_someones_Zahler_cannot_be_deleted() => Assert.NotNull(PayerRules.ValidateDelete(2, Links));

    [Fact]
    public void A_Person_with_a_Zahler_but_nobody_paying_for_them_can_be_deleted() => Assert.Null(PayerRules.ValidateDelete(3, Links));
}
