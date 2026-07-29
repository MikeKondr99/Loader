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
- [ ]  LOAD FROM LOAD
- [ ]  DROP TABLE
- [ ] LINK - `shadow table`
- [ ] LIB CONNECT
- [x] CALENDAR
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
