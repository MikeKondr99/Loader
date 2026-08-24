# RawType

`RawType` возвращает физический тип выражения в ClickHouse.

## RawType(input)

`RawType(input)` нужен для диагностики сгенерированного SQL и поведения ClickHouse.

В отличие от `Type`, эта функция показывает не доменный тип Loader, а результат ClickHouse `toTypeName(...)`.

Результат может меняться при изменении SQL-шаблона функции, версии ClickHouse или неявных преобразований.

Примеры:

| Expression | Result |
| --- | --- |
| `RawType(42)` | `'UInt8'` |
| `RawType(Int('42'))` | `'Nullable(Int64)'` |
| `RawType(Bool('abc'))` | `'Bool'` |
