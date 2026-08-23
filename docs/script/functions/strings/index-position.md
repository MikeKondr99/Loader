# Index

`Index` возвращает позицию первого вхождения подстроки.

## Index(value, substring)

`Index(value, substring)` возвращает позицию первого вхождения `substring` в `value`.

Позиция начинается с `1`.

Если подстрока не найдена, результат `null`.

Если `substring` пустая строка, результат `1`.

Если `value` или `substring` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Index('abc', 'a')` | `1` |
| `Index('abc', 'b')` | `2` |
| `Index('abc', 'c')` | `3` |
| `Index('abc', 'bc')` | `2` |
| `Index('aaaaAaaa', 'A')` | `5` |
| `Index('abc', 'd')` | `null` |
| `Index('', 'a')` | `null` |
| `Index('abc', '')` | `1` |
| `Index('aabaa', 'aa')` | `1` |
| `Index('привет', 'ив')` | `3` |
| `Index(null, 'a')` | `null` |
| `Index('abc', null)` | `null` |
