# JsonHas

`JsonHas` проверяет, существует ли значение по JSONPath.

## JsonHas(path)

`JsonHas(path)` возвращает `true`, если путь существует.

JSON `null` считается существующим значением.

Если входной JSON невалидный, результат `false`. Если вход равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'{\"geometry\":{\"x\":1}}'.JsonHas('$.geometry')` | `true` |
| `'{\"geometry\":null}'.JsonHas('$.geometry')` | `true` |
| `'{\"geometry\":{\"x\":1}}'.JsonHas('$.geometry.x')` | `true` |
| `'{\"geometry\":{\"x\":1}}'.JsonHas('$.missing')` | `false` |
| `'not-json'.JsonHas('$.geometry')` | `false` |
| `null.JsonHas('$.geometry')` | `null` |
