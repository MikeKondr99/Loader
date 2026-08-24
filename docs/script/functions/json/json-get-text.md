# JsonGetText

`JsonGetText` возвращает JSON scalar как `text`.

Если значение по пути является строкой, кавычки JSON убираются. Числа и bool возвращаются как текстовое представление.

## JsonGetText()

`JsonGetText()` читает root JSON scalar.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'\"Mike\"'.JsonGetText()` | `'Mike'` |
| `'42'.JsonGetText()` | `'42'` |
| `'true'.JsonGetText()` | `'true'` |
| `null.JsonGetText()` | `null` |

## JsonGetText(path)

`JsonGetText(path)` читает JSON scalar по константному JSONPath.

Если путь не найден, входной JSON невалидный или вход равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"user\":{\"name\":\"Mike\"}}'.JsonGetText('$.user.name')` | `'Mike'` |
| `'{\"items\":[{\"name\":\"first\"},{\"name\":\"second\"}]}'.JsonGetText('$.items[1].name')` | `'second'` |
| `'{\"empty\":\"\"}'.JsonGetText('$.empty')` | `''` |
| `'{\"value\":true}'.JsonGetText('$.value')` | `'true'` |
| `'{\"value\":false}'.JsonGetText('$.value')` | `'false'` |
| `'{\"value\":42}'.JsonGetText('$.value')` | `'42'` |
| `'{\"value\":12.34}'.JsonGetText('$.value')` | `'12.34'` |
| `'{\"user\":{\"name\":\"Mike\"}}'.JsonGet('$.user.name').JsonGetText()` | `'Mike'` |
| `'{\"user\":{\"name\":\"Mike\"}}'.JsonGetText('$.missing')` | `null` |
| `'not-json'.JsonGetText('$.name')` | `null` |
| `null.JsonGetText('$.name')` | `null` |
