# Concat +

Оператор `+` склеивает два значения типа `text`.

Функция нужна, когда нужно собрать строку из нескольких частей: префикса, значения поля, результата `Text(...)` и других строк.

## left + right

Оба аргумента должны быть текстовыми выражениями.

Если один из аргументов равен `null`, результат тоже `null`.

Оператор сохраняет пробелы, переносы строк, табуляции и Unicode-символы.

Примеры:

| Expression | Result |
| --- | --- |
| `'hello' + 'world'` | `'helloworld'` |
| `'a' + 'b' + 'c'` | `'abc'` |
| `'' + 'text'` | `'text'` |
| `'text' + ''` | `'text'` |
| `'' + ''` | `''` |
| `'text' + null` | `null` |
| `null + 'text'` | `null` |
| `null + null` | `null` |
| `'hello ' + 'world'` | `'hello world'` |
| `'line1\n' + 'line2'` | `"line1\nline2"` |
| `'tab\t' + 'end'` | `"tab\tend"` |
| `'number: ' + Text(42)` | `'number: 42'` |
| `Text(3.14) + ' is pi'` | `'3.14 is pi'` |
| `'result: ' + Text(true)` | `'result: true'` |
| `Text(false) + ' is false'` | `'false is false'` |
| `'text' + Text(null)` | `null` |
| `Text(null) + 'text'` | `null` |
