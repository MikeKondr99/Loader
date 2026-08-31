# SubField

`SubField` возвращает часть строки после разделения по delimiter.

## SubField(value, delimiter, fieldNo)

`SubField(value, delimiter, fieldNo)` делит строку по `delimiter` и возвращает часть с номером `fieldNo`.

Положительный `fieldNo` считается слева, начиная с `1`.

Отрицательный `fieldNo` считается справа: `-1` означает последнюю часть.

Если `fieldNo = 0`, часть не найдена и результат `null`.

Если исходная строка пустая, результат `null`.

Если `delimiter` пустой, строка делится на отдельные символы.

Если любой аргумент равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `SubField('abc;cde;efg', ';', 1)` | `'abc'` |
| `SubField('abc;cde;efg', ';', 2)` | `'cde'` |
| `SubField('abc;cde;efg', ';', 3)` | `'efg'` |
| `SubField('abc;cde;efg', ';', -1)` | `'efg'` |
| `SubField('abc;cde;efg', ';', -2)` | `'cde'` |
| `SubField('abc;cde;efg', ';', 0)` | `null` |
| `SubField('abc;cde;efg', ';', 4)` | `null` |
| `SubField('', ';', 1)` | `null` |
| `SubField(';', ';', 1)` | `''` |
| `SubField(';', ';', 2)` | `''` |
| `SubField(';abc;;def;', ';', 3)` | `''` |
| `SubField('a--b--c', '--', 2)` | `'b'` |
| `SubField('привет|мир|😀', '|', 2)` | `'мир'` |
| `SubField('привет|мир|😀', '|', -1)` | `'😀'` |
| `SubField('abc', '', 1)` | `'a'` |
| `SubField('abc', '', 3)` | `'c'` |
| `SubField('abc', '', 0)` | `null` |
| `SubField('abc', '', 4)` | `null` |
| `SubField(null, ';', 1)` | `null` |
| `SubField('abc', null, 1)` | `null` |
| `SubField('abc', ';', null)` | `null` |
| `'abc;cde;efg'.SubField(';', 2)` | `'cde'` |
| `Type(SubField('abc;cde;efg', ';', 2))` | `'text'` |
