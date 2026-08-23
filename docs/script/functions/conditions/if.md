# If

`If` выбирает одно из двух значений по условию.

## If(condition, then, else)

`If(condition, then, else)` возвращает `then`, если `condition = true`, иначе возвращает `else`.

`If` нельзя вызывать через method form: записи вида `condition.If(then, else)` не поддерживаются.

Если `condition` равен `null`, он обрабатывается как `false`.

`then` и `else` должны приводиться к одному доменному типу. Если один вариант `null`, тип берется из второго варианта.

Примеры:

| Expression | Result |
| --- | --- |
| `If(true, null, 0).Type()` | `'int'` |
| `If(true, null, 0.0).Type()` | `'num'` |
| `If(true, null, 'lol').Type()` | `'text'` |
| `If(null, 1, 0).Type()` | `'int!'` |
| `If(null, 1.0, 0.0).Type()` | `'num!'` |
| `If(null, 'one', 'zero').Type()` | `'text!'` |
| `If(null, 'then', 'else')` | `'else'` |
| `If(10 > 5 and null, 'then', 'else')` | `'else'` |
| `If(true, 10, 15.5)` | `10.0` |
| `If(true, 0, 12 / 0)` | `0` |
| `If(true, Date('2026-01-02'), Date('2026-01-03'))` | `2026-01-02 00:00:00` |
| `If(false, Date('2026-01-02'), Date('2026-01-03'))` | `2026-01-03 00:00:00` |
| `If(null, Date('2026-01-02'), Date('2026-01-03'))` | `2026-01-03 00:00:00` |
| `If(true, null, Date('2026-01-03')).Type()` | `'date'` |
| `If(true, Time('03:04:05'), Time('06:07:08'))` | `03:04:05` |
| `If(false, Time('03:04:05'), Time('06:07:08'))` | `06:07:08` |
| `If(null, Time('03:04:05'), Time('06:07:08'))` | `06:07:08` |
| `If(true, null, Time('06:07:08')).Type()` | `'time'` |
