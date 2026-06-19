using Bearcat.Infrastructure.Security;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Security;

public class NoOpSecretProtectorTest
{
    [Test]
    public void Protect_ReturnsPlaintextUnchanged()
    {
        // Act / Assert
        NoOpSecretProtector.Instance.Protect("value").ShouldBe("value");
    }

    [Test]
    public void Unprotect_ReturnsValueUnchanged()
    {
        // Act / Assert
        NoOpSecretProtector.Instance.Unprotect("value").ShouldBe("value");
    }

    [Test]
    public void IsProtected_AlwaysReturnsFalse()
    {
        // Act / Assert
        NoOpSecretProtector.Instance.IsProtected("bearcat:enc:v1:anything").ShouldBeFalse();
    }
}
