# JSON Functions

JSON functions читают значения из JSON-текста.

## Функции

- [JsonGet](/docs/script/functions/json/json-get.md) - получить raw JSON fragment.
- [JsonGetText](/docs/script/functions/json/json-get-text.md) - получить JSON scalar как `text`.
- [JsonGetInt](/docs/script/functions/json/json-get-int.md) - получить JSON scalar как `int`.
- [JsonGetNum](/docs/script/functions/json/json-get-num.md) - получить JSON scalar как `num`.
- [JsonGetBool](/docs/script/functions/json/json-get-bool.md) - получить JSON scalar как `bool`.
- [JsonHas](/docs/script/functions/json/json-has.md) - проверить существование значения по пути.
- [JsonType](/docs/script/functions/json/json-type.md) - получить тип JSON-значения.
- [JsonLength](/docs/script/functions/json/json-length.md) - получить длину JSON array/object.

## JSONPath

Путь задается ClickHouse JSONPath и должен быть константной строкой.

Поддерживаемые примеры путей:

| Path | Meaning |
| --- | --- |
| `$` | root JSON |
| `$.user.name` | property внутри object |
| `$.items[0]` | элемент array по индексу |
| `$.items[*].id` | wildcard по array |

Если путь не начинается с `$`, ошибка сейчас доходит до ClickHouse.

TODO: валидировать JSONPath на уровне QueryResolution и отдавать доменную ошибку со span.
