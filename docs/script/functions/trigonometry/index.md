# Trigonometry Functions

Trigonometry functions работают с углами и числами типа `num`.

Обычные тригонометрические функции принимают угол в радианах. Для перевода между градусами и радианами используются `Rad` и `Deg`.

## Функции

- [Sin](/docs/script/functions/trigonometry/sin.md) - синус угла в радианах.
- [Cos](/docs/script/functions/trigonometry/cos.md) - косинус угла в радианах.
- [Tan](/docs/script/functions/trigonometry/tan.md) - тангенс угла в радианах.
- [Asin](/docs/script/functions/trigonometry/asin.md) - арксинус.
- [Acos](/docs/script/functions/trigonometry/acos.md) - арккосинус.
- [Atan](/docs/script/functions/trigonometry/atan.md) - арктангенс.
- [Atan2](/docs/script/functions/trigonometry/atan2.md) - арктангенс `y / x` с учетом квадранта.
- [Rad](/docs/script/functions/trigonometry/rad.md) - перевод градусов в радианы.
- [Deg](/docs/script/functions/trigonometry/deg.md) - перевод радиан в градусы.
- [Sinh](/docs/script/functions/trigonometry/sinh.md) - гиперболический синус.
- [Cosh](/docs/script/functions/trigonometry/cosh.md) - гиперболический косинус.
- [Tanh](/docs/script/functions/trigonometry/tanh.md) - гиперболический тангенс.
- [Asinh](/docs/script/functions/trigonometry/asinh.md) - гиперболический арксинус.
- [Acosh](/docs/script/functions/trigonometry/acosh.md) - гиперболический арккосинус.
- [Atanh](/docs/script/functions/trigonometry/atanh.md) - гиперболический арктангенс.

## Общая семантика

Все функции возвращают `num`.

Если входной аргумент равен `null`, результат тоже `null`.

Для функций с ограниченной областью определения невалидный вход возвращает `null`, а не ошибку. Это касается `Asin`, `Acos`, `Acosh` и `Atanh`.
