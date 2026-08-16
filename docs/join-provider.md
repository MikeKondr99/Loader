# Join provider

`Join` соединяет две уже загруженные script-таблицы по равенству ключевых полей.

```text
result:
LOAD *
FROM Join(orders, customer_id, customers, id);
```

Поддерживаемые provider names:

```text
Join(table1, field1, table2, field2)      // INNER JOIN
LeftJoin(table1, field1, table2, field2)  // LEFT JOIN
RightJoin(table1, field1, table2, field2) // RIGHT JOIN
FullJoin(table1, field1, table2, field2)  // FULL OUTER JOIN
```

Аргументы должны быть именами без кавычек. Строки не считаются ссылками:

```text
Join(orders, id, customers, id)       // ok
Join([orders 2026], id, clients, id)  // ok
Join('orders', 'id', clients, id)     // error
```

## Алгоритм

1. Resolver требует ровно 4 positional аргумента.
2. Все аргументы должны быть `NameLiteral`.
3. Таблицы ищутся в `ScriptContext.LoadedTables` по `LoadedTable.Alias`.
4. Ключевые поля ищутся по логическим именам, case-sensitive.
5. Типы ключей должны совпадать по `DataType`.
6. В результат попадают все поля левой таблицы и все поля правой таблицы.
7. Если имя поля встречается в обеих таблицах, оба поля переименовываются в `tableAlias.fieldName`.
8. Если после такого переименования имя всё равно конфликтует с существующим полем, provider возвращает ошибку.
9. SQL использует внутренние alias-ы `join_columnN`; пользовательские имена не попадают в ClickHouse SQL.
10. Дальше результат проходит обычный `LOAD` pipeline: temp table, query resolver, final table.

## SQL shape

Даже сейчас, когда читается только final table name, join строится через read-фрагмент:

```sql
SELECT
    l.`column1` AS `join_column1`,
    r.`column1` AS `join_column2`
FROM
(
    SELECT
        `column1`,
        `column2`
    FROM `left_final`
) AS l
INNER JOIN
(
    SELECT
        `column1`,
        `column2`
    FROM `right_final`
) AS r
ON l.`column1` = r.`column1`
SETTINGS join_use_nulls = 1
```

Позже `BuildTableReadSql` можно заменить на inline/temp SQL-фрагмент без изменения join-алгоритма.

## Null и дубликаты

Join key сравнивается ClickHouse-оператором `=`.

- `NULL = NULL` не матчится.
- Для outer join SQL явно включает `join_use_nulls = 1`, иначе ClickHouse вернет default-значения типа вместо `NULL`.
- Дубликаты ключей дают обычную many-to-many кардинальность.
- Специальных стратегий merge ключевых полей нет: оба key field остаются в результате и проходят общий алгоритм конфликтов имён.
