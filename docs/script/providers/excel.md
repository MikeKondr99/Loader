# Excel

`Excel` читает workbook из `FileStorage` через Sylvan.Data.Excel.

## Минимальный пример

```ts
orders:
LOAD *
FROM Excel('orders.xlsx');
```

## Полный пример с настройками по умолчанию

```ts
orders:
LOAD *
FROM Excel(
  path='orders.xlsx',
  header=true
);
```

Если нужно читать конкретный лист или прямоугольную область:

```ts
orders:
LOAD *
FROM Excel(
  path='orders.xlsx',
  sheet='Продажи',
  range='B3:F200',
  header=true
);
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `path` | `text` | обязателен | Путь к файлу внутри `FileStorage`. Можно передать позиционно: `Excel('orders.xlsx')`. |
| `sheet` | `text` | первый доступный лист | Имя листа workbook-а. |
| `header` | `bool` | `true` | Если `true`, первая строка источника задаёт имена полей. Если `false`, имена генерируются как `A`, `B`, `C`. |
| `range` | `text` | нет | A1-диапазон вида `B3:F200`, `B3:F` или `B:F`. Ограничивает чтение строк и колонок. |

## Форматы

Provider использует auto-detect Sylvan по имени файла.

Поддерживаемые форматы:

| Extension | Формат |
| --- | --- |
| `.xlsx` | Excel Open XML workbook |
| `.xlsm` | Excel Open XML macro-enabled workbook |
| `.xlsb` | Excel binary workbook |
| `.xls` | Excel BIFF workbook |

## Range

`range` превращает выбранный прямоугольник в отдельную таблицу.

```ts
orders:
LOAD *
FROM Excel('orders.xlsx', sheet='Продажи', range='B3:D5');
```

Если `header=true`, строка `B3:D3` становится header, а данные читаются из `B4:D5`.

Если `header=false`, строка `B3:D3` уже считается данными, а поля называются по физическим Excel-колонкам `B`, `C`, `D`:

```ts
orders:
LOAD
  B AS order_id,
  C AS city,
  D AS amount
FROM Excel('orders.xlsx', sheet='Продажи', range='B3:D5', header=false);
```

Если `header=true`, но в header-строке внутри range есть пустая ячейка, для неё также используется физическое имя Excel-колонки.

Named ranges сейчас не поддерживаются. `range='SalesRange'` и `range='Sheet1!A1:B2'` считаются ошибкой; имя листа указывается отдельно через `sheet`.

Можно не указывать конечную строку:

```ts
orders:
LOAD *
FROM Excel('orders.xlsx', sheet='Продажи', range='B4:H');
```

В этом случае Loader начинает с `B4:H4` и читает до конца данных листа. Если не указана начальная строка, чтение начинается с первой строки:

```ts
orders:
LOAD *
FROM Excel('orders.xlsx', sheet='Продажи', range='B:H');
```

### Производительность range

`range` применяется как streaming projection поверх Excel reader-а, а не как random access seek.

Это значит:

- До начала диапазона Loader двигает reader через `Read()` и смотрит только на физический номер строки Excel.
- Значения ячеек до `StartRow` не читаются через `GetValue` / `GetString`.
- Стоимость чтения глубокого диапазона всё равно растёт от объёма данных перед ним в листе.
- `range='B1000000:H1000010'` на плотном листе может быть дорогим, потому что Excel reader остаётся forward-only.
- На sparse `.xlsx` / `.xlsm` пустые физически отсутствующие строки дешевле, но это всё равно не мгновенный seek.

Практический смысл `range`: выбрать табличную область внутри листа и не тащить лишние строки/колонки в результат. Это не механизм ускоренного прыжка в конец очень большого Excel-листа.

### Особенности Excel

`range` использует Excel coordinates. При обычном script-вызове hidden rows читаются как часть диапазона, чтобы `B4:H10` означал именно строки Excel `4..10`.

Формулы не вычисляются заново. Sylvan.Data.Excel читает сохранённый cached result из workbook-а.

Merged cells не поддерживаются как визуальная Excel-операция. Loader не разворачивает значение на весь merged range: значение обычно есть только в верхней левой ячейке, остальные ячейки внутри merge читаются как пустые.
