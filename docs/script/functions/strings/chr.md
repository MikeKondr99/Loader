# Chr

`Chr` возвращает символ по Unicode-коду.

## Chr(code)

`Chr(code)` возвращает символ для указанного Unicode code point.

Если `code` вне диапазона `0..1114111`, результат `null`.

Если `code` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Chr(65)` | `'A'` |
| `Chr(97)` | `'a'` |
| `Chr(32)` | `' '` |
| `Chr(9)` | tab |
| `Chr(10)` | newline |
| `Chr(13)` | carriage return |
| `Chr(0)` | zero byte |
| `Chr(1055)` | `'П'` |
| `Chr(1087)` | `'п'` |
| `Chr(128512)` | `'😀'` |
| `Chr(128077)` | `'👍'` |
| `Chr(8364)` | `'€'` |
| `Chr(-1)` | `null` |
| `Chr(1114112)` | `null` |
| `Chr(null)` | `null` |
