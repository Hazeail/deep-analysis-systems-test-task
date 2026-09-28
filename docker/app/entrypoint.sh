#!/bin/sh
# Журнал изменений:
# 28-09-2026 — Сильченко Артем — Сборка ограничена deploy-проектом API без тестовых исходников.
# 28-09-2026 — Сильченко Артем — Добавлены восстановление, сборка и запуск API при старте контейнера.

set -eu
cd /src
dotnet restore src/TestJob.Api/TestJob.Api.csproj
dotnet build src/TestJob.Api/TestJob.Api.csproj --configuration Release --no-restore
exec dotnet run --project src/TestJob.Api/TestJob.Api.csproj --configuration Release --no-build --urls http://0.0.0.0:8090
