# Case

`Case` условно возвращает значение или альтернативу.

Основной сценарий для `Case` - собрать аналог `switch`, пока для него нет отдельного синтаксиса:

```text
Case(status = 'new', 'Новый')
    .Case(status = 'paid', 'Оплачен')
    .Case(status = 'closed', 'Закрыт')
    .Alt('Неизвестно')
```

Каждый `Case(condition, then)` возвращает `then` или `null`, следующий `.Case(...)` заполняет значение только если предыдущие ветки вернули `null`, а `.Alt(default)` задает значение по умолчанию.

Если разные ветки должны возвращать один тип, приведение лучше делать явно в самих `then` значениях.

## Case(condition, then)

`Case(condition, then)` возвращает `then`, если `condition = true`, иначе возвращает `null`.

Если `condition` равен `null`, результат `null`.

Тип результата совпадает с типом `then`, но результат nullable, потому что условие может не выполниться.

Примеры:

| Expression | Result |
| --- | --- |
| `Case(true, 'text')` | `'text'` |
| `Case(false, 'text')` | `null` |
| `Case(null, 'text')` | `null` |
| `Case(true, 42)` | `42` |
| `Case(false, 42)` | `null` |
| `Case(true, 3.14)` | `3.14` |
| `Case(false, 3.14)` | `null` |
| `Case(true, Date('2026-01-02'))` | `2026-01-02 00:00:00` |
| `Case(false, Date('2026-01-02'))` | `null` |
| `Case(true, Time('03:04:05'))` | `03:04:05` |
| `Case(false, Time('03:04:05'))` | `null` |
| `Type(Case(true, 'text'))` | `'text'` |
| `Type(Case(false, 'text'))` | `'text'` |
| `Type(Case(true, 42))` | `'int'` |
| `Type(Case(false, 42))` | `'int'` |
| `Type(Case(true, Date('2026-01-02')))` | `'date'` |
| `Type(Case(true, Time('03:04:05')))` | `'time'` |

## value.Case(condition, alt)

`value.Case(condition, alt)` возвращает `value`, если он не `null`.

Если `value` равен `null` и `condition = true`, возвращается `alt`.

Если `value` равен `null` и `condition = false` или `null`, возвращается `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Case('input', true, 'other')` | `'input'` |
| `Case('input', false, 'other')` | `'input'` |
| `Case(null, true, 'other')` | `'other'` |
| `Case(null, false, 'other')` | `null` |
| `Case(42, true, 100)` | `42` |
| `Case(42, false, 100)` | `42` |
| `Case(null, true, 100)` | `100` |
| `Case(null, false, 100)` | `null` |
| `Case(3.14, true, 2.71)` | `3.14` |
| `Case(null, true, 2.71)` | `2.71` |
| `Case(Date('2026-01-02'), true, Date('2026-01-03'))` | `2026-01-02 00:00:00` |
| `Case(Date(null), true, Date('2026-01-03'))` | `2026-01-03 00:00:00` |
| `Case(Date(null), false, Date('2026-01-03'))` | `null` |
| `Case(Time('03:04:05'), true, Time('06:07:08'))` | `03:04:05` |
| `Case(Time(null), true, Time('06:07:08'))` | `06:07:08` |
| `Case(Time(null), false, Time('06:07:08'))` | `null` |
| `Type(Case('input', true, 'other'))` | `'text'` |
| `Type(Case(null, true, 'other'))` | `'text'` |
| `Type(Case(null, false, 'other'))` | `'text'` |
| `Type(Case(42, true, 100))` | `'int'` |
| `Type(Case(Date(null), true, Date('2026-01-03')))` | `'date'` |
| `Type(Case(Time(null), true, Time('06:07:08')))` | `'time'` |
