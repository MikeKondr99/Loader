# Upper

`Upper` переводит текст в верхний регистр.

## Upper(value)

`Upper(value)` возвращает строку, где буквы приведены к верхнему регистру.

Цифры, знаки пунктуации, пробелы и emoji не меняются.

Функция использует UTF-8-aware преобразование регистра ClickHouse.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Upper('Hello World!')` | `'HELLO WORLD!'` |
| `Upper('')` | `''` |
| `Upper('ALREADY UPPER')` | `'ALREADY UPPER'` |
| `Upper('abc 123 !?')` | `'ABC 123 !?'` |
| `Upper('ПрИвЕт Мир!')` | `'ПРИВЕТ МИР!'` |
| `Upper('ёЁ')` | `'ЁЁ'` |
| `Upper('😀Hello👍')` | `'😀HELLO👍'` |
| `Upper(null)` | `null` |
