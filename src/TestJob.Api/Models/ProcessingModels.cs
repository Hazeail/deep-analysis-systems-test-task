// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Контракты развёрнуты для читаемости и сопровождения.
// 28-09-2026 — Сильченко Артем — Добавлена XML-документация моделей и валидатора.
// 28-09-2026 — Сильченко Артем — Добавлены контракты API и правила валидации входного объекта.

using System.Text.Json.Serialization;
using FluentValidation;

namespace TestJob.Api.Models;

/// <summary>
/// Содержит параметры поиска, исходную страницу и данные для расшифрования.
/// </summary>
public sealed class ProcessingRequest
{
    [JsonPropertyName("selector")]
    public string? Selector { get; init; }

    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("url_b64")]
    public string? UrlBase64 { get; init; }

    [JsonPropertyName("encrypted_text_bytes_b64")]
    public string? EncryptedTextBytesBase64 { get; init; }

    [JsonPropertyName("key_bytes_b64")]
    public string? KeyBytesBase64 { get; init; }

    [JsonPropertyName("page_b64")]
    public string? PageBase64 { get; init; }
}

/// <summary>
/// Представляет единый JSON-ответ для успешной обработки и ошибок.
/// </summary>
public sealed class ProcessingResponse
{
    [JsonPropertyName("is_error")]
    public int IsError { get; init; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; init; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; init; } = string.Empty;

    [JsonPropertyName("elements_count")]
    public int ElementsCount { get; init; }

    [JsonPropertyName("emails_count")]
    public int EmailsCount { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("decrypted_plain_text")]
    public string DecryptedPlainText { get; init; } = string.Empty;

    [JsonPropertyName("elements_attr_list")]
    public IReadOnlyList<string> ElementsAttributeList { get; init; } = [];

    [JsonPropertyName("emails_list")]
    public IReadOnlyList<string> EmailsList { get; init; } = [];

    /// <summary>
    /// Создаёт ответ с признаком ошибки и стабильным текстовым кодом.
    /// </summary>
    /// <param name="code">Текстовый код ошибки.</param>
    /// <param name="message">Сообщение об ошибке.</param>
    /// <returns>Заполненный ошибочный ответ.</returns>
    public static ProcessingResponse Failure(string code, string message) =>
        new() { IsError = 1, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// Проверяет наличие всех обязательных параметров входного объекта.
/// </summary>
public sealed class ProcessingRequestValidator : AbstractValidator<ProcessingRequest>
{
    /// <summary>
    /// Инициализирует правила проверки обязательных полей.
    /// </summary>
    public ProcessingRequestValidator()
    {
        Required(value => value.Selector, "SELECTOR_REQUIRED", "selector");
        Required(value => value.Attribute, "ATTRIBUTE_REQUIRED", "attribute");
        Required(value => value.UrlBase64, "URL_REQUIRED", "url_b64");
        Required(value => value.EncryptedTextBytesBase64, "ENCRYPTED_TEXT_REQUIRED", "encrypted_text_bytes_b64");
        Required(value => value.KeyBytesBase64, "KEY_REQUIRED", "key_bytes_b64");
        Required(value => value.PageBase64, "PAGE_REQUIRED", "page_b64");
    }

    /// <summary>
    /// Добавляет правило обязательности со стабильным кодом ошибки.
    /// </summary>
    /// <param name="expression">Выражение выбора проверяемого поля.</param>
    /// <param name="code">Код ошибки валидации.</param>
    /// <param name="field">JSON-имя поля для сообщения.</param>
    private void Required(
        System.Linq.Expressions.Expression<Func<ProcessingRequest, string?>> expression,
        string code,
        string field) =>
        RuleFor(expression).NotEmpty().WithErrorCode(code).WithMessage($"The {field} field is required.");
}
