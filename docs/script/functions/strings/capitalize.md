# Capitalize

`Capitalize` приводит первую букву каждого слова к верхнему регистру, остальные буквы слова к нижнему.

## Capitalize(value)

`Capitalize(value)` принимает `text` и возвращает `text`.

Функция использует UTF-8-aware преобразование ClickHouse. Разделителями слов считаются не только пробелы: например, после дефиса или emoji следующее слово тоже начинается заново.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Capitalize('hello world')` | `'Hello World'` |
| `Capitalize('already Capitalized')` | `'Already Capitalized'` |
| `Capitalize('mixed-case text')` | `'Mixed-Case Text'` |
| `Capitalize('привет мир')` | `'Привет Мир'` |
| `Capitalize('ёЖИК В тУмАНе')` | `'Ёжик В Тумане'` |
| `Capitalize('😀hello 👍world')` | `'😀Hello 👍World'` |
| `Capitalize('')` | `''` |
| `Capitalize(null)` | `null` |

