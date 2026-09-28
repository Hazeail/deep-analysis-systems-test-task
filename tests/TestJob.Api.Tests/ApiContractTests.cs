// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Зафиксирован публичный маршрут OpenAPI-схемы для Swagger UI.
// 28-09-2026 — Сильченко Артем — Добавлены проверки единого контракта ошибок HTTP API.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TestJob.Api.Models;

namespace TestJob.Api.Tests;

/// <summary>
/// Проверяет HTTP-контракт для запросов, отклоняемых до вызова бизнес-сервиса.
/// </summary>
public sealed class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    /// <summary>
    /// Создаёт клиент тестового ASP.NET Core приложения.
    /// </summary>
    /// <param name="factory">Фабрика тестового веб-приложения.</param>
    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] =
                        "Host=localhost;Database=testjob;Username=testjob;Password=testjob"
                });
            });
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }).CreateClient();
    }

    /// <summary>
    /// Проверяет доступность OpenAPI-схемы по маршруту, используемому Swagger UI.
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ReturnsOpenApiDefinition()
    {
        using var response = await client.GetAsync("/api/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Проверяет единый ответ при отсутствии тела запроса.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_EmptyBody_ReturnsProcessingResponse()
    {
        using var content = new StringContent(string.Empty, null, "application/json");
        using var response = await client.PostAsync("/api/process", content);

        var result = await response.Content.ReadFromJsonAsync<ProcessingResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.IsError);
        Assert.Equal("INVALID_REQUEST", result.ErrorCode);
    }

    /// <summary>
    /// Проверяет единый ответ при синтаксически некорректном JSON.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_MalformedJson_ReturnsProcessingResponse()
    {
        using var content = new StringContent("{", null, "application/json");
        using var response = await client.PostAsync("/api/process", content);

        var result = await response.Content.ReadFromJsonAsync<ProcessingResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.IsError);
        Assert.Equal("INVALID_REQUEST", result.ErrorCode);
    }
}
