using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ViesClaro.Playwright.Common;
using Xunit;

namespace ViesClaro.Playwright.Tests;

public class ApiKeyEndpointFilterTests
{
    private const string ValidKey = "test-secret-12345";

    private static ApiKeyEndpointFilter CreateFilter(string key = ValidKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ApiKeyEndpointFilter.EnvVarName] = key,
            })
            .Build();
        return new ApiKeyEndpointFilter(config, NullLogger<ApiKeyEndpointFilter>.Instance);
    }

    [Fact]
    public void Constructor_Throws_When_Env_Var_Is_Missing()
    {
        var config = new ConfigurationBuilder().Build();
        var act = () => new ApiKeyEndpointFilter(config, NullLogger<ApiKeyEndpointFilter>.Instance);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{ApiKeyEndpointFilter.EnvVarName}*");
    }

    [Fact]
    public async Task Rejects_When_Header_Is_Missing()
    {
        var filter = CreateFilter();
        var ctx = MakeInvocationContext(headerValue: null);
        var nextCalled = false;

        var result = await filter.InvokeAsync(ctx, _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        result.Should().NotBeNull();
        result.Should().Be(Results.Unauthorized());
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_When_Header_Has_Wrong_Key()
    {
        var filter = CreateFilter();
        var ctx = MakeInvocationContext(headerValue: "wrong-key");
        var nextCalled = false;

        var result = await filter.InvokeAsync(ctx, _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        result.Should().Be(Results.Unauthorized());
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Allows_When_Header_Matches()
    {
        var filter = CreateFilter();
        var ctx = MakeInvocationContext(headerValue: ValidKey);
        var nextCalled = false;

        var result = await filter.InvokeAsync(ctx, _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        nextCalled.Should().BeTrue();
        result.Should().Be(Results.Ok());
    }

    private static DefaultEndpointFilterInvocationContext MakeInvocationContext(string? headerValue)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/fetch";
        if (headerValue is not null)
        {
            http.Request.Headers[ApiKeyEndpointFilter.HeaderName] = headerValue;
        }
        return new DefaultEndpointFilterInvocationContext(http);
    }
}
