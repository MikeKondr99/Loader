# DbName

`DbName` возвращает имя внутренней базы выполнения.

## DbName()

`DbName()` сейчас возвращает `'ClickHouse'`.

TODO: удалить функцию, если внутренний backend выполнения всегда будет только ClickHouse.

Функция диагностическая: она показывает, на каком backend выполняются выражения Loader.

Примеры:

| Expression | Result |
| --- | --- |
| `DbName()` | `'ClickHouse'` |
