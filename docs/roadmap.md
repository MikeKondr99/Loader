## Роадмапа

### Провадеры

- [ ] Базы данных
  - [x] Postgres
  - [x] ClickHouse
  - [x] Microsoft SQL Server
  - [x] Oracle
  - [ ] Apache Hive
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
- [ ] Поддержка нескольких операций в скрипте
- [ ] Базовые Трансформации в LOAD
  - [ ] WHERE
  - [ ] ORDER BY
  - [ ] GROUP BY и Аггрегации
  - [ ] LIMIT OFFSET
- [ ]  LOAD FROM LOAD
- [ ]  DROP TABLE
- [ ] LINK - `shadow table`
- [ ] LIB CONNECT
- [ ] CALENDAR
- [ ] LOAD FROM DATASET (возможно только уже внутри PIX?)
- [ ] INDEX?
- [ ] *Трейсы процесса загрузки \*OpenTelemetry (не обязательно но круто для дебага)*

### Типы
- [ ] Соединить ReData.DataType и Loader.DataType
- [ ] Разделение на Decimal и Float вместо numeric

### Функции

- [ ] Time('12:00')
- [ ] Text(Time('12:00'))
- [ ] Num(text, ',') или Dec|Float
- [ ] Num(text, ',', ' ') или Dec|Float
- [ ] Date(text, 'yyyy-mm-dd') Joda паттерн
- [ ] Subfield(text, delimeter, index)
- [ ] Json функции
- [ ] Сделать названия case insensitive
- [ ] Переименовать что бы были больше похожи на Qlik
- [ ] *Qol функции (необязательно)*
- [ ] Убрать некоторые редкие функции что бы не обещать их поддержку (FutureValue)
