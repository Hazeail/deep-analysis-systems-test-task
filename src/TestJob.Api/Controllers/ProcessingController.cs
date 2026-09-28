// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Блоки обработки результата и исключений развёрнуты для читаемости.
// 28-09-2026 — Сильченко Артем — Добавлена XML-документация контроллера и его метода.
// 28-09-2026 — Сильченко Артем — Добавлен POST-контроллер с единым контрактом ответов.

using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TestJob.Api.Models;
using TestJob.Api.Services;

namespace TestJob.Api.Controllers;

/// <summary>
/// Принимает запрос на обработку страницы и возвращает унифицированный результат выполнения.
/// </summary>
[ApiController]
[Route("api/process")]
public sealed class ProcessingController(
    IProcessingService service,
    IValidator<ProcessingRequest> validator,
    ILogger<ProcessingController> logger) : ControllerBase
{
    /// <summary>
    /// Проверяет входной объект и передаёт его сервису обработки.
    /// </summary>
    /// <param name="request">Входные параметры обработки страницы.</param>
    /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
    /// <returns>Результат обработки либо описание ошибки в установленном контракте.</returns>
    [HttpPost]
    public async Task<ActionResult<ProcessingResponse>> ProcessAsync(
        [FromBody] ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            return BadRequest(ProcessingResponse.Failure(error.ErrorCode, error.ErrorMessage));
        }

        try
        {
            var response = await service.ProcessAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ProcessingException exception)
        {
            return BadRequest(ProcessingResponse.Failure(exception.Code, exception.Message));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Processing request failed.");
            return StatusCode(500, ProcessingResponse.Failure("INTERNAL_ERROR", exception.Message));
        }
    }
}
