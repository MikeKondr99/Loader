# Reverse

`Reverse` разворачивает строку.

## Reverse(value)

`Reverse(value)` возвращает строку в обратном порядке.

Если вход равен `null`, результат тоже `null`.

TODO: текущая реализация требует отдельной проверки и доработки для emoji/surrogate pairs.

Примеры:

| Expression | Result |
| --- | --- |
| `Reverse('Hello')` | `'olleH'` |
| `Reverse('Привет мир!')` | `'!рим тевирП'` |
| `Reverse('')` | `''` |
| `Reverse(null)` | `null` |
