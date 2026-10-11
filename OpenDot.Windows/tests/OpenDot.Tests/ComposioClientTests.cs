using System.Net;
using System.Text.Json;
using OpenDot.Integrations.Composio;

namespace OpenDot.Tests;

public class ComposioClientTests
{
    private class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task GetTriggerAppsAsync_WithoutApiKey_ReturnsStandardAppsUnconnected()
    {
        var client = new ComposioClient(apiKeyProvider: () => null);

        var apps = await client.GetTriggerAppsAsync();

        Assert.NotEmpty(apps);
        Assert.Equal(ComposioClient.StandardTriggerApps.Count, apps.Count);
        Assert.All(apps, a => Assert.False(a.Connected));
        Assert.Contains(apps, a => a.Slug == "gmail");
        Assert.Contains(apps, a => a.Slug == "slack");
        Assert.Contains(apps, a => a.Slug == "github");
    }

    [Fact]
    public async Task GetTriggerAppsAsync_WithApiKeyAndConnectedAccounts_MarksConnectedAndSortsFirst()
    {
        var jsonResponse = JsonSerializer.Serialize(new
        {
            items = new[]
            {
                new { toolkit = new { slug = "gmail" }, status = "ACTIVE" },
                new { toolkit = new { slug = "github" }, status = "ACTIVE" },
                new { toolkit = new { slug = "customapp" }, status = "ACTIVE" }
            }
        });

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.True(req.Headers.Contains("x-api-key"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler);
        var client = new ComposioClient(http, apiKeyProvider: () => "comp_test_key", userIdProvider: () => "test_user");

        var apps = await client.GetTriggerAppsAsync();

        Assert.NotEmpty(apps);
        var gmail = apps.First(a => a.Slug == "gmail");
        var github = apps.First(a => a.Slug == "github");
        var custom = apps.First(a => a.Slug == "customapp");
        var slack = apps.First(a => a.Slug == "slack");

        Assert.True(gmail.Connected);
        Assert.True(github.Connected);
        Assert.True(custom.Connected);
        Assert.False(slack.Connected);

        // Connected apps must appear before unconnected apps
        Assert.True(apps[0].Connected);
        Assert.True(apps[1].Connected);
        Assert.True(apps[2].Connected);
        Assert.False(apps.Last().Connected);
    }

    [Fact]
    public async Task GetTriggerAppsAsync_WhenComposioApiReturnsError_NeverReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Gone)
        {
            Content = new StringContent("{\"error\":\"Endpoint gone\"}", System.Text.Encoding.UTF8, "application/json")
        });

        var http = new HttpClient(handler);
        var client = new ComposioClient(http, apiKeyProvider: () => "comp_test_key");

        var apps = await client.GetTriggerAppsAsync();

        // Must fall back to standard apps, never be empty
        Assert.NotEmpty(apps);
        Assert.Equal(ComposioClient.StandardTriggerApps.Count, apps.Count);
        Assert.All(apps, a => Assert.False(a.Connected));
    }

    [Fact]
    public async Task GetTriggerAppsAsync_WhenHttpThrowsException_NeverReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler(_ => throw new HttpRequestException("Network failure"));
        var http = new HttpClient(handler);
        var client = new ComposioClient(http, apiKeyProvider: () => "comp_test_key");

        var apps = await client.GetTriggerAppsAsync();

        Assert.NotEmpty(apps);
        Assert.Equal(ComposioClient.StandardTriggerApps.Count, apps.Count);
    }

    [Fact]
    public async Task ConnectAppUrlAsync_WithApiKey_ReturnsFallbackPlatformUrlWhenLinkEndpointFails()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(handler);
        var client = new ComposioClient(http, apiKeyProvider: () => "comp_test_key");

        var url = await client.ConnectAppUrlAsync("gmail");

        Assert.NotNull(url);
        Assert.Equal("https://platform.composio.dev/apps/gmail", url);
    }

    [Fact]
    public async Task ConnectAppUrlAsync_WithoutApiKey_ReturnsNull()
    {
        var client = new ComposioClient(apiKeyProvider: () => null);

        var url = await client.ConnectAppUrlAsync("gmail");

        Assert.Null(url);
    }
}
