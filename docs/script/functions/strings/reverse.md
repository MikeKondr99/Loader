# Reverse

`Reverse` разворачивает строку.

## Reverse(value)

`Reverse(value)` возвращает строку в обратном порядке.

Если вход равен `null`, результат тоже `null`.

`Reverse` использует ClickHouse `reverseUTF8`.

- Не работает с 4-byte символами если CH недостаточной версии (24.8). с 26.6 проверено такой проблемы нет. 

Примеры:

| Expression | Result |
| --- | --- |
| `Reverse('Hello')` | `'olleH'` |
| `Reverse('Привет мир!')` | `'!рим тевирП'` |
| `Reverse('é')` | `'́e'` |
| `Reverse('')` | `''` |
| `Reverse(null)` | `null` |
