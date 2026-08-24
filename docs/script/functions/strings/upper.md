# Upper

`Upper` переводит текст в верхний регистр.

## Upper(value)

`Upper(value)` возвращает строку, где латинские буквы ASCII приведены к верхнему регистру.

Цифры, знаки пунктуации, пробелы и emoji не меняются.

Кириллица в текущей реализации регистр не меняет.

TODO: добавить поддержку Unicode-case для кириллицы и других не-ASCII букв.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Upper('Hello World!')` | `'HELLO WORLD!'` |
| `Upper('')` | `''` |
| `Upper('ALREADY UPPER')` | `'ALREADY UPPER'` |
| `Upper('abc 123 !?')` | `'ABC 123 !?'` |
| `Upper('ПрИвЕт Мир!')` | `'ПрИвЕт Мир!'` |
| `Upper('😀Hello👍')` | `'😀HELLO👍'` |
| `Upper(null)` | `null` |
