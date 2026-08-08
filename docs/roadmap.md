## Роадмапа

### Провадеры

- [ ] Базы данных
  - [x] Postgres
  - [x] ClickHouse
  - [x] Microsoft SQL Server
  - [x] Oracle
  - [x] Apache Hive
  - [ ] ODBC
- [x]  Файлы
  - [x] Excel
  - [x] CSV
  - [x] QVD
  - [x] XML
  - [x] JSON
- [ ] Источники файлов
  - [x] Файловая система
  - [ ] Remote HTTP\[S]

### Скрипт

- [x] LOAD
- [x] Поддержка нескольких операций в скрипте
- [x] Базовые Трансформации в LOAD
  - [x] WHERE
  - [x] ORDER BY
    - [ ] Проблема с LOG
    - [ ] Проблема с Order By оптимизацией
  - [x] GROUP BY и Аггрегации
  - [x] LIMIT OFFSET
- [ ] LOAD: стабилизация MVP
  - [x] `ProviderResolution`: покрыты unknown marker/source, конфликт markers, неверный тип option, отсутствующая обязательная option.
  - [x] `ProviderResolution` и `QueryResolution` возвращают `LangError[]`; где позиция известна, у ошибки есть `LangSpan`.
  - [x] `QueryResolution`: покрыты duplicate alias, non-boolean `WHERE`, invalid aggregate/group by, `LIMIT 0`; unknown fields покрыты на query layer.
  - [x] Есть base `LoadScriptException` wrapper для `LoadScriptStageException` со `StatementIndex`, `Stage`, `Span`, `Errors`.
  - [x] Есть mixed-source script test: несколько `LOAD` из разных source в одном script, final tables доступны, temp tables очищены.
  - [x] Интеграционные script tests есть для CSV, JSON, XML, Postgres, ClickHouse; SQL Server/Oracle вынесены в heavy/rare suite.
  - [ ] Добавить `StatementAlias` в `LoadScriptException`/playground response.
  - [ ] `SourceOpen`: missing file и недоступная DB/source table оборачиваются stage `SourceOpen`, а не выходят raw provider/driver exception.
  - [ ] `TempTableWrite`: provider schema с `0` полей останавливается доменной ошибкой до `CREATE TABLE`.
  - [ ] `TempTableWrite`: ошибка ClickHouse write оборачивается stage `TempTableWrite`.
  - [ ] `FinalTableWrite`: ошибка ClickHouse query/write оборачивается stage `FinalTableWrite`; temp/final cleanup сохраняет уже принятое поведение.
  - [ ] Файловые providers разъединяют одинаковые имена колонок до query mapping: `Field`, `Field (2)`, `Field (3)`.
  - [ ] Добавить недостающие script integration tests для Excel и QVD либо явно вынести их из MVP.
- [ ] LOAD: оптимизации final table
  - [ ] Type narrowing: integer min/max, decimal precision/scale.
  - [ ] Nullability: `Nullable(T)` только если schema/meta говорит, что null возможен или это неизвестно.
  - [ ] `LowCardinality(String)` по известной низкой cardinality.
  - [ ] Final tables на `MergeTree`; temp/staging оставить на `Log`.
  - [ ] `ORDER BY` не угадывать агрессивно без query pattern/user setting.
- [ ] LOAD: Check vs Execute
  - [ ] Спроектировать `Check`, который часто запускается при редактировании script и не делает полноценный load.
  - [ ] Не дублировать отдельный pipeline, чтобы ошибки `Check` и `Execute` не разошлись.
  - [ ] Сделать check-режим неинтрузивным для автора statement executor-а: один набор шагов, разные capabilities/side effects.
- [ ]  LOAD FROM LOAD
- [ ]  DROP TABLE
- [ ] LINK - `shadow table`
- [ ] LIB CONNECT
- [ ] CALENDAR (Федя)
- [ ] LOAD FROM DATASET (возможно только уже внутри PIX?)
- [ ] INDEX?
- [x] *Трейсы процесса загрузки \*OpenTelemetry (LoadScript ActivitySource)*
  - [x] `Script.Statement`
  - [x] `LoadStatement.Prepare`
  - [x] `LoadStatement.TempTableWrite`
  - [x] `LoadStatement.QueryBuild`
  - [x] `LoadStatement.FinalTableWrite`
  - [x] Sanitized telemetry tag для `load.source`

### Типы
- [ ] Соединить ReData.DataType и Loader.DataType
- [ ] Разделение на Decimal и Float вместо numeric

### Функции

- [ ] Time('12:00')
- [ ] Text(Time('12:00'))
- [x] Num(text, ',') или Dec|Float
- [x] Date(text, 'yyyy-mm-dd') Joda паттерн
- [x] Text(date, 'yyyy-mm-dd') Joda паттерн
- [x] Subfield(text, delimeter, index) Без разворачивания
- [x] Json функции JsonGet JsonGet<Type> JsonHas JsonType JsonLength
- [x] Fractile
- [ ] Сделать названия case insensitive
- [ ] Переименовать что бы были больше похожи на Qlik
- [ ] *Qol функции (необязательно)*
  - [x] ExcludeChars KeepChars
- [ ] Убрать некоторые редкие функции что бы не обещать их поддержку (FutureValue)
