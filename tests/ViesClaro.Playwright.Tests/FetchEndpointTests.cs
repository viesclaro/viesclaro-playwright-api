using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ViesClaro.Playwright.BrowserPool;
using ViesClaro.Playwright.Fetch;
using Xunit;

namespace ViesClaro.Playwright.Tests;

/// <summary>
/// Cobre input validation do <see cref="FetchEndpoint.HandleAsync"/> — os 3
/// caminhos que retornam 400 sem chamar o pool. Casos que envolvem o
/// Playwright real (200/408/502) são cobertos por testes de integração
/// quando o Chromium estiver disponível.
/// </summary>
public class FetchEndpointTests
{
    [Fact]
    public async Task Returns_400_When_Url_Is_Missing()
    {
        var pool = Substitute.For<IBrowserPool>();
        var request = new FetchRequest { Url = "" };

        var result = await FetchEndpoint.HandleAsync(request, pool, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Title.Should().Be("Invalid request");

        await pool.DidNotReceive().FetchAsync(Arg.Any<FetchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_400_When_Url_Is_Not_Absolute()
    {
        var pool = Substitute.For<IBrowserPool>();
        var request = new FetchRequest { Url = "/relative/path" };

        var result = await FetchEndpoint.HandleAsync(request, pool, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Title.Should().Be("Invalid URL");

        await pool.DidNotReceive().FetchAsync(Arg.Any<FetchRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ftp://example.com/file.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public async Task Returns_400_When_Scheme_Is_Not_Http_Or_Https(string url)
    {
        var pool = Substitute.For<IBrowserPool>();
        var request = new FetchRequest { Url = url };

        var result = await FetchEndpoint.HandleAsync(request, pool, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Title.Should().Be("Invalid URL scheme");

        await pool.DidNotReceive().FetchAsync(Arg.Any<FetchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_408_When_Pool_Throws_TimeoutException()
    {
        var pool = Substitute.For<IBrowserPool>();
        pool.FetchAsync(Arg.Any<FetchRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Browser pool saturado"));

        var request = new FetchRequest { Url = "https://example.com/article-1" };

        var result = await FetchEndpoint.HandleAsync(request, pool, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(408);
        problem.ProblemDetails.Title.Should().Be("Fetch timeout");
    }

    [Fact]
    public async Task Returns_200_With_FetchResult_For_Valid_Url()
    {
        var expected = new FetchResult
        {
            Url = "https://example.com/article-1",
            FinalUrl = "https://example.com/article-1",
            StatusCode = 200,
            Html = "<!DOCTYPE html><html><body>ok</body></html>",
            DurationMs = 1234,
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var pool = Substitute.For<IBrowserPool>();
        pool.FetchAsync(Arg.Any<FetchRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new FetchRequest { Url = "https://example.com/article-1" };

        var result = await FetchEndpoint.HandleAsync(request, pool, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<FetchResult>>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }
}
