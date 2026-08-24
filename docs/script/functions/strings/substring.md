# Substring

`Substring` возвращает часть строки.

Позиция `start` начинается с `1`.

Функция работает по UTF-8 символам, а не по байтам.

## Substring(value, start)

`Substring(value, start)` возвращает часть строки от позиции `start` до конца.

Если `value` или `start` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Substring('Hello World!', 1)` | `'Hello World!'` |
| `Substring('Hello World!', 5)` | `'o World!'` |
| `Substring('Hello World!', 12)` | `'!'` |
| `Substring('Hello World!', 13)` | `''` |
| `Substring('Привет мир!', 3)` | `'ивет мир!'` |
| `Substring('😀👍👋', 2)` | `'👍👋'` |
| `Substring(null, 1)` | `null` |
| `Substring('Hello World!', null)` | `null` |

## Substring(value, start, count)

`Substring(value, start, count)` возвращает часть строки от позиции `start` длиной `count`.

Если `count = 0`, результат пустая строка.

Отрицательный `count` сейчас даёт ClickHouse-specific результат и не должен считаться устойчивой доменной семантикой.

Если любой аргумент равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Substring('Hello World!', 5, 3)` | `'o W'` |
| `Substring('Hello World!', 1, 5)` | `'Hello'` |
| `Substring('Hello World!', 5, 0)` | `''` |
| `Substring('Hello World!', 5, -1)` | `'o World'` |
| `Substring('Привет мир!', 3, 4)` | `'ивет'` |
| `Substring('😀👍👋', 2, 1)` | `'👍'` |
| `Substring('a😀b👍c', 2, 3)` | `'😀b👍'` |
| `Substring(null, 1, 3)` | `null` |
| `Substring('Hello World!', 1, null)` | `null` |
