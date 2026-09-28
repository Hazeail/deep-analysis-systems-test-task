// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Устранено удаление байтов plaintext; regex перенесён в compile-time генерацию.
// 28-09-2026 — Сильченко Артем — Добавлена XML-документация сервиса и хранилища элементов.
// 28-09-2026 — Сильченко Артем — Реализованы декодирование, DOM-разбор, поиск email, AES и сохранение элементов.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Dapper;
using Npgsql;
using TestJob.Api.Models;

namespace TestJob.Api.Services;

/// <summary>
/// Определяет операцию комплексной обработки входного объекта.
/// </summary>
public interface IProcessingService
{
    /// <summary>
    /// Обрабатывает входные данные и формирует результат API.
    /// </summary>
    /// <param name="request">Проверенный входной объект.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат обработки страницы.</returns>
    Task<ProcessingResponse> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Определяет сохранение найденных HTML-элементов.
/// </summary>
public interface IElementRepository
{
    /// <summary>
    /// Сохраняет набор элементов как одну логическую операцию.
    /// </summary>
    /// <param name="elements">Элементы, обнаруженные по CSS-селектору.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача сохранения данных.</returns>
    Task SaveAsync(IReadOnlyCollection<FoundElement> elements, CancellationToken cancellationToken);
}

/// <summary>
/// Содержит значение выбранного атрибута и полный HTML найденного элемента.
/// </summary>
/// <param name="AttributeValue">Значение запрошенного атрибута.</param>
/// <param name="Html">Полный HTML-код элемента.</param>
public sealed record FoundElement(string AttributeValue, string Html);

/// <summary>
/// Представляет ожидаемую ошибку обработки со стабильным кодом API.
/// </summary>
/// <param name="code">Текстовый код ошибки.</param>
/// <param name="message">Безопасное сообщение для ответа.</param>
/// <param name="innerException">Исходное исключение, если оно доступно.</param>
public sealed class ProcessingException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

/// <summary>
/// Выполняет декодирование, DOM-анализ, поиск email, расшифрование и сохранение результата.
/// </summary>
/// <param name="elementRepository">Хранилище обнаруженных HTML-элементов.</param>
public sealed partial class ProcessingService(IElementRepository elementRepository) : IProcessingService
{
    [GeneratedRegex(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Последовательно обрабатывает все части входного объекта и сохраняет найденные элементы.
    /// </summary>
    /// <param name="request">Проверенный входной объект.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Заполненный результат обработки.</returns>
    public async Task<ProcessingResponse> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken)
    {
        var url = DecodeUtf8(request.UrlBase64!, "INVALID_URL_BASE64", "url_b64");
        var page = DecodeUtf8(request.PageBase64!, "INVALID_PAGE_BASE64", "page_b64");
        var key = DecodeBytes(request.KeyBytesBase64!, "INVALID_KEY_BASE64", "key_bytes_b64");
        var cipher = DecodeBytes(request.EncryptedTextBytesBase64!, "INVALID_ENCRYPTED_TEXT_BASE64", "encrypted_text_bytes_b64");

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(response => response.Content(page), cancellationToken);
        IHtmlCollection<IElement> selected;
        try
        {
            selected = document.QuerySelectorAll(request.Selector!);
        }
        catch (Exception exception)
        {
            throw new ProcessingException("INVALID_SELECTOR", "The CSS selector is invalid.", exception);
        }

        var elements = selected.Select(element => new FoundElement(
            element.GetAttribute(request.Attribute!) ?? string.Empty,
            element.OuterHtml)).ToArray();
        var emails = EmailRegex().Matches(page).Select(match => match.Value).ToArray();
        var decryptedText = Decrypt(cipher, key);

        await elementRepository.SaveAsync(elements, cancellationToken);
        return new ProcessingResponse
        {
            ElementsCount = elements.Length,
            EmailsCount = emails.Length,
            Url = url,
            DecryptedPlainText = decryptedText,
            ElementsAttributeList = elements.Select(element => element.AttributeValue).ToArray(),
            EmailsList = emails
        };
    }

    /// <summary>
    /// Декодирует Base64-представление строки UTF-8.
    /// </summary>
    private static string DecodeUtf8(string value, string code, string field) =>
        Encoding.UTF8.GetString(DecodeBytes(value, code, field));

    /// <summary>
    /// Декодирует Base64 и преобразует ошибку формата в ошибку контракта API.
    /// </summary>
    private static byte[] DecodeBytes(string value, string code, string field)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new ProcessingException(code, $"The {field} field is not valid Base64.", exception);
        }
    }

    /// <summary>
    /// Расшифровывает AES-256 ECB данные без удаления криптографического padding.
    /// </summary>
    private static string Decrypt(byte[] cipher, byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ProcessingException("INVALID_AES_KEY", "The AES-256 key must contain 32 bytes.");
        }

        if (cipher.Length == 0 || cipher.Length % 16 != 0)
        {
            throw new ProcessingException("INVALID_ENCRYPTED_TEXT", "The encrypted text length must be a non-zero multiple of 16 bytes.");
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException exception)
        {
            throw new ProcessingException("AES_DECRYPTION_FAILED", "The encrypted text cannot be decrypted.", exception);
        }
    }
}

/// <summary>
/// Сохраняет найденные элементы в PostgreSQL посредством Dapper.
/// </summary>
/// <param name="dataSource">Источник подключений PostgreSQL.</param>
public sealed class DapperElementRepository(NpgsqlDataSource dataSource) : IElementRepository
{
    private const string InsertSql = """
        INSERT INTO elements (attribute_value, html)
        SELECT attribute_value, html
        FROM unnest(@AttributeValues, @HtmlValues) AS input(attribute_value, html);
        """;

    /// <summary>
    /// Записывает набор элементов в одной транзакции PostgreSQL.
    /// </summary>
    /// <param name="elements">Элементы для сохранения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача записи и фиксации транзакции.</returns>
    public async Task SaveAsync(IReadOnlyCollection<FoundElement> elements, CancellationToken cancellationToken)
    {
        if (elements.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            AttributeValues = elements.Select(element => element.AttributeValue).ToArray(),
            HtmlValues = elements.Select(element => element.Html).ToArray()
        };
        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql, parameters, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }
}
