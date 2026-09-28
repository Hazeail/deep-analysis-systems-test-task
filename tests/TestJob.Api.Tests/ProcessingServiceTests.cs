// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Проверки расширены до точных списков атрибутов, email и HTML.
// 28-09-2026 — Сильченко Артем — Добавлена XML-документация тестовых сценариев и вспомогательных типов.
// 28-09-2026 — Сильченко Артем — Добавлены проверки образцов, ошибок Base64 и обязательных полей.

using System.Text.Json;
using TestJob.Api.Models;
using TestJob.Api.Services;

namespace TestJob.Api.Tests;

/// <summary>
/// Проверяет бизнес-логику сервиса на эталонных и ошибочных входных данных.
/// </summary>
public sealed class ProcessingServiceTests
{
    /// <summary>
    /// Проверяет результаты обработки обоих эталонных payload-файлов.
    /// </summary>
    [Theory]
    [InlineData("json_payload_1.txt", "json_result_1.txt")]
    [InlineData("json_payload_2.txt", "json_result_2.txt")]
    public async Task ProcessAsync_ReturnsExpectedResult(string payloadFile, string resultFile)
    {
        var repository = new RecordingRepository();
        var service = new ProcessingService(repository);
        var request = await ReadJsonAsync<ProcessingRequest>(payloadFile);
        var expected = await ReadJsonAsync<ProcessingResponse>(resultFile);

        var result = await service.ProcessAsync(request, CancellationToken.None);

        Assert.Equal(expected.IsError, result.IsError);
        Assert.Equal(expected.ElementsCount, result.ElementsCount);
        Assert.Equal(expected.EmailsCount, result.EmailsCount);
        Assert.Equal(expected.Url, result.Url);
        Assert.Equal(expected.DecryptedPlainText, result.DecryptedPlainText);
        Assert.Equal(expected.ElementsAttributeList, result.ElementsAttributeList);
        Assert.Equal(expected.EmailsList, result.EmailsList);
        Assert.Equal(expected.ElementsCount, repository.Elements.Count);
        Assert.All(repository.Elements, element => Assert.False(string.IsNullOrWhiteSpace(element.Html)));
    }

    /// <summary>
    /// Проверяет стабильный код ошибки для некорректного Base64 URL.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_InvalidUrlBase64_ReturnsStableCode()
    {
        var service = new ProcessingService(new RecordingRepository());
        var request = (await ReadJsonAsync<ProcessingRequest>("json_payload_1.txt")).WithUrl("not-base64");

        var exception = await Assert.ThrowsAsync<ProcessingException>(
            () => service.ProcessAsync(request, CancellationToken.None));

        Assert.Equal("INVALID_URL_BASE64", exception.Code);
    }

    /// <summary>
    /// Проверяет код ошибки обязательного пустого селектора.
    /// </summary>
    [Fact]
    public async Task Validator_MissingSelector_ReturnsStableCode()
    {
        var request = (await ReadJsonAsync<ProcessingRequest>("json_payload_1.txt")).WithSelector(string.Empty);
        var result = await new ProcessingRequestValidator().ValidateAsync(request);
        Assert.Equal("SELECTOR_REQUIRED", result.Errors[0].ErrorCode);
    }

    /// <summary>
    /// Читает и десериализует входной объект из корня репозитория.
    /// </summary>
    private static async Task<T> ReadJsonAsync<T>(string file)
    {
        var root = FindRepositoryRoot();
        await using var stream = File.OpenRead(Path.Combine(root, file));
        return (await JsonSerializer.DeserializeAsync<T>(stream))!;
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
    /// Сохраняет переданные элементы в памяти для проверки побочного эффекта сервиса.
    /// </summary>
    private sealed class RecordingRepository : IElementRepository
    {
        public IReadOnlyCollection<FoundElement> Elements { get; private set; } = [];

        /// <summary>
        /// Запоминает элементы без обращения к внешней базе данных.
        /// </summary>
        public Task SaveAsync(IReadOnlyCollection<FoundElement> elements, CancellationToken cancellationToken)
        {
            Elements = elements;
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Создаёт изменённые копии входного объекта для негативных сценариев.
/// </summary>
file static class RequestChanges
{
    /// <summary>
    /// Возвращает копию запроса с новым Base64 URL.
    /// </summary>
    public static ProcessingRequest WithUrl(this ProcessingRequest source, string value) => new()
    {
        Selector = source.Selector,
        Attribute = source.Attribute,
        UrlBase64 = value,
        EncryptedTextBytesBase64 = source.EncryptedTextBytesBase64,
        KeyBytesBase64 = source.KeyBytesBase64,
        PageBase64 = source.PageBase64
    };

    /// <summary>
    /// Возвращает копию запроса с новым CSS-селектором.
    /// </summary>
    public static ProcessingRequest WithSelector(this ProcessingRequest source, string value) => new()
    {
        Selector = value,
        Attribute = source.Attribute,
        UrlBase64 = source.UrlBase64,
        EncryptedTextBytesBase64 = source.EncryptedTextBytesBase64,
        KeyBytesBase64 = source.KeyBytesBase64,
        PageBase64 = source.PageBase64
    };
}
