# Оптимизации финальных ClickHouse-таблиц

Цель: выбрать безопасные автоматические оптимизации для финальных таблиц, когда исходные данные неизвестны и доступны только простая schema/meta: типы, nullable, cardinality, min/max, плотность null.

## Что можно делать сейчас

### MergeTree для финальных таблиц

Финальные пользовательские таблицы стоит создавать на `MergeTree`.

`Log` подходит для staging/temp, где нужно быстро принять поток данных без пользовательских запросов. Для final-таблиц нужен `MergeTree`, потому что только он дает нормальную физическую сортировку `ORDER BY`, sparse primary index и skip indexes.

Решение:

```sql
-- staging
ENGINE = Log

-- final
ENGINE = MergeTree
ORDER BY ...
```

### Nullable по schema/meta

Если meta или schema уверенно говорит, что колонка не содержит null, финальная колонка должна быть non-nullable.

В бенчах на средних таблицах это не дало большого speedup, но это корректнее по контракту и потенциально дешевле на больших/широких таблицах.

Решение:

```text
CanBeNull = false -> T
CanBeNull = true  -> Nullable(T)
```

Не надо делать все колонки nullable “на всякий случай”, если у нас есть достоверная meta.

### LowCardinality(String)

`LowCardinality(String)` полезен для аналитики на низкой cardinality, но вреден на высокой.

По текущим бенчам:

- `unique <= 1_000`: обычно быстрее для `GROUP BY`, `WHERE =`, `WHERE IN`.
- `unique = 10_000`: уже рискованно; на 1M и 10M строк `WHERE`/`SELECT` заметно замедлялись.
- `unique >= 100_000`: почти всегда хуже.

Предварительное решение:

```text
String + known cardinality <= 1_000 -> LowCardinality(String)
String + unknown/high/exceeded cardinality -> String
```

Дополнительные условия:

- не включать без meta;
- не включать для all-unique text;
- не включать для null-only text;
- сделать порог настройкой writer-а, а не захардкодить навсегда.

## Что нельзя автоматизировать вслепую

### ORDER BY

`ORDER BY` самый сильный механизм оптимизации, но только если он совпадает с query pattern.

Текущие бенчи показывают:

- `ORDER BY (product_id, event_time)` сильно ускоряет `WHERE product_id IN (...) GROUP BY date`;
- `ORDER BY (city, event_time)` ускоряет `WHERE city = ... AND date range`;
- `ORDER BY (status, channel, city, event_time)` ускоряет фильтры по `status/channel`;
- просто `ORDER BY event_time` не показал универсального выигрыша на текущем датасете.

Практическое правило:

```text
Если известны частые WHERE equality/IN поля:
ORDER BY (where equality fields..., date/range field)
```

Примеры:

```sql
ORDER BY (city, event_time)
ORDER BY (status, channel, city, event_time)
ORDER BY (product_id, event_time)
```

Для неизвестных данных автоматически угадывать `ORDER BY` опасно. Низкая cardinality сама по себе говорит, что поле похоже на dimension, но не доказывает, что по нему будет `WHERE`.

Предварительное решение:

- если пользователь/скрипт явно задает ключ или known query pattern, использовать его;
- если query pattern неизвестен, не делать агрессивный `ORDER BY` только по cardinality;
- можно позже добавить рекомендатель, который предлагает ключ, но не применяет его молча.

### Skip indexes

Skip indexes в ClickHouse не являются B-tree индексами. Они не ищут строки, а помогают пропускать гранулы.

В текущем общем бенче варианты с skip indexes были медленнее аналогичных layout без индексов.

Решение:

```text
Не создавать skip indexes автоматически для неизвестных данных.
```

Их можно рассматривать только при известных запросах и после проверки:

```sql
EXPLAIN indexes = 1
SELECT ...
```

Кандидаты:

- `set(N)` для low-cardinality equality/IN;
- `minmax` для date/numeric range, если поле не входит в `ORDER BY`;
- `bloom_filter` для high-cardinality equality/IN.

Но это не замена правильному `ORDER BY`.

### Projections

Projections дают альтернативную физическую раскладку или pre-aggregation, но требуют дополнительного хранения и усложняют модель.

Решение:

```text
Не делать автоматически на первом этапе.
```

Вернуться, если появятся стабильные query patterns и таблицы будут достаточно большими.

### Codecs

Codecs пока не проверялись. Это оптимизация storage/IO, а не первая линия ускорения запросов.

Решение:

```text
Отложить.
```

## Что еще надо исследовать

### ORDER BY на более реалистичных сценариях

Нужно понять, можно ли получить безопасную эвристику для неизвестных таблиц.

Проверить:

- `ORDER BY tuple()` baseline;
- `ORDER BY date`;
- `ORDER BY dimension,date`;
- `ORDER BY date,dimension`;
- `ORDER BY several low-cardinality fields by increasing cardinality, date`;
- `ORDER BY high-selective id,date`.

Важно отдельно проверять:

- широкие date ranges;
- узкие date ranges;
- equality по dimension;
- `IN` по dimension/id;
- group by по тем же полям.

### LowCardinality на длинных строках

Текущий benchmark использует короткие значения вида `value_00000010`.

Надо проверить:

- короткие строки;
- средние строки;
- длинные повторяющиеся строки;
- nullable strings;
- разные `unique / row_count` ratio.

Текущий вывод `unique <= 1_000` может оказаться слишком консервативным для длинных строк.

### Skip indexes отдельно

Нужно проверить не просто “индекс есть/нет”, а реально ли ClickHouse его использует.

Для каждого кандидата:

```sql
EXPLAIN indexes = 1
SELECT ...
```

Сравнить:

- без `ORDER BY`;
- с подходящим `ORDER BY`;
- с неподходящим `ORDER BY`;
- разную гранулярность index-а.

### Nullable cost на больших и широких таблицах

Первый бенч не показал большой разницы, но он был не про wide nullable-heavy workload.

Проверить:

- много nullable columns;
- мало nullable columns;
- wide table;
- aggregation по nullable/non-nullable numeric fields.

## Предварительная стратегия для writer-а

На текущих данных безопасный первый вариант:

```text
1. staging table:
   ENGINE = Log

2. final table:
   ENGINE = MergeTree

3. Nullable:
   по schema/meta

4. Integer/Decimal:
   сужать по min/max и precision/scale

5. String:
   LowCardinality(String), если known unique <= 1_000
   иначе String

6. ORDER BY:
   если задан пользователем или известен query pattern
   иначе осторожный default или tuple(), решение отдельно

7. Skip indexes:
   не создавать автоматически

8. Projections/codecs:
   не делать на первом этапе
```
