# Union provider

`Union` объединяет уже загруженные script-таблицы:

```text
result:
LOAD *
FROM Union(table1, table2, table3);
```

Provider принимает только имена таблиц без кавычек. Строковые значения намеренно не считаются ссылками на таблицы:

```text
Union(table1, table2)      // ok
Union([table 1], table2)   // ok
Union('table1', 'table2')  // error
```

## Почему не merge()

ClickHouse `merge()` читает физические таблицы по database/regexp и не решает задачу Loader:

- final tables в DWH хранят физические поля `column1`, `column2`, ...
- пользовательские alias-ы есть только в `LoadedTable.Fields`;
- `column3` в разных таблицах может означать разные логические поля;
- отсутствующие логические поля нужно заполнять `NULL`;
- порядок колонок в `UNION ALL` должен быть одинаковым во всех ветках.

Поэтому `Union` строит ручной `UNION ALL`.

## Алгоритм

1. Resolver читает positional options и требует минимум две таблицы.
2. Каждая option должна быть `NameLiteral`, например `orders` или `[orders 2026]`.
3. Таблицы ищутся в `ScriptContext.LoadedTables` по `LoadedTable.Alias`.
4. Строится общий список логических полей в порядке первого появления.
5. Если поле есть не во всех таблицах, итоговое поле считается nullable.
6. Для каждой таблицы строится `SELECT` в одном и том же порядке общего списка.
7. Если поле есть в таблице, выбирается соответствующий физический `columnN`.
8. Если поля нет, подставляется `CAST(NULL AS Nullable(T))`.
9. Все выражения получают внутренние alias-ы `union_column1`, `union_column2`, ...
10. Reader результата переименовывается обратно в логические имена полей.
11. Дальше обычный `LoadStatementExecutor` загружает union reader во временную таблицу и применяет `LOAD WHERE/GROUP/ORDER/LIMIT` как для любого другого source.

## Пример SQL

Вход:

```text
orders_a fields:
id     -> column1
city   -> column2
amount -> column3

orders_b fields:
id    -> column1
city  -> column2
total -> column3
```

Общий список:

```text
id, city, amount, total
```

SQL:

```sql
SELECT
    CAST(`column1` AS Nullable(Int64)) AS `union_column1`,
    CAST(`column2` AS Nullable(String)) AS `union_column2`,
    CAST(`column3` AS Nullable(Decimal(38, 10))) AS `union_column3`,
    CAST(NULL AS Nullable(Decimal(38, 10))) AS `union_column4`
FROM `orders_a_final`
UNION ALL
SELECT
    CAST(`column1` AS Nullable(Int64)) AS `union_column1`,
    CAST(`column2` AS Nullable(String)) AS `union_column2`,
    CAST(NULL AS Nullable(Decimal(38, 10))) AS `union_column3`,
    CAST(`column3` AS Nullable(Decimal(38, 10))) AS `union_column4`
FROM `orders_b_final`
```

После чтения reader видит логические имена:

```text
id, city, amount, total
```

## Важное ограничение

На текущем этапе `Union` всегда читает physical final table name из `LoadedTable.Name`.
Оптимизация временных/inline таблиц сюда не входит.

Позже вместо `FROM final_table` можно заменить источник таблицы на SQL-фрагмент:

```sql
FROM (SELECT ... FROM temp_or_source)
```

Алгоритм `Union` от этого не должен измениться: ему нужен только SQL-фрагмент чтения таблицы и mapping логических полей на физические выражения.
