# JsonLength

`JsonLength` возвращает длину JSON array/object.

Для scalar-значений результат `0`.

## JsonLength()

`JsonLength()` возвращает длину root JSON array/object.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'[1,2,3]'.JsonLength()` | `3` |
| `'{\"a\":1,\"b\":2}'.JsonLength()` | `2` |
| `'1'.JsonLength()` | `0` |
| `null.JsonLength()` | `null` |

## JsonLength(path)

`JsonLength(path)` возвращает длину JSON array/object по константному JSONPath.

Если путь не найден или вход равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"items\":[1,2,3]}'.JsonLength('$.items')` | `3` |
| `'{\"obj\":{\"a\":1,\"b\":2}}'.JsonLength('$.obj')` | `2` |
| `'{\"value\":1}'.JsonLength('$.value')` | `0` |
| `'{\"value\":1}'.JsonLength('$.missing')` | `null` |
| `null.JsonLength('$.items')` | `null` |
