# План тестирования рабочих компонентов

Цель: фиксировать прямые тесты поведения конкретных объектов. Косвенные проверки через другой компонент не считаются достаточными, если из теста не видно, что именно этот объект обязан работать так.

Не тестируем все подряд. Простые data-only объекты, очевидные one-line методы, `Source`, `Row`, `DataSchema` и прямые тесты конвертеров пока не приоритет, если поведение уже покрывается provider/domain tests.

## Текущее прямое покрытие

| Компонент | Прямые тесты сейчас | Статус |
|---|---:|---|
| `CsvProvider` / `CsvProviderDataReader` | 23 | Основной набор edge cases есть, нужны спорные/политические cases |
| `PostgresProvider` | 12 методов, 55 value cases | Есть базовая матрица типов и DB behavior, нужно добить ошибки/connection/SequentialAccess |
| `DomainDataReader` | 7 | Базовое поведение нормализации и позиции reader покрыто |
| `WhereDomainDataReader` | 9 | Базовый фильтр, null, case-sensitive names, async покрыты |
| `LimitDbDataReader` / `Normalize` | 8 | Базовый limit, порядок с Where, async, negative values покрыты |
| `CollectMeta` / `DataMetaContainer` | 7 + 1 Postgres schema case | Базовая статистика, empty/full/partial/error/async, after Where/Limit покрыты |
| `ExcelProvider` | 0 | Следующий большой provider после CSV/Postgres |
| `ClickHouseProvider` | 0 | После Postgres, идеи можно копировать почти один в один |

Общий прогон после последних изменений: 122 теста ожидаются в suite.

## Текущие решения

- `Row` отдельно пока не тестируем: тонкая обертка над уже нормализованным `DomainDataReader`.
- `Source` отдельно пока не тестируем: текущая роль почти data/container, не основная доменная логика.
- `DataSchema` отдельно пока не приоритет: вернемся, если появится логика merge/alias policy/schema policy.
- `DataValueConverter` и `DataTypeMapper` пока не трогаем прямыми тестами: есть косвенное покрытие через provider/domain tests. Потом решить, оставлять `DataTypeMapper` facade или сводить к одному registry.
- `Select` удален из API и не тестируется до новой реализации.
- DB provider errors должны идти через общий `DbExecutionException`, а не provider-specific exception.
- Meta tests в CSV provider не нужны, если нет подозрения на leaky abstraction. Meta тестируем отдельно через `CollectMeta`.

## CsvProvider

Текущее: 23 прямых теста.

Уже закрыто:

- [x] CSV с числами/датами возвращает все значения строками и типы `DataType.Text`.
- [x] CSV с кастомным delimiter `;` читает quoted values.
- [x] CSV с UTF-8 BOM не добавляет BOM в имя первой колонки.
- [x] CSV в UTF-8 читает Unicode значения.
- [x] CSV в UTF-16 LE с явной `Encoding` читает Unicode значения.
- [x] CSV в UTF-16 BE с явной `Encoding` читает Unicode значения.
- [x] CSV с CRLF line endings читает строки как обычные записи.
- [x] CSV с последней строкой без newline читает последнюю запись.
- [x] CSV с delimiter внутри quoted value возвращает одно значение.
- [x] CSV с header-дубликатами кидает `DuplicateDataFieldNameException`.
- [x] CSV с пустым файлом и `Header=true` кидает `NoHeaderCsvProviderException`.
- [x] CSV только с header возвращает схему и 0 строк.
- [x] CSV с одной пустой quoted ячейкой возвращает `string.Empty`, не `DBNull`.
- [x] CSV с escaped quote внутри quoted value возвращает кавычку в строке.
- [x] CSV с backslash quote не считает backslash CSV escape.
- [x] CSV с quoted newline возвращает одну строку с переносом.
- [x] CSV без header генерирует `A`, `B`.
- [x] CSV без header генерирует имена после `Z`: `AA`, `AB`.
- [x] CSV без header и короткой строкой возвращает `DBNull` в отсутствующей ячейке.
- [x] CSV без header и длинной строкой игнорирует extra values сверх первой строки.
- [x] CSV с короткой строкой относительно header возвращает `DBNull`.
- [x] CSV с длинной строкой относительно header игнорирует extra values.
- [x] CSV с незакрытой кавычкой кидает `MalformedCsvProviderException`.

Точно надо дальше:

- [ ] CSV `Header=false` и пустой файл: зафиксировать текущую реакцию или доменный exception.
- [ ] CSV с whitespace вокруг unquoted value: зафиксировать trim/no-trim.
- [ ] CSV с whitespace вокруг quoted value: зафиксировать trim/no-trim.
- [ ] CSV с пустой строкой между строками: зафиксировать текущее поведение.
- [ ] CSV с quote в середине unquoted value: зафиксировать поведение или доменный exception.
- [ ] CSV с пустым именем header: имя колонки или exception.
- [ ] CSV malformed exception сохраняет inner Sylvan exception.
- [ ] CSV no-header exception сохраняет inner Sylvan exception.

Может быть позже / нужна политика:

- [ ] Legacy encodings типа Windows-1251: решить, добавляем ли `System.Text.Encoding.CodePages` в тесты/пакеты и кто регистрирует `CodePagesEncodingProvider`.
- [ ] CSV с очень длинным значением поля: пока нет политики отказа.
- [ ] CSV с очень длинным именем поля: пока нет политики отказа.
- [ ] CSV reader dispose закрывает underlying reader/source, если можно проверить без хрупкого теста.
- [ ] Длина значения считается в `char`, UTF-16 code unit, byte length target encoding или provider-specific units.

## PostgresProvider

Текущее: 12 методов, 55 value cases.

Уже закрыто:

- [x] SQL expression matrix мапится в ожидаемые `DataType` и canonical CLR values.
- [x] Empty result сохраняет schema.
- [x] Aliases: unquoted lower-case, quoted сохраняют точное имя.
- [x] Multiple rows читаются в порядке результата.
- [x] `.Where` работает поверх Postgres domain reader.
- [x] `GetDataTypeName` сохраняет origin provider type name.
- [x] SQL `NULL` возвращается как `DBNull`.
- [x] `select 1` без alias дает PostgreSQL generated column name.
- [x] Query error оборачивается в `DbExecutionException`.
- [x] Oversized numeric падает в `DataReaderValueException`.
- [x] Duplicate column names кидают duplicate schema exception.
- [x] `numeric(10,2)` отдает precision/scale в meta.

Точно надо дальше:

- [ ] Неверная connection string кидает общий `DbExecutionException`.
- [ ] SQL syntax error кидает общий `DbExecutionException`.
- [ ] Missing table кидает общий `DbExecutionException`.
- [ ] `DbExecutionException` содержит provider kind `postgres`.
- [ ] `DbExecutionException` содержит исходный SQL.
- [ ] `DbExecutionException` сохраняет inner Npgsql exception.
- [ ] Cancellation до открытия соединения не wrapping в `DbExecutionException`.
- [ ] Cancellation во время query не wrapping в `DbExecutionException`.
- [ ] Reader закрывает connection через `CommandBehavior.CloseConnection`.
- [ ] `select 1, 2` без alias с двумя `?column?` кидает duplicate column exception.
- [ ] Duplicate aliases кидают `DuplicateDataFieldNameException`.
- [ ] `numeric` без precision/scale фиксирует null или provider value.
- [ ] `numeric(1000,500)` schema читается.
- [ ] Ошибка чтения одной колонки сохраняет field name и ordinal в `DataReaderValueException`.

Может быть позже / исследование:

- [ ] SequentialAccess: тест/исследование с большим `bytea` или `text`.
- [ ] Проверить, что `DomainDataReader` читает Postgres columns слева направо.
- [ ] Проверить provider-specific memory behavior Npgsql для больших values.

## DomainDataReader

Текущее: 7 прямых тестов.

Уже закрыто:

- [x] `string` сводится к `DataType.Text`, value остается `string`.
- [x] `GetDataTypeName` остается origin provider value.
- [x] Field name lookup case-sensitive.
- [x] Базовые CLR-типы сводятся к canonical values: `long`, `decimal`, `bool`, `DateTime`, `TimeOnly`.
- [x] `DBNull` не смешивается с `string.Empty`.
- [x] `GetValues` копирует текущую строку.
- [x] `GetValue` до `Read()` кидает `InvalidOperationException`.
- [x] `GetValue` после EOF кидает `InvalidOperationException`.
- [x] Unknown CLR type кидает `NotSupportedException` при schema build.

Точно надо дальше:

- [ ] `null` от inner reader нормализуется в `DBNull` через fake reader, который реально возвращает `null`.
- [ ] Значения текущей строки буферизуются и доступны в любом порядке после `Read()`.
- [ ] `GetValues` с массивом меньше количества колонок копирует только доступное.
- [ ] `ReadAsync` повторяет поведение `Read`.
- [ ] Ошибка `Inner.GetValue` оборачивается в `DataReaderValueException`.
- [ ] Ошибка conversion оборачивается в `DataReaderValueException`.
- [ ] `DataReaderValueException` содержит имя поля и ordinal.
- [ ] Duplicate column names падают при построении domain schema.

Может быть позже:

- [ ] Не тестировать простые one-line pass-through методы decorator-а, если там нет доменной логики.

## Pipeline: Normalize, Where, Limit, CollectMeta

Текущее: `Where` 9, `Limit/Normalize` 8, `CollectMeta` 7.

Уже закрыто:

- [x] `Normalize(config Limit=0)` сохраняет schema и возвращает 0 строк.
- [x] `Normalize(config Limit=1)` читает ровно 1 строку.
- [x] `Normalize(config Limit=-1)` кидает понятный exception.
- [x] `Normilize` alias покрыт через `Limit=0`.
- [x] `AsTyped()` legacy shortcut возвращает `DomainDataReader`.
- [x] `.Where(predicate)` сохраняет schema.
- [x] `.Where(predicate)` вызывает predicate при чтении.
- [x] `.Where(predicate)` с exception пробрасывает exception.
- [x] `.Limit(-1)` кидает до создания reader-а.
- [x] `.Limit` сохраняет schema.
- [x] `.Limit` работает через `ReadAsync`.
- [x] `Where -> Limit` применяет limit после фильтра.
- [x] `Normalize Limit -> Where` применяет limit до фильтра.
- [x] `.CollectMeta` после `.Where` собирает filtered rows.
- [x] `.CollectMeta` после `.Limit` собирает limited rows.
- [x] `.CollectMeta` при partial read оставляет `Success=false`.
- [x] `.CollectMeta` при ошибке дальше в pipeline оставляет `Success=false`.
- [x] `.CollectMeta` через `ReadAsync` после полного чтения ставит `Success=true`.

Точно надо дальше:

- [ ] `Normalize(config Limit=null)` возвращает `DomainDataReader`.
- [ ] `AsDomain()` повторяет `Normalize(Limit=null)`.
- [ ] `.Limit(0)` после `.Where` не вызывает predicate.
- [ ] `.Limit(1)` после `.Where` не читает лишние подходящие строки.
- [ ] `CollectMeta -> Where -> Limit` зафиксирован тестом как допустимый, но опасный порядок для смысла meta.
- [ ] Если consumer прочитал все строки, но не вызвал последний `Read()` до false, `Success=false`.

## CollectMeta, DataMetaContainer, DataColumnMeta

Текущее: 7 прямых тестов + 1 Postgres precision/scale.

Уже закрыто:

- [x] Новый container до полного чтения имеет `Success=false`.
- [x] Full read non-empty result ставит `Success=true`.
- [x] Full read empty result ставит `Success=true`.
- [x] Partial read оставляет `Success=false`.
- [x] Exception дальше в pipeline после meta оставляет `Success=false`.
- [x] `UniqueValueCount`, `AllValuesUnique`, `Density`, `Min`, `Max` покрыты базовым mixed dataset.
- [x] `Density` корректна для null/non-null mix.
- [x] `Min/Max` собираются для `Integer` и `Number`.
- [x] `Min/Max` остаются null для non-numeric.
- [x] Meta после `Where` собирает только filtered rows.
- [x] Meta после `Limit` собирает только limited rows.
- [x] `DecimalPrecision` и `DecimalScale` берутся из Postgres column schema.

Точно надо дальше:

- [ ] Новый container имеет `RowCount=0`.
- [ ] После `CollectMeta` до чтения уже есть schema columns.
- [ ] Exception во время inner `Read` оставляет `Success=false`.
- [ ] Exception во время value conversion оставляет `Success=false`.
- [ ] Повторное использование одного container очищает старые columns.
- [ ] `UniqueValueCount` считает `DBNull`.
- [ ] `AllValuesUnique=false` для repeated `DBNull`.
- [ ] `Density=1` без null.
- [ ] `Density=0` когда все значения `DBNull`.
- [ ] `Min/Max` игнорируют `DBNull`.
- [ ] Missing precision/scale остается null.

## ExcelProvider

Текущее: 0.

Точно надо начать после CSV/Postgres:

- [ ] Excel читает sheet по имени.
- [ ] Excel читает sheet с пробелами в имени.
- [ ] Excel читает header row.
- [ ] Excel без header генерирует A/B/C names.
- [ ] Excel пустой файл дает доменный exception.
- [ ] Excel пустой sheet дает понятное поведение.
- [ ] Excel header-only дает schema и 0 rows.
- [ ] Excel пустая ячейка дает `DBNull` или empty string, нужно зафиксировать.
- [ ] Excel text/numeric/date/bool/formula cells фиксируют текущее canonical behavior.
- [ ] Excel rows with missing cells дают `DBNull`.
- [ ] Excel rows with extra cells фиксируют поведение.
- [ ] Excel duplicate headers кидают duplicate schema exception.
- [ ] Excel provider exception нормализует ошибки Sylvan.

Может быть позже:

- [ ] Excel long text cell policy.
- [ ] Excel long field name policy.

## ClickHouseProvider

Текущее: 0.

Пока не начинаем. После Postgres tests переносим идеи почти один в один под ClickHouse:

- [ ] Структура provider tests.
- [ ] ClickHouse type matrix.
- [ ] Общий `DbExecutionException`.
- [ ] Aliases.
- [ ] Duplicate columns.
- [ ] Empty result schema.
- [ ] `GetDataTypeName`.
- [ ] `Where`.
- [ ] `Limit`.
- [ ] `CollectMeta`.
- [ ] Precision/scale, если provider отдает это в schema.

## Долги и политики не сейчас

- [ ] Политика отказа от слишком длинного значения поля на уровне CSV provider.
- [ ] Политика отказа от слишком длинного имени поля на уровне CSV provider.
- [ ] Политика отказа от слишком длинного значения после transform/query pipeline.
- [ ] Политика отказа от слишком длинного имени поля после transform/query pipeline.
- [ ] Где хранить лимиты: global config, provider config или normalize config.
- [ ] Какой exception кидать на превышение лимита длины значения.
- [ ] Какой exception кидать на превышение лимита длины имени поля.
- [ ] Нужно ли meta хранить max string length по наблюдаемым данным.
- [ ] Нужно ли meta хранить max field name length.

## Ближайший порядок

- [x] Обновить DB exception contract на общий `DbExecutionException`.
- [x] Удалить `Select` stub.
- [x] Добить основной CSV provider edge набор.
- [x] Добавить CSV encoding tests для UTF-8 / UTF-16 LE / UTF-16 BE.
- [x] Добить базовые pipeline interaction tests.
- [x] Расширить базовые CollectMeta tests.
- [x] Расширить базовые DomainDataReader tests.
- [ ] Добить оставшиеся спорные CSV behavior tests.
- [ ] Добить Postgres provider behavior tests.
- [ ] Начать Excel provider tests.
- [ ] После Postgres перенести подход на ClickHouse.
