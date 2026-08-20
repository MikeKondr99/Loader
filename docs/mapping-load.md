# Mapping LOAD notes

`ENGINE = Join` в ClickHouse подходит как физическая основа для будущего `Mapped LOAD`/`field.Map(...)`.

Проверенное поведение:

- `joinGetOrNull` работает как быстрый lookup и не требует менять `FROM` основного запроса.
- `SELECT`, `WHERE`, `GROUP BY`, `ORDER BY`, `LIMIT` по `ENGINE = Join` физически выполняются.
- `ALTER TABLE ... ADD COLUMN` не поддерживается для `ENGINE = Join`.
- `Join(ANY, LEFT, key)` схлопывает дубли ключей уже при хранении. Если загрузить два значения для одного key, обычный `SELECT *` покажет только одно.

Практический вывод:

- Строго запрещать чтение такой таблицы как обычной технически не обязательно: базовые query работают.
- Но нужно показывать warning, если пользователь делает `FROM mapped_table`, `Union(mapped_table, ...)` или `Join(mapped_table, ...)`.
- Warning должен объяснять, что таблица хранится как ClickHouse `ENGINE = Join`, дубли ключей схлопнуты, а часть table operations недоступна.
- Если когда-нибудь потребуется гарантированно сохранить все строки mapping source, нужно создавать два объекта: обычную table и отдельную join lookup table.
