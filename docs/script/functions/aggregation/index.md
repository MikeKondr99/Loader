# Aggregation Functions

Aggregation functions собирают несколько строк в одно значение и используются в `LOAD` вместе с `GROUP BY` или без него.

Если `GROUP BY` отсутствует, весь источник считается одной группой.

## Функции

- [COUNT](/docs/script/functions/aggregation/count.md) - количество строк или non-null значений.
- [COUNT_IF](/docs/script/functions/aggregation/count-if.md) - количество строк, где условие истинно.
- [COUNT_DISTINCT](/docs/script/functions/aggregation/count-distinct.md) - количество уникальных non-null значений.
- [SUM](/docs/script/functions/aggregation/sum.md) - сумма числовых значений.
- [AVG](/docs/script/functions/aggregation/avg.md) - среднее числовое значение или средняя дата.
- [STDDEV](/docs/script/functions/aggregation/stddev.md) - стандартное отклонение числовых значений.
- [CORREL](/docs/script/functions/aggregation/correl.md) - корреляция Пирсона между двумя числовыми выражениями.
- [MIN](/docs/script/functions/aggregation/min.md) - минимальное значение.
- [MAX](/docs/script/functions/aggregation/max.md) - максимальное значение.
- [ONLY](/docs/script/functions/aggregation/only.md) - значение, если уникальное non-null значение ровно одно.
- [MODE](/docs/script/functions/aggregation/mode.md) - наиболее частое non-null значение.
- [CONCAT](/docs/script/functions/aggregation/concat.md) - склейка text значений внутри группы.
- [MEDIAN](/docs/script/functions/aggregation/median.md) - медиана numeric значений.
- [FRACTILE](/docs/script/functions/aggregation/fractile.md) - квантиль numeric значений.

## Общая семантика

Агрегации игнорируют `null`, если на странице конкретной функции не указано другое.

`COUNT()` считает строки, включая строки с `null`.

`COUNT(value)` и остальные агрегации работают по non-null значениям.

`MEDIAN` сейчас принимает `num`, `FRACTILE` принимает `num` и `int`.
