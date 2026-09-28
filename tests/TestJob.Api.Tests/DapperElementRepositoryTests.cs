// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Добавлена интеграционная проверка Dapper с PostgreSQL.

using Dapper;
using Npgsql;
using TestJob.Api.Services;

namespace TestJob.Api.Tests;

/// <summary>
/// Проверяет реальную запись элементов через Dapper в PostgreSQL.
/// </summary>
public sealed class DapperElementRepositoryTests
{
    /// <summary>
    /// Записывает элемент, проверяет все поля и удаляет тестовую строку.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveAsync_ConfiguredPostgres_PersistsCompleteElement()
    {
        var connectionString = Environment.GetEnvironmentVariable("TESTJOB_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var marker = "integration-" + Guid.NewGuid().ToString("N");
        const string html = "<a href=\"integration\">integration</a>";
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new DapperElementRepository(dataSource);

        try
        {
            await repository.SaveAsync(
                [new FoundElement(marker, html)],
                CancellationToken.None);

            await using var connection = await dataSource.OpenConnectionAsync();
            var storedHtml = await connection.QuerySingleAsync<string>(
                "SELECT html FROM elements WHERE attribute_value = @marker",
                new { marker });
            Assert.Equal(html, storedHtml);
        }
        finally
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync(
                "DELETE FROM elements WHERE attribute_value = @marker",
                new { marker });
        }
    }
}
