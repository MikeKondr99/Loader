# Alt

`Alt` возвращает первое не-null значение.

## Alt(value, alternative)

`Alt(value, alternative)` возвращает `value`, если он не `null`, иначе возвращает `alternative`.

Оба аргумента должны приводиться к одному доменному типу.

Если оба аргумента `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `2.Alt(3).Type()` | `'int!'` |
| `Int(null).Alt(3).Type()` | `'int!'` |
| `2.Alt(Int(null)).Type()` | `'int!'` |
| `Int(null).Alt(Int(null)).Type()` | `'int'` |
| `2.Alt(3)` | `2` |
| `Int(null).Alt(3)` | `3` |
| `2.Alt(Int(null))` | `2` |
| `Int(null).Alt(Int(null))` | `null` |
| `Int(null).Alt(If(true, 2, null))` | `2` |
| `'first'.Alt('second')` | `'first'` |
| `Text(null).Alt('default')` | `'default'` |
| `Date('2026-01-02').Alt(Date('2026-01-03'))` | `2026-01-02 00:00:00` |
| `Date(null).Alt(Date('2026-01-03'))` | `2026-01-03 00:00:00` |
| `Date(null).Alt(Date(null))` | `null` |
| `Time('03:04:05').Alt(Time('06:07:08'))` | `03:04:05` |
| `Time(null).Alt(Time('06:07:08'))` | `06:07:08` |
| `Time(null).Alt(Time(null))` | `null` |
