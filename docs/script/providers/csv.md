# Csv

`Csv` читает CSV-файл из `FileStorage`.

## Минимальный пример

```ts
orders:
LOAD *
FROM Csv('orders.csv');
```

## Полный пример с настройками по умолчанию

```ts
orders:
LOAD *
FROM Csv(
  path='orders.csv',
  delimiter=',',
  header=true,
  skipRows=0,
  style='lax',
  encoding='utf-8',
  trimHeaders=false,
  trimValues=false,
  emptyAsNull=false
);
```

`comment` по умолчанию выключен, поэтому в полном примере он не указан. Если нужен комментарий, передайте один символ:

```ts
orders:
LOAD *
FROM Csv('orders.csv', comment='#');
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `path` | `text` | обязателен | Путь к файлу внутри `FileStorage`. Можно передать позиционно: `Csv('orders.csv')`. |
| `delimiter` | `text` | `','` | Один символ разделителя. |
| `header` | `bool` | `true` | Если `true`, первая строка задает имена полей. Если `false`, имена генерируются как `A`, `B`, `C`. |
| `skipRows` | `int` | `0` | Пропускает указанное количество физических строк перед чтением header или data rows. |
| `style` | `text` | `'lax'` | Режим разбора CSV: `standard`, `lax`, `escaped`. |
| `encoding` | `text` | `utf-8` | Имя кодировки .NET. Полный список: [encodings.md](/docs/script/providers/encodings.md). |
| `comment` | `text` | нет | Один символ начала строки комментария. Такие строки пропускаются. |
| `trimHeaders` | `bool` | `false` | Удаляет пробелы вокруг имен колонок из header. |
| `trimValues` | `bool` | `false` | Удаляет пробелы вокруг значений. |
| `emptyAsNull` | `bool` | `false` | Превращает пустые строки в `null`. Если включен `trimValues`, whitespace-only значения тоже становятся `null`. |

## SkipRows

`skipRows` применяется до чтения header.

```text
metadata 1
metadata 2
id,name
1,Alice
```

Скрипт:

```ts
orders:
LOAD *
FROM Csv('orders.csv', skipRows=2, header=true);
```

Результат:

| id | name |
| --- | --- |
| `1` | `Alice` |

## Style

`style` напрямую выбирает режим чтения CSV в Sylvan.

### standard

Строгий CSV-режим. Подходит, когда файл должен быть корректным CSV и лучше получить ошибку, чем молча прочитать неоднозначные данные.

Пример:

```text
value
"abc"tail
```

Результат: ошибка формата CSV, потому что после закрывающей кавычки есть текст.

### lax

Дружелюбный режим для реальных файлов. Некорректно quoted значения не ломают чтение, если Sylvan может однозначно восстановить значение.

Пример:

```text
value
"abc"tail
```

Результат:

| value |
| --- |
| `abctail` |

### escaped

Режим для файлов, где поля считаются неявно quoted, а кавычка работает как escape для delimiter/newline.

Пример:

```text
id,note
1,hello",world
```

Результат:

| id | note |
| --- | --- |
| `1` | `hello,world` |

## EmptyAsNull

Без `trimValues` только реально пустые значения становятся `null`.

```text
id,name
1,
2,""
3,   
```

При `emptyAsNull=true`:

| id | name |
| --- | --- |
| `1` | `null` |
| `2` | `null` |
| `3` | `'   '` |

При `trimValues=true, emptyAsNull=true` третья строка тоже станет `null`.
