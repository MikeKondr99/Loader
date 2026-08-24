# StartsWith

`StartsWith` проверяет начало строки.

## StartsWith(value, substring)

`StartsWith(value, substring)` возвращает `true`, если `value` начинается с `substring`.

Проверка case-sensitive.

Пустая подстрока считается найденной в начале любой строки.

Если `value` или `substring` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'hello'.StartsWith('hel')` | `true` |
| `StartsWith('hello', 'world')` | `false` |
| `'hello'.StartsWith('')` | `true` |
| `''.StartsWith('a')` | `false` |
| `''.StartsWith('')` | `true` |
| `StartsWith('привет', 'при')` | `true` |
| `'😀👍👋'.StartsWith('😀')` | `true` |
| `'Apple'.StartsWith('A')` | `true` |
| `'Apple'.StartsWith('a')` | `false` |
| `StartsWith(null, 'a')` | `null` |
| `StartsWith('abc', null)` | `null` |
