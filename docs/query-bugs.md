# Query test gaps

- [x] FAIL: `ORDER BY total`, где `total` это `SELECT ... AS total`, сейчас ищется только среди source fields, поэтому сортировка по output alias не работает.
- [x] FAIL: `flag = true` и `flag = false` не резолвятся, потому что нет overload сравнения для `Boolean`.
- [x] FAIL: `COUNT(flag)` для boolean не резолвится, потому что `COUNT(value)` сейчас не зарегистрирован для `Boolean`.
