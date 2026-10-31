using LookAtMe.Core;

namespace LookAtMe.Tests;

public class RelativeTimeTests
{
    [Theory]
    [InlineData(0, "now")]
    [InlineData(4, "now")]
    [InlineData(45, "45s")]
    [InlineData(190, "3m")]
    [InlineData(7200, "2h")]
    public void Formats(int seconds, string expected)
    {
        Assert.Equal(expected, RelativeTime.Format(TimeSpan.FromSeconds(seconds)));
    }
}
