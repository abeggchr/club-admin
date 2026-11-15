using backend.Auth;

namespace backend.Tests.Api;

public class ReturnUrlTests
{
    [Theory]
    [InlineData("/persons?x=1", "/persons?x=1")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    public void Only_local_paths_are_kept(string? input, string expected) =>
        Assert.Equal(expected, AuthSetup.SafeReturnUrl(input));
}
