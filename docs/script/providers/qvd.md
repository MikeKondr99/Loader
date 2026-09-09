# Qvd

`Qvd` читает QVD-файл из `FileStorage`.

## Минимальный пример

```ts
orders:
LOAD *
FROM Qvd('orders.qvd');
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `path` | `text` | обязателен | Путь к QVD-файлу внутри `FileStorage`. Можно передать позиционно: `Qvd('orders.qvd')`. |

## Особенности

Provider читает schema и значения из QVD metadata и symbol table.

SQL после `FROM Qvd(...)` не поддерживается.
