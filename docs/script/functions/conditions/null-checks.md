# Null Checks

`IsNull` и `NotNull` проверяют значение на `null`.

## IsNull(value)

`IsNull(value)` возвращает `true`, если значение равно `null`.

Результат всегда non-null `bool`.

Примеры:

| Expression | Result |
| --- | --- |
| `IsNull(null)` | `true` |
| `IsNull(42)` | `false` |
| `IsNull('text')` | `false` |
| `IsNull('')` | `false` |
| `IsNull(0)` | `false` |
| `IsNull(1 + null)` | `true` |
| `IsNull(Lower(Text(null)))` | `true` |
| `IsNull(Date('2026-01-02'))` | `false` |
| `IsNull(Date(null))` | `true` |
| `IsNull(Time('03:04:05'))` | `false` |
| `IsNull(Time(null))` | `true` |

## NotNull(value)

`NotNull(value)` возвращает `true`, если значение не равно `null`.

Результат всегда non-null `bool`.

Примеры:

| Expression | Result |
| --- | --- |
| `NotNull(null)` | `false` |
| `NotNull(42)` | `true` |
| `NotNull('text')` | `true` |
| `NotNull('')` | `true` |
| `NotNull(0)` | `true` |
| `NotNull(1 + null)` | `false` |
| `NotNull(Lower(Text(null)))` | `false` |
| `NotNull(Date('2026-01-02'))` | `true` |
| `NotNull(Date(null))` | `false` |
| `NotNull(Time('03:04:05'))` | `true` |
| `NotNull(Time(null))` | `false` |
