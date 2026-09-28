// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Удалено скрытое изменение файлов из тестового запуска.
// 28-09-2026 — Сильченко Артем — Добавлена XML-документация проверки result-файлов.
// 28-09-2026 — Сильченко Артем — Добавлена воспроизводимая проверка эталонных JSON-результатов.

using System.Text.Json;
using TestJob.Api.Models;
using TestJob.Api.Services;

namespace TestJob.Api.Tests;

/// <summary>
/// Проверяет соответствие обязательных result-файлов фактическому ответу сервиса.
/// </summary>
public sealed class ResultFilesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Сравнивает сохранённый JSON с результатом повторной обработки payload-файла.
    /// </summary>
    [Theory]
    [InlineData("json_payload_1.txt", "json_result_1.txt")]
    [InlineData("json_payload_2.txt", "json_result_2.txt")]
    public async Task ResultFile_MatchesServiceOutput(string payloadFile, string resultFile)
    {
        var root = FindRepositoryRoot();
        var request = JsonSerializer.Deserialize<ProcessingRequest>(
            await File.ReadAllTextAsync(Path.Combine(root, payloadFile)))!;
        var response = await new ProcessingService(new NoOpRepository())
            .ProcessAsync(request, CancellationToken.None);
        var actual = JsonSerializer.Serialize(response, JsonOptions) + Environment.NewLine;
        var resultPath = Path.Combine(root, resultFile);

        Assert.Equal(actual, await File.ReadAllTextAsync(resultPath));
    }

    /// <summary>
    /// Находит корень репозитория по файлу README.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    /// <summary>
    /// Исключает запись в БД при формировании эталонного ответа.
    /// </summary>
    private sealed class NoOpRepository : IElementRepository
    {
        /// <summary>
        /// Завершает операцию без сохранения элементов.
        /// </summary>
        public Task SaveAsync(IReadOnlyCollection<FoundElement> elements, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
