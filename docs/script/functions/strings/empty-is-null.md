# EmptyIsNull

`EmptyIsNull` заменяет пустую строку на `null`.

## EmptyIsNull(value)

`EmptyIsNull(value)` возвращает `null`, если строка пустая.

Непустые строки возвращаются без изменений.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `EmptyIsNull('Hello world!')` | `'Hello world!'` |
| `EmptyIsNull('')` | `null` |
| `EmptyIsNull(null)` | `null` |
