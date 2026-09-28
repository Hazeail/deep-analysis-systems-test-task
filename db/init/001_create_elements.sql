-- Журнал изменений:
-- 28-09-2026 — Сильченко Артем — Добавлена воспроизводимая схема хранения найденных элементов.

CREATE TABLE IF NOT EXISTS elements
(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    attribute_value text NOT NULL,
    html text NOT NULL
);
