using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Surbibor.Api.Dtos;

namespace Surbibor.Api.Tests.Integration;

public class AuthFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VerifyEmail_ThenForgotAndResetPassword_ViaEmailedLinks()
    {
        const string email = "flow@example.com";
        var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "flow_user", "SuperSecret123"));
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(auth!.User.EmailVerified);

        // Unverified users can still use the app, but can't get a reset link yet.
        var earlyForgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.NoContent, earlyForgot.StatusCode);
        Assert.Single(_factory.Emails.Sent, m => m.To == email);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(_factory.Emails.LastTokenFor(email)));
        verifyResponse.EnsureSuccessStatusCode();
        Assert.True((await verifyResponse.Content.ReadFromJsonAsync<UserResponse>())!.EmailVerified);

        var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var me = await authedClient.GetFromJsonAsync<UserResponse>("/api/auth/me");
        Assert.True(me!.EmailVerified);

        var forgotResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.NoContent, forgotResponse.StatusCode);

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(_factory.Emails.LastTokenFor(email), "BrandNewPass1", "BrandNewPass1"));
        resetResponse.EnsureSuccessStatusCode();

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "SuperSecret123"));
        Assert.Equal(HttpStatusCode.BadRequest, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "BrandNewPass1"));
        newLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns204()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("ghost@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == "ghost@example.com");
    }

    [Fact]
    public async Task ResetPassword_WithBogusToken_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("not-a-real-token", "BrandNewPass1", "BrandNewPass1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/auth/resend-verification", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
