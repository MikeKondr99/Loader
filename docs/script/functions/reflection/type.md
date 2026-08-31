# Type

`Type` возвращает доменный тип выражения в Loader.

## Type(input)

`Type(input)` возвращает текстовое имя типа.

Если тип точно не может быть `null`, к имени добавляется `!`.

Если выражение может вернуть `null`, имя возвращается без `!`.

`Type(null)` возвращает `null` как текстовое имя типа, а не SQL `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Type(42)` | `'int!'` |
| `Type(If(false, 42, null))` | `'int'` |
| `Type(Int(null))` | `'int'` |
| `Type('hello')` | `'text!'` |
| `Type(If(false, 'hello', null))` | `'text'` |
| `Type(''.EmptyIsNull())` | `'text'` |
| `Type(3.14)` | `'num!'` |
| `Type(If(false, 3.14, null))` | `'num'` |
| `Type(Num(null))` | `'num'` |
| `Type(Date(2023, 1, 1))` | `'date!'` |
| `Type(Date(null, 1, 1))` | `'date'` |
| `Type(Time('03:04:05'))` | `'time'` |
| `Type(Time(null))` | `'time'` |
| `Type(true)` | `'bool!'` |
| `Type(If(false, true, null))` | `'bool'` |
| `Type(null)` | `'null'` |
