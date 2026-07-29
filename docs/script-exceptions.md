# Ошибки script

Все ошибки выполнения script должны подниматься наружу как `LoadScriptException`.
Он добавляет номер statement, тип statement, этап выполнения и, если доступно, `LangSpan`.

## Этапы

| Stage | Exception | Span |
| --- | --- | --- |
| `ProviderResolution` | Ошибка определения provider-а и source по `FROM (...)`: неизвестный provider, конфликт marker-ов, неверный тип option, отсутствующая обязательная option. | Да: marker/option ошибки указывают на весь marker или `option=value`; отсутствующие обязательные options указывают на `FROM`. |
| `SourceOpen` | Ошибка открытия файла, подключения или исходной таблицы. | Обычно нет: ошибка приходит из provider/runtime, но позже можно привязать к `FROM`. |
| `TempTableWrite` | Ошибка записи исходного reader-а во временную ClickHouse-таблицу. | Обычно нет: ошибка уже на данных или DWH. |
| `QueryResolution` | Ошибка разрешения LOAD expressions в `Query`: неизвестное поле, неподходящие типы, дубли alias. | Да, если ошибка относится к expression или alias. |
| `QueryCompilation` | Ошибка компиляции resolved query в SQL. | Обычно нет; resolved expression span можно добавить позже, если compiler начнет возвращать доменную ошибку. |
| `FinalTableWrite` | Ошибка материализации финальной таблицы из temp table. | Обычно нет: ошибка на ClickHouse SQL/runtime. |

Реализованные срезы: `ProviderResolutionException`, `QueryResolutionException`.
Оба могут возвращать массив `LangError`, если за один проход найдено несколько ошибок.
