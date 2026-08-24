# JsonGetBool

`JsonGetBool` возвращает JSON scalar как `bool`.

## JsonGetBool()

`JsonGetBool()` читает root JSON scalar.

Поддерживаются значения `true`, `false`, `1`, `0` и строки с этими значениями.

Примеры:

| Expression | Result |
| --- | --- |
| `'true'.JsonGetBool()` | `true` |
| `'false'.JsonGetBool()` | `false` |
| `'1'.JsonGetBool()` | `true` |
| `'0'.JsonGetBool()` | `false` |
| `'\"true\"'.JsonGetBool()` | `true` |
| `'\"false\"'.JsonGetBool()` | `false` |
| `'\"not-bool\"'.JsonGetBool()` | `null` |
| `null.JsonGetBool()` | `null` |

## JsonGetBool(path)

`JsonGetBool(path)` читает JSON scalar по константному JSONPath и пытается преобразовать его в `bool`.

Если путь не найден, входной JSON невалидный или значение нельзя преобразовать, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"active\":true}'.JsonGetBool('$.active')` | `true` |
| `'{\"active\":false}'.JsonGetBool('$.active')` | `false` |
| `'{\"active\":1}'.JsonGetBool('$.active')` | `true` |
| `'{\"active\":0}'.JsonGetBool('$.active')` | `false` |
| `'{\"active\":\"true\"}'.JsonGetBool('$.active')` | `true` |
| `'{\"active\":\"false\"}'.JsonGetBool('$.active')` | `false` |
| `'{\"active\":\"1\"}'.JsonGetBool('$.active')` | `true` |
| `'{\"active\":\"0\"}'.JsonGetBool('$.active')` | `false` |
| `'{\"active\":\"not-bool\"}'.JsonGetBool('$.active')` | `null` |
| `'{\"active\":true}'.JsonGetBool('$.missing')` | `null` |
| `'not-json'.JsonGetBool('$.active')` | `null` |
| `null.JsonGetBool('$.active')` | `null` |
