#!/bin/sh
# Журнал изменений:
# 28-09-2026 — Сильченко Артем — Изолированы контейнерные артефакты сборки от Windows checkout.
# 28-09-2026 — Сильченко Артем — Сборка ограничена deploy-проектом API без тестовых исходников.
# 28-09-2026 — Сильченко Артем — Добавлены восстановление, сборка и запуск API при старте контейнера.

set -eu
cd /src
dotnet restore src/TestJob.Api/TestJob.Api.csproj \
    --packages /root/.nuget/packages \
    --artifacts-path /tmp/testjob-artifacts
dotnet publish src/TestJob.Api/TestJob.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /tmp/testjob-publish \
    --artifacts-path /tmp/testjob-artifacts
exec dotnet /tmp/testjob-publish/TestJob.Api.dll --urls http://0.0.0.0:8090
