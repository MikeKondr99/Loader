# Left / Right / Mid

`Left`, `Right` и `Mid` возвращают часть строки.

Позиция `start` в `Mid` начинается с `1`.

Функции работают по UTF-8 символам, а не по байтам.

## Left(value, count)

`Left(value, count)` возвращает `count` символов с начала строки.

Если `count <= 0`, результат пустая строка.

Если любой аргумент равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Left('Hello World!', 5)` | `'Hello'` |
| `Left('Hello World!', 20)` | `'Hello World!'` |
| `Left('Hello World!', 0)` | `''` |
| `Left('Hello World!', -1)` | `''` |
| `Left('Привет мир!', 6)` | `'Привет'` |
| `Left('😀👍👋', 2)` | `'😀👍'` |
| `Left(null, 5)` | `null` |
| `Left('Hello World!', null)` | `null` |

## Right(value, count)

`Right(value, count)` возвращает `count` символов с конца строки.

Если `count <= 0`, результат пустая строка.

Если любой аргумент равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Right('Hello World!', 6)` | `'World!'` |
| `Right('Hello World!', 20)` | `'Hello World!'` |
| `Right('Hello World!', 0)` | `''` |
| `Right('Hello World!', -1)` | `''` |
| `Right('Привет мир!', 4)` | `'мир!'` |
| `Right('😀👍👋', 2)` | `'👍👋'` |
| `Right(null, 5)` | `null` |
| `Right('Hello World!', null)` | `null` |

## Mid(value, start)

`Mid(value, start)` возвращает часть строки от позиции `start` до конца.

Если `value` или `start` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Mid('Hello World!', 1)` | `'Hello World!'` |
| `Mid('Hello World!', 5)` | `'o World!'` |
| `Mid('Hello World!', 12)` | `'!'` |
| `Mid('Hello World!', 13)` | `''` |
| `Mid('Привет мир!', 3)` | `'ивет мир!'` |
| `Mid('😀👍👋', 2)` | `'👍👋'` |
| `Mid(null, 1)` | `null` |
| `Mid('Hello World!', null)` | `null` |

## Mid(value, start, count)

`Mid(value, start, count)` возвращает часть строки от позиции `start` длиной `count`.

Если `count = 0`, результат пустая строка.

Если `count < 0`, результат тоже пустая строка.

Если любой аргумент равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Mid('Hello World!', 5, 3)` | `'o W'` |
| `Mid('Hello World!', 1, 5)` | `'Hello'` |
| `Mid('Hello World!', 5, 0)` | `''` |
| `Mid('Hello World!', 5, -1)` | `''` |
| `Mid('Привет мир!', 3, 4)` | `'ивет'` |
| `Mid('😀👍👋', 2, 1)` | `'👍'` |
| `Mid('a😀b👍c', 2, 3)` | `'😀b👍'` |
| `Mid(null, 1, 3)` | `null` |
| `Mid('Hello World!', 1, null)` | `null` |
