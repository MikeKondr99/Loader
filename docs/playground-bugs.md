# Playground Bugs

- [ ] `RowCount` не отображается в успешном результате. Сейчас `LoadedTable`/ответ playground не несет количество строк final table.
- [ ] Temp table без meta должна быть безопасной по nullability. Найдено на `DBNull` из Postgres/QVD: без анализа density ClickHouse writer может создать non-nullable колонку и упасть на insert.
- [ ] Повторные alias в `SELECT` дают ошибку, но сообщение плохо читается для пользователя script/playground.
- [ ] `Alt(0.0)` или `Alt(0)` может привести к `UnknownClrTypeException: CLR type 'System.Object' is unknown to Loader data type mapper` при чтении результата из ClickHouse.
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
