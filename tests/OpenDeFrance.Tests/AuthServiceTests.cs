using OpenDeFrance.Core.Services;
using Xunit;

namespace OpenDeFrance.Tests;

public class AuthServiceTests
{
    private readonly AuthService _authService = new();

    [Theory]
    [InlineData("admin", "admin123", "Admin")]
    [InlineData("ADMIN", "admin", "Admin")]
    [InlineData("Admin", "kyokushin", "Admin")]
    [InlineData("table", "table123", "Table")]
    [InlineData("Table", "tatami", "Table")]
    [InlineData("tv", "tv123", "Tv")]
    [InlineData("TV", "ecran", "Tv")]
    [InlineData("streamer", "streamer123", "Streamer")]
    [InlineData("STREAMER", "streamer", "Streamer")]
    [InlineData("Streamer", "stream", "Streamer")]
    [InlineData("streamer", "live", "Streamer")]
    [InlineData("collector", "collector123", "Collector")]
    [InlineData("COLLECTOR", "collector", "Collector")]
    [InlineData("Collector", "appel", "Collector")]
    [InlineData("collector", "chambre", "Collector")]
    [InlineData("pesee", "pesee123", "WeighIn")]
    [InlineData("PESEE", "pesee", "WeighIn")]
    [InlineData("weighin", "weighin", "WeighIn")]
    [InlineData("pesee", "poids", "WeighIn")]
    [InlineData("pesée", "balance", "WeighIn")]
    public void Authenticate_ValidCredentials_ReturnsExpectedRole(string username, string password, string expectedRole)
    {
        var session = _authService.Authenticate(username, password);

        Assert.NotNull(session);
        Assert.Equal(expectedRole, session.Role);
        Assert.Equal(username.ToLowerInvariant(), session.Username);
    }

    [Theory]
    [InlineData("admin", "wrongpassword")]
    [InlineData("table", "badpass")]
    [InlineData("tv", "1234")]
    [InlineData("streamer", "wrongpass")]
    [InlineData("collector", "badpass")]
    [InlineData("pesee", "wrongpassword")]
    [InlineData("unknown", "admin123")]
    [InlineData("", "")]
    [InlineData("   ", "admin123")]
    public void Authenticate_InvalidCredentials_ReturnsNull(string username, string password)
    {
        var session = _authService.Authenticate(username, password);

        Assert.Null(session);
    }
}
