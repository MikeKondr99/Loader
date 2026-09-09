# Connect: Папка

Подключение `Папка` читает файл внутри подключенной папки.

В скрипте нужно указать `path`. По расширению файла `Connect` выбирает обычный файловый провайдер: `Csv`, `Json`, `Qvd`, `Excel` или `Xml`.

## Пример

```ts
orders:
LOAD *
FROM Connect('shared', path='orders.csv');
```

Параметры выбранного файлового провайдера передаются прямо в `Connect`:

```ts
orders:
LOAD *
FROM Connect(
  'shared',
  path='orders.csv',
  delimiter=';',
  header=true,
  emptyAsNull=true
);
```

Этот пример эквивалентен чтению `Csv`, но файл берется не из основного `FileStorage` скрипта, а из папки подключения `shared`.

## Настройка

Пример для Playground:

```json
{
  "Name": "shared",
  "Type": "Folder",
  "RootPath": "shared"
}
```

`Type` также может быть `FileStorage`, но в UI и ошибках это отображается как `Папка`.

## Расширения

| Расширение | Провайдер |
| --- | --- |
| `.csv` | `Csv` |
| `.json` | `Json` |
| `.jsonl` | `Json` |
| `.ndjson` | `Json` |
| `.qvd` | `Qvd` |
| `.xlsx` | `Excel` |
| `.xls` | `Excel` |
| `.xml` | `Xml` |

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `name` | `text` | обязателен | Имя подключения. Можно передать позиционно: `Connect('shared', path='orders.csv')`. |
| `path` | `text` | обязателен | Путь к файлу внутри подключенной папки. |

Остальные параметры зависят от расширения файла и передаются выбранному провайдеру.

Примеры:

```ts
FROM Connect('shared', path='orders.csv', delimiter=';', header=true)
FROM Connect('shared', path='orders.json', root='response.items')
FROM Connect('shared', path='orders.json', textRow=true)
FROM Connect('shared', path='orders.xlsx', sheet='Orders', range='A1:D100')
FROM Connect('shared', path='people.xml', table='person')
FROM Connect('shared', path='orders.qvd')
```

## Ошибки

`SQL` после `FROM Connect(...)` запрещен:

```text
Подключение Папка 'shared' не поддерживает SQL после FROM.
```

Если не указан `path`:

```text
Подключение Папка 'shared' требует параметр path='relative/path'.
```

Если расширение не поддерживается:

```text
Подключение Папка 'shared' не поддерживает расширение '.bin'. Используйте csv, json, jsonl, ndjson, qvd, xlsx, xls или xml.
```

После выбора провайдера возможны его собственные ошибки, например ошибки CSV header, JSON root, Excel sheet или XML table.
