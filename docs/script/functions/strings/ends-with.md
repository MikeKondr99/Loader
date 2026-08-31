# EndsWith

`EndsWith` проверяет конец строки.

## EndsWith(value, substring)

`EndsWith(value, substring)` возвращает `true`, если `value` заканчивается на `substring`.

Проверка case-sensitive.

Пустая подстрока считается найденной в конце любой строки.

Если `value` или `substring` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'hello'.EndsWith('llo')` | `true` |
| `EndsWith('hello', 'world')` | `false` |
| `'hello'.EndsWith('')` | `true` |
| `''.EndsWith('a')` | `false` |
| `''.EndsWith('')` | `true` |
| `EndsWith(null, '')` | `null` |
| `EndsWith('привет', 'вет')` | `true` |
| `'😀👍👋'.EndsWith('👋')` | `true` |
| `'Apple'.EndsWith('e')` | `true` |
| `'Apple'.EndsWith('E')` | `false` |
| `EndsWith(null, 'a')` | `null` |
| `EndsWith('abc', null)` | `null` |
