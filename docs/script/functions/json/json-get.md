# JsonGet

`JsonGet` возвращает raw JSON fragment как `text`.

Эта функция нужна, когда нужно вытащить часть JSON и дальше обрабатывать ее как JSON: например, сначала получить object или array, а потом читать значение внутри него.

## JsonGet(path)

`JsonGet(path)` читает значение из JSON по константному JSONPath.

Если путь не найден, входной JSON невалидный или вход равен `null`, результат `null`.

Если найден scalar, результат остается JSON-представлением scalar: строка будет с кавычками, число и bool без кавычек.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"user\":{\"name\":\"Mike\"}}'.JsonGet('$.user')` | `'{\"name\":\"Mike\"}'` |
| `'{\"items\":[{\"id\":1},{\"id\":2}]}'.JsonGet('$.items[1]')` | `'{\"id\":2}'` |
| `'[10,20]'.JsonGet('$[1]')` | `'20'` |
| `'{\"name\":\"Mike\"}'.JsonGet('$.name')` | `'\"Mike\"'` |
| `'{\"id\":42}'.JsonGet('$.id')` | `'42'` |
| `'{\"active\":true}'.JsonGet('$.active')` | `'true'` |
| `'{\"id\":42}'.JsonGet('$.missing')` | `null` |
| `'not-json'.JsonGet('$.name')` | `null` |
| `null.JsonGet('$.name')` | `null` |

## Path variants

Примеры поддерживаемых вариантов JSONPath:

| Expression | Result |
| --- | --- |
| `'{\"id\":42,\"name\":\"Mike\"}'.JsonGet('$')` | `'{\"id\":42,\"name\":\"Mike\"}'` |
| `'{\"user\":{\"name\":\"Mike\"}}'.JsonGet('$.user.name')` | `'\"Mike\"'` |
| `'{\"items\":[10,20]}'.JsonGet('$.items')` | `'[10,20]'` |
| `'{\"items\":[10,20]}'.JsonGet('$.items[0]')` | `'10'` |
| `'{\"items\":[{\"id\":1},{\"id\":2}]}'.JsonGet('$.items[*].id')` | `'1'` |
