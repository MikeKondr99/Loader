# Conversion Functions

Conversion functions явно преобразуют значение в нужный доменный тип.

## Функции

- [Text](/docs/script/functions/conversions/text.md) - преобразование значения в `text`.
- [Int](/docs/script/functions/conversions/int.md) - преобразование значения в `int`.
- [Num](/docs/script/functions/conversions/num.md) - преобразование значения в `num`.
- [Bool](/docs/script/functions/conversions/bool.md) - преобразование значения в `bool`.
- [Date](/docs/script/functions/conversions/date.md) - преобразование значения в `date`.
- [Time](/docs/script/functions/conversions/time.md) - преобразование значения в `time`.

## Общая семантика

Conversion functions нужны, когда тип значения нужно задать явно: например, при чтении строковых данных из файла, подготовке ключей, фильтрации или арифметике.

Если вход невалиден для выбранного типа, conversion function обычно возвращает `null`, а не ошибку. Исключения и текущие TODO фиксируются на странице конкретной функции.

Голый `null` обычно автоматически приводится к нужному типу по контексту. Явная conversion function нужна только если контекста недостаточно или нужно явно подсказать тип `null`.
