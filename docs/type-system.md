# Система типов

В проекте сейчас есть два близких, но не объединенных типа:

- `Loader.Core.Models.DataType` - типы доменного reader pipeline и provider metadata.
- `Loader.Query.Models.DataType` - типы expression/query resolver-а.

Их объединение остается отдельной задачей roadmap.

## Core DataType

Базовый набор:

- `Text`
- `Integer`
- `Number`
- `DateTime`
- `Date`
- `Time`
- `Boolean`

`Normalize()` строит `DataSchema` по source reader и приводит значения к доменному контракту.
Декораторы, меняющие значения (`Normalize`, `AutoCast`), обязаны менять `DataSchema`, `GetColumnSchema()` и `GetSchemaTable()` согласованно.

## Query DataType

Query layer использует собственные типы для resolve/compile выражений.
Тип выражения хранится в `ExprType`:

- `DataType`
- `CanBeNull`
- aggregate/constant flags

`QueryResolver` использует эти типы для выбора overload-ов функций, implicit casts и output schema.

## Схемы

Есть три разных уровня схемы:

- Source schema: то, что отдал provider или DB driver.
- Domain schema: `DataSchema` после `Normalize()`/`AutoCast()`.
- Query schema: `ResolvedQuery.OutputFields` после resolve выражений.

В `Loader.Script` переход выглядит так:

```text
provider DbDataReader
-> Normalize() DataSchema
-> QuerySource.Fields
-> ResolvedQuery.OutputFields
-> LoadedTable.Fields
```

## Файловые источники

Файловые источники не гарантируют надежные source-типы:

- CSV фактически текстовый.
- Excel может менять типы от ячейки к ячейке.
- JSON/XML анализируют shape, но значения в provider reader остаются текстовыми.
- QVD имеет собственные symbol tables и dual values, но тоже проходит через доменный mapper.

Если нужен typing поверх файлов, сейчас путь такой:

```text
Analyze/CollectAutoCast -> AutoCastSchema -> AutoCast()
```

В Script pipeline пользовательские преобразования типов обычно задаются выражениями `LOAD`, например `Int(id)`, `Num(amount)`, `Date(created, 'yyyy-MM-dd')`.
