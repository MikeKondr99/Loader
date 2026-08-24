# JsonGetNum

`JsonGetNum` возвращает JSON scalar как `num`.

## JsonGetNum()

`JsonGetNum()` читает root JSON scalar.

Число можно получить из JSON number или JSON string с числом. Десятичный разделитель должен быть точкой.

Примеры:

| Expression | Result |
| --- | --- |
| `'12.34'.JsonGetNum()` | `12.34` |
| `'42'.JsonGetNum()` | `42.0` |
| `'\"12.34\"'.JsonGetNum()` | `12.34` |
| `'\"12,34\"'.JsonGetNum()` | `null` |
| `null.JsonGetNum()` | `null` |

## JsonGetNum(path)

`JsonGetNum(path)` читает JSON scalar по константному JSONPath и пытается преобразовать его в `num`.

Если путь не найден, входной JSON невалидный или значение нельзя преобразовать, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"price\":12.34}'.JsonGetNum('$.price')` | `12.34` |
| `'{\"price\":42}'.JsonGetNum('$.price')` | `42.0` |
| `'{\"price\":\"12.34\"}'.JsonGetNum('$.price')` | `12.34` |
| `'{\"price\":\"12,34\"}'.JsonGetNum('$.price')` | `null` |
| `'{\"price\":\"42\"}'.JsonGetNum('$.price')` | `42.0` |
| `'{\"price\":\"not-num\"}'.JsonGetNum('$.price')` | `null` |
| `'{\"price\":true}'.JsonGetNum('$.price')` | `null` |
| `'{\"price\":false}'.JsonGetNum('$.price')` | `null` |
| `'{\"price\":12.34}'.JsonGetNum('$.missing')` | `null` |
| `'not-json'.JsonGetNum('$.price')` | `null` |
| `null.JsonGetNum('$.price')` | `null` |
