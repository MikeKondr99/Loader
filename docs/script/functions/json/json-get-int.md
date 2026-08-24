# JsonGetInt

`JsonGetInt` возвращает JSON scalar как `int`.

## JsonGetInt()

`JsonGetInt()` читает root JSON scalar.

Целое число можно получить из JSON number или JSON string с целым числом.

Дробные числа, bool, нечисловые строки и `null` дают `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'42'.JsonGetInt()` | `42` |
| `'12.34'.JsonGetInt()` | `null` |
| `'\"42\"'.JsonGetInt()` | `42` |
| `null.JsonGetInt()` | `null` |

## JsonGetInt(path)

`JsonGetInt(path)` читает JSON scalar по константному JSONPath и пытается преобразовать его в `int`.

Если путь не найден, входной JSON невалидный или значение нельзя преобразовать, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"id\":42}'.JsonGetInt('$.id')` | `42` |
| `'{\"items\":[10,20]}'.JsonGetInt('$.items[1]')` | `20` |
| `'{\"id\":\"42\"}'.JsonGetInt('$.id')` | `42` |
| `'{\"id\":12.34}'.JsonGetInt('$.id')` | `null` |
| `'{\"id\":\"not-int\"}'.JsonGetInt('$.id')` | `null` |
| `'{\"id\":true}'.JsonGetInt('$.id')` | `null` |
| `'{\"id\":false}'.JsonGetInt('$.id')` | `null` |
| `'{\"id\":42}'.JsonGetInt('$.missing')` | `null` |
| `'not-json'.JsonGetInt('$.id')` | `null` |
| `null.JsonGetInt('$.id')` | `null` |
