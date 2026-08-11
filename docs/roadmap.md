# Роадмап

## Провайдеры

- [x] Базы данных
  - [x] PostgreSQL
  - [x] ClickHouse
  - [x] Microsoft SQL Server
  - [x] Oracle
  - [x] Apache Hive
  - [x] ODBC
- [x] Файлы
  - [x] Excel
  - [x] CSV
  - [x] QVD
  - [x] XML
  - [x] JSON
- [ ] Источники файлов
  - [x] Файловая система
  - [ ] Remote HTTP[S]

## Скрипт

- [x] LOAD
- [x] Поддержка нескольких операций в одном скрипте
- [x] Базовые трансформации в LOAD
  - [x] WHERE
  - [x] ORDER BY
  - [x] GROUP BY и агрегации
  - [x] LIMIT/OFFSET
- [ ] LOAD: стабилизация MVP. Детальный чеклист: [load-stabilization.md](load-stabilization.md).
- [ ] LOAD: оптимизации final table
  - [ ] Type narrowing: integer min/max, decimal precision/scale.
  - [ ] Nullability: `Nullable(T)` только если schema/meta говорит, что null возможен или это неизвестно.
  - [ ] `LowCardinality(String)` по известной низкой cardinality.
  - [ ] Final tables на `MergeTree`; temp/staging оставить на `Log`.
- [x] LOAD FROM LOAD
- [ ] DROP TABLE
- [ ] LINK - `shadow table`
- [ ] FROM JOIN
- [ ] FROM UNION
- [ ] FROM INLINE
- [ ] FROM Numbers
- [ ] FROM CALENDAR
- [ ] FROM DATASET
- [ ] INDEX?

## Типы

- [ ] Соединить ReData.DataType и Loader.DataType
- [ ] Разделение на Decimal и Float вместо общего numeric

## Функции

- [ ] Time('12:00')
- [ ] Text(Time('12:00'))
- [x] Num(text, ',') или Dec/Float
- [x] Date(text, 'yyyy-mm-dd') Joda pattern
- [x] Text(date, 'yyyy-mm-dd') Joda pattern
- [x] Subfield(text, delimiter, index) без разворачивания
- [x] Json функции: JsonGet, JsonGet<Type>, JsonHas, JsonType, JsonLength
- [x] Fractile
- [ ] Сделать названия case-insensitive
- [ ] Переименовать функции, чтобы они были ближе к Qlik
- [ ] QOL функции
  - [x] ExcludeChars
  - [x] KeepChars
- [ ] Убрать редкие функции, поддержку которых не хотим обещать
