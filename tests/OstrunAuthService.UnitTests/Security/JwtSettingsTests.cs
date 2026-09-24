using System.ComponentModel.DataAnnotations;
using OstrunAuthService.Infrastructure.Security;

namespace OstrunAuthService.UnitTests.Security;

public class JwtSettingsTests
{
    [Theory]
    [InlineData(31, false)]
    [InlineData(32, true)]
    public void Secret_MustBeLongEnoughForHs256(int length, bool expectedValid)
    {
        var settings = new JwtSettings { Secret = new string('k', length), Issuer = "issuer", Audience = "audience" };

        var isValid = Validator.TryValidateObject(settings, new ValidationContext(settings), null, validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }
}
