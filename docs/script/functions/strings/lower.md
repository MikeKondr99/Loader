# Lower

`Lower` переводит текст в нижний регистр.

## Lower(value)

`Lower(value)` возвращает строку, где латинские буквы ASCII приведены к нижнему регистру.

Цифры, знаки пунктуации, пробелы и emoji не меняются.

Кириллица в текущей реализации регистр не меняет.

TODO: добавить поддержку Unicode-case для кириллицы и других не-ASCII букв.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Lower('Hello World!')` | `'hello world!'` |
| `Lower('')` | `''` |
| `Lower('already lower')` | `'already lower'` |
| `Lower('ABC 123 !?')` | `'abc 123 !?'` |
| `Lower('ПрИвЕт Мир!')` | `'ПрИвЕт Мир!'` |
| `Lower('😀Hello👍')` | `'😀hello👍'` |
| `Lower(null)` | `null` |
