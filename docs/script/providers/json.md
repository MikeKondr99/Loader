# Json

`Json` читает JSON-файл из `FileStorage` как таблицу.

Файл должен содержать массив записей в корне документа или по пути `root`.

## Минимальный пример

```ts
orders:
LOAD *
FROM Json('orders.json');
```

## Полный пример с настройками по умолчанию

```ts
orders:
LOAD *
FROM Json(
  path='orders.json',
  root='items',
  textRow=false
);
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `path` | `text` | обязателен | Путь к файлу внутри `FileStorage`. Можно передать позиционно: `Json('orders.json')`. |
| `root` | `text` | нет | Dot-path до массива записей внутри JSON. Если не задан, таблицей считается root-массив документа. |
| `textRow` | `bool` | `false` | Если `true`, не анализирует поля записи и читает каждую запись целиком как JSON-строку в колонку `row`. |

## Root

`root` нужен, когда массив записей находится внутри объекта.

```json
{
  "response": {
    "items": [
      { "id": 1, "city": "Moscow" },
      { "id": 2, "city": "Berlin" }
    ]
  }
}
```

Скрипт:

```ts
orders:
LOAD *
FROM Json('orders.json', root='response.items');
```

Можно указывать индекс массива как сегмент пути:

```ts
orders:
LOAD *
FROM Json('orders.json', root='tables.0.data');
```

## TextRow

`textRow=true` полезен, когда схема нестабильная или не хочется запускать анализ полей.

В этом режиме provider создает одну колонку `row`, а каждую запись возвращает целиком как текст:

```ts
raw_orders:
TEMP LOAD *
FROM Json('orders.json', textRow=true);
```

Дальше пользователь может разобрать нужные поля явно:

```ts
orders:
LOAD
  row.JsonGetText('$.user.name') AS name,
  row.JsonGetText('$.city') AS city
FROM raw_orders;
```
