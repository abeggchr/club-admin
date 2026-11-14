using backend.Person;

namespace backend.Tests.Person;

public class PayerRulesTests
{
    // (Id, PayerId) of all Personen.
    private static readonly PayerLink[] Links = [new(1, null), new(2, null), new(3, 2), new(4, null)];

    [Fact]
    public void No_Zahler_is_valid() => Assert.Null(PayerRules.ValidateSet(1, null, Links));

    [Fact]
    public void A_Person_can_get_a_Zahler_without_own_Zahler() => Assert.Null(PayerRules.ValidateSet(1, 4, Links));

    [Fact]
    public void A_Person_cannot_be_their_own_Zahler() => Assert.NotNull(PayerRules.ValidateSet(1, 1, Links));

    [Fact]
    public void A_Zahler_must_exist() => Assert.NotNull(PayerRules.ValidateSet(1, 99, Links));

    [Fact]
    public void A_Zahler_cannot_have_a_Zahler_themselves() => Assert.NotNull(PayerRules.ValidateSet(1, 3, Links));

    [Fact]
    public void A_Person_who_is_already_a_Zahler_cannot_get_a_Zahler() => Assert.NotNull(PayerRules.ValidateSet(2, 4, Links));

    [Fact]
    public void Keeping_the_current_Zahler_stays_valid() => Assert.Null(PayerRules.ValidateSet(3, 2, Links));

    [Fact]
    public void A_Person_who_is_someones_Zahler_cannot_be_deleted() => Assert.NotNull(PayerRules.ValidateDelete(2, Links));

    [Fact]
    public void A_Person_with_a_Zahler_but_nobody_paying_for_them_can_be_deleted() => Assert.Null(PayerRules.ValidateDelete(3, Links));
}
