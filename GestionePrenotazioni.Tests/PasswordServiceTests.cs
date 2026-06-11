using GestionePrenotazioni.Web.Services;

namespace GestionePrenotazioni.Tests;

public sealed class PasswordServiceTests
{
    [Fact]
    public void HashCanBeVerifiedAndRejectsWrongPassword()
    {
        var service = new PasswordService();
        var hash = service.Hash("admin");

        Assert.True(service.Verify("admin", hash));
        Assert.False(service.Verify("wrong", hash));
    }
}
