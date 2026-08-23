# Functions

Функции используются в выражениях `LOAD`, `WHERE`, `GROUP BY`, `ORDER BY` и других местах, где script принимает expression.

Функции описываются по имени. Если у функции несколько перегрузок, они описываются на одной странице этой функции.

## Группы

- [Conversions](/docs/script/functions/conversions/index.md) - явные преобразования типов: `Text`, `Int`, `Num`, `Bool`, `Date`, `Time`.
- [Strings](/docs/script/functions/strings/index.md) - операции со строками: concat, регистр, trim и другие string helpers.
- [Numbers](/docs/script/functions/numbers/index.md) - арифметика, округление, остатки, знаки и числовые константы.
- [Trigonometry](/docs/script/functions/trigonometry/index.md) - синус, косинус, тангенс, обратные и гиперболические функции.
- [JSON](/docs/script/functions/json/index.md) - чтение JSON fragment/scalar, проверка путей, типы и длины.
- [Conditions](/docs/script/functions/conditions/index.md) - логика, сравнения, `If`, `Case`, `Alt` и проверки `null`.
- [Date/Time](/docs/script/functions/date-time/index.md) - сдвиги дат, календарные поля, части даты/времени, `Now` и `Today`.
- [Reflection](/docs/script/functions/reflection/index.md) - диагностика доменных и физических типов.
- [Color](/docs/script/functions/color/index.md) - создание числовых представлений цветов.
- [Financial](/docs/script/functions/financial/index.md) - финансовые вычисления.
- [Aggregation](/docs/script/functions/aggregation/index.md) - групповые функции: `COUNT`, `SUM`, `AVG`, `MIN/MAX`, `CONCAT` и другие.
- [Особые](/docs/script/functions/special/index.md) - функции, зависящие от script-контекста, например `ApplyMap`.

## Общие правила

Большинство функций можно вызывать в обычной форме и в method form:

```text
Text(value)
value.Text()
```

Если аргумент равен `null`, обычно результат тоже `null`, но точное поведение фиксируется на странице конкретной функции.

Если функция требует константный аргумент, это указывается рядом с конкретной перегрузкой.
