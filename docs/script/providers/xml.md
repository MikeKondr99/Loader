# Xml

`Xml` читает XML-файл из `FileStorage` как таблицу.

Строкой таблицы считается каждый XML-node с именем из параметра `table`.

## Минимальный пример

```ts
people:
LOAD *
FROM Xml(path='people.xml', table='person');
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `path` | `text` | обязателен | Путь к файлу внутри `FileStorage`. Можно передать позиционно: `Xml('people.xml', table='person')`. |
| `table` | `text` | обязателен | Имя XML-node, который считается одной записью таблицы. |

## Пример XML

```xml
<root>
  <person>
    <id>1</id>
    <name>Alice</name>
  </person>
  <person>
    <id>2</id>
    <name>Bob</name>
  </person>
</root>
```

Скрипт:

```ts
people:
LOAD
  id.Int() AS id,
  name
FROM Xml('people.xml', table='person');
```

## Особенности

`Xml` сначала анализирует схему файла, а потом открывает reader для чтения данных.

SQL после `FROM Xml(...)` не поддерживается.
