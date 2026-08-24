# Condition Functions

Condition functions работают с `bool` и используются в `WHERE`, `If`, `Case`, фильтрации и вычисляемых полях.

## Функции

- [Logic](/docs/script/functions/conditions/logic.md) - `and`, `or`, `Not`.
- [Comparison](/docs/script/functions/conditions/comparison.md) - `=`, `!=`, `<`, `>`, `<=`, `>=`, `Between`.
- [If](/docs/script/functions/conditions/if.md) - условный выбор между двумя значениями.
- [Case](/docs/script/functions/conditions/case.md) - условная подстановка значения.
- [Alt](/docs/script/functions/conditions/alt.md) - первое не-null значение.
- [Null Checks](/docs/script/functions/conditions/null-checks.md) - `IsNull`, `NotNull`.

## Null-семантика

Логические операции и сравнения используют SQL-подобную трехзначную логику: `true`, `false`, `null`.

В `WHERE` значение `null` не проходит фильтр так же, как `false`.
