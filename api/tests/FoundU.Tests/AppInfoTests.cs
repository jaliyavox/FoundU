using FoundU.Application.Common;
using Xunit;

namespace FoundU.Tests;

public class AppInfoTests
{
    [Fact]
    public void Describe_ReturnsProjectName()
    {
        Assert.Equal("FoundU API", AppInfo.Describe());
    }
}
