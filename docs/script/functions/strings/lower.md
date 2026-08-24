# Lower

`Lower` переводит текст в нижний регистр.

## Lower(value)

`Lower(value)` возвращает строку, где буквы приведены к нижнему регистру.

Цифры, знаки пунктуации, пробелы и emoji не меняются.

Функция использует UTF-8-aware преобразование регистра ClickHouse.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Lower('Hello World!')` | `'hello world!'` |
| `Lower('')` | `''` |
| `Lower('already lower')` | `'already lower'` |
| `Lower('ABC 123 !?')` | `'abc 123 !?'` |
| `Lower('ПрИвЕт Мир!')` | `'привет мир!'` |
| `Lower('ёЁ')` | `'ёё'` |
| `Lower('😀Hello👍')` | `'😀hello👍'` |
| `Lower(null)` | `null` |
