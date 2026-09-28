// Журнал изменений:
// 28-09-2026 — Сильченко Артем — Унифицированы ответы для ошибок JSON и model binding.
// 28-09-2026 — Сильченко Артем — Добавлена XML-документация точки входа приложения.
// 28-09-2026 — Сильченко Артем — Настроены REST API, Swagger, FluentValidation и доступ к PostgreSQL.

using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using TestJob.Api.Models;
using TestJob.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidatorsFromAssemblyContaining<ProcessingRequestValidator>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
            ?? "The request body is invalid.";

        return new BadRequestObjectResult(
            ProcessingResponse.Failure("INVALID_REQUEST", message));
    };
});
builder.Services.AddScoped<IProcessingService, ProcessingService>();
builder.Services.AddScoped<IElementRepository, DapperElementRepository>();
builder.Services.AddSingleton(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
    return NpgsqlDataSource.Create(connectionString);
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options => options.RoutePrefix = "api/swagger");
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
