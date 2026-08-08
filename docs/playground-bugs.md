# Playground Bugs

- [ ] `RowCount` не отображается в успешном результате. Сейчас `LoadedTable`/ответ playground не несет количество строк final table.
- [ ] Если provider возвращает схему с `0` полей, Load не останавливается доменной ошибкой и доходит до `CREATE TABLE (...)` с пустым списком колонок. Нужно валидировать `FieldCount > 0`/schema columns до temp table write и выдавать понятную ошибку script/provider этапа.
- [ ] ClickHouse writer не должен генерировать `Decimal(0, 0)`, если provider/schema отдали decimal precision/scale как `0`. Repro: Postgres source SQL `SELECT city, COUNT(*) AS cnt, SUM(amount) AS total_amount FROM public.playground_orders GROUP BY city` падает на temp table write с `Wrong precision: it must be between 1 and 76, got 0`. Нужно игнорировать нулевые precision/scale или fallback на безопасный decimal default.
- [ ] Playground нужен `Copy` button для error details, чтобы быстро копировать полный текст ошибки/stack trace из UI.
- [ ] ClickHouse `Variant(...)` надо обработать отдельной политикой. Сейчас смешение физических numeric types вроде `Decimal64` + `Float64` может дать `Variant(Decimal64, Float64)` или `NO_COMMON_TYPE` на старых CH; `DbDataReader` видит `System.Object`, а `Normalize`/mapper не знают, как безопасно мапить такой столбец. Нужно решить: запрещать, приводить на query layer или поддерживать Variant как отдельный DataType/edge case.
- [x] Temp table без meta должна быть безопасной по nullability. Найдено на `DBNull` из Postgres/QVD: без анализа density ClickHouse writer может создать non-nullable колонку и упасть на insert.
- [ ] Файловые providers должны разъединять одинаковые имена колонок при нормализации схемы. Например source `Поле`, `Поле`, `Поле` должен стать `Поле`, `Поле (2)`, `Поле (3)`, чтобы не ломались name-to-ordinal, ADO schema и дальнейший query mapping. Repro: Jira CSV export `PIX Jira 2026-07-30T00_01_28+0300.csv` содержит дубли `Исправить в версиях` и `Метки4`, из-за чего CSV load падает до загрузки данных.
- [x] Query resolver должен валидировать агрегации и `GROUP BY` до выполнения SQL в ClickHouse. Сейчас ошибки вроде `SELECT city, SUM(amount)` без `GROUP BY city` и `GROUP BY SUM(amount)` доходят до CH runtime вместо доменной ошибки resolver-а.
- [x] Повторные alias в `SELECT` дают ошибку, но сообщение плохо читается для пользователя script/playground.
- [x] `Alt(0.0)` или `Alt(0)` может привести к `UnknownClrTypeException: CLR type 'System.Object' is unknown to Loader data type mapper` при чтении результата из ClickHouse.
- [x] Опция `header` должна быть строго boolean, но сейчас ошибка типа опции идет без `LangSpan`; нужно хранить span option/value и возвращать доменную ошибку с позицией.
- [x] Final table физически использует alias из `LOAD SELECT` как имена колонок. По договоренности внутри ClickHouse должны быть стабильные `column1`, `column2`, ...; alias должны жить в `LoadedTable.Fields`/metadata.

Пример воспроизведения `Alt`:

```sql
orders: LOAD
   [sepal.length].Num().Alt(0.0) as sepal_length,
   [sepal.width].Num() as sepal_width,
   [petal.length].Num() as petal_length,
   [petal.width].Num() as petal_width,
FROM Csv(path='iris.csv', delimiter=',', header=true);
```
