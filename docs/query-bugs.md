# Query test gaps

- [x] FAIL: `ORDER BY total`, где `total` это `SELECT ... AS total`, сейчас ищется только среди source fields, поэтому сортировка по output alias не работает.
- [x] FAIL: `LIMIT 0` сейчас не попадает в SQL, потому что compiler пишет `LIMIT` только для значений больше нуля.
- [x] FAIL: `SELECT city, SUM(amount)` без `GROUP BY city` доходит до ClickHouse, а должен отклоняться нашим resolver-ом как смешивание aggregate и non-grouped field.
- [x] FAIL: `GROUP BY SUM(amount)` доходит до ClickHouse, а должен отклоняться resolver-ом как группировка по aggregate expression.
- [x] FAIL: `flag = true` и `flag = false` не резолвятся, потому что нет overload сравнения для `Boolean`.
- [x] FAIL: `COUNT(flag)` для boolean не резолвится, потому что `COUNT(value)` сейчас не зарегистрирован для `Boolean`.
