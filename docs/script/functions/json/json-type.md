# JsonType

`JsonType` возвращает тип JSON-значения как `text`.

Типы возвращаются в формате ClickHouse: например, `Int64`, `Double`, `Bool`, `String`, `Array`, `Object`, `Null`.

## JsonType()

`JsonType()` возвращает тип root JSON-значения.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'1'.JsonType()` | `'Int64'` |
| `'true'.JsonType()` | `'Bool'` |
| `'\"text\"'.JsonType()` | `'String'` |
| `'[1,2]'.JsonType()` | `'Array'` |
| `'{\"x\":1}'.JsonType()` | `'Object'` |
| `'null'.JsonType()` | `'Null'` |
| `null.JsonType()` | `null` |

## JsonType(path)

`JsonType(path)` возвращает тип JSON-значения по константному JSONPath.

Если путь не найден или вход равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"value\":1}'.JsonType('$.value')` | `'Int64'` |
| `'{\"value\":12.34}'.JsonType('$.value')` | `'Double'` |
| `'{\"value\":true}'.JsonType('$.value')` | `'Bool'` |
| `'{\"value\":\"text\"}'.JsonType('$.value')` | `'String'` |
| `'{\"value\":[1,2]}'.JsonType('$.value')` | `'Array'` |
| `'{\"value\":{\"x\":1}}'.JsonType('$.value')` | `'Object'` |
| `'{\"value\":null}'.JsonType('$.value')` | `'Null'` |
| `'{\"value\":1}'.JsonType('$.missing')` | `null` |
| `null.JsonType('$.value')` | `null` |
