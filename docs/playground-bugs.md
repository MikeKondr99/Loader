# Playground Bugs

- [ ] `RowCount` не отображается в успешном результате. Сейчас `LoadedTable`/ответ playground не несет количество строк final table.
- [ ] Temp table без meta должна быть безопасной по nullability. Найдено на `DBNull` из Postgres/QVD: без анализа density ClickHouse writer может создать non-nullable колонку и упасть на insert.
- [ ] Файловые providers должны разъединять одинаковые имена колонок при нормализации схемы. Например source `Поле`, `Поле`, `Поле` должен стать `Поле`, `Поле (2)`, `Поле (3)`, чтобы не ломались name-to-ordinal, ADO schema и дальнейший query mapping. Repro: Jira CSV export `PIX Jira 2026-07-30T00_01_28+0300.csv` содержит дубли `Исправить в версиях` и `Метки4`, из-за чего CSV load падает до загрузки данных.
- [x] Query resolver должен валидировать агрегации и `GROUP BY` до выполнения SQL в ClickHouse. Сейчас ошибки вроде `SELECT city, SUM(amount)` без `GROUP BY city` и `GROUP BY SUM(amount)` доходят до CH runtime вместо доменной ошибки resolver-а.
- [ ] Повторные alias в `SELECT` дают ошибку, но сообщение плохо читается для пользователя script/playground.
- [ ] `Alt(0.0)` или `Alt(0)` может привести к `UnknownClrTypeException: CLR type 'System.Object' is unknown to Loader data type mapper` при чтении результата из ClickHouse.
- [x] Опция `header` должна быть строго boolean, но сейчас ошибка типа опции идет без `LangSpan`; нужно хранить span option/value и возвращать доменную ошибку с позицией.
- [x] Final table физически использует alias из `LOAD SELECT` как имена колонок. По договоренности внутри ClickHouse должны быть стабильные `column1`, `column2`, ...; alias должны жить в `LoadedTable.Fields`/metadata.

Пример воспроизведения `Alt`:

```sql
orders: LOAD
   [sepal.length].Num().Alt(0.0) as sepal_length,
   [sepal.width].Num() as sepal_width,
   [petal.length].Num() as petal_length,
   [petal.width].Num() as petal_width,
FROM [iris.csv] (csv, delimiter=',', header=true);
```
