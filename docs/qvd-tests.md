# QVD тесты

QVD provider пока beta. Цель этого списка - закрыть функциональные риски формата.

Performance, скорость чтения и память не входят в этот набор тестов. Для них будет отдельный perf-пакет, потому что QVD специально предзагружает symbol tables в память, а rows читает потоково.

## Точно надо

- [x] Bit packing на границах байтов: `BitOffset` не кратен 8, `BitWidth` 1/2/7/8/9/15/16/17.
- [x] Bias варианты: `-1`, `-2`, `0`, положительный bias.
- [x] Null через отрицательный symbol index.
- [x] Колонка с одним уникальным значением и строками: `BitWidth = 0`.
- [x] Пустая symbol table при наличии строк кидает `QvdFormatProviderException`.
- [x] Row с symbol index за пределами symbol table кидает `QvdFormatProviderException`.
- [x] Symbol table truncation: строка без `\0`.
- [x] Symbol table truncation: int token обрезан.
- [x] Symbol table truncation: double token обрезан.
- [x] Row section truncation: header обещает N строк, файл короче.
- [x] Duplicate column names падают на `Normalize()` через `DuplicateDataFieldNameException`.
- [x] Case-sensitive имена колонок: `Name` и `name` доступны отдельно.
- [x] Очень много колонок, например 100+, проверяет schema/ordinal/name lookup.
- [x] Очень длинная строка в symbol table, например 100 KB.
- [x] Unicode field names и values: кириллица, emoji, RTL, control chars.
- [x] Date/time boundary: `1899-12-30`.
- [x] Date/time boundary: leap day.
- [x] Date/time boundary: midnight.
- [x] Date/time boundary: `23:59:59`.
- [ ] Fractional seconds, если QVD fixture может их выразить.
- [x] Dual temporal, где numeric и display string расходятся: фиксируем текущую политику доверять display string.
- [x] Dual non-temporal: numeric prefix игнорируется, display string становится `Text`.
- [x] Empty table с `BitWidth = 0`: schema доступна, `Read()` возвращает `false`.
- [x] Non-seekable stream source кидает `QvdFormatProviderException`.
- [x] Missing file кидает `QvdFileOpenProviderException`.
- [x] Corrupted XML header кидает `QvdFormatProviderException`.
- [x] Corrupted binary layout кидает `QvdFormatProviderException`.

## Желательно

- [x] Token type `1`: pure int.
- [x] Token type `2`: pure double.
- [x] Token type `4`: string.
- [x] Token type `5`: dual int.
- [x] Token type `6`: dual double.
- [ ] NumberFormat регистр: `DATE`, `Date`, `date`.
- [ ] NumberFormat регистр: `TIMESTAMP`, `Timestamp`, `timestamp`.
- [x] Unknown NumberFormat не падает, тип выводится по symbols.
- [ ] Null-only column: schema `Text`, все значения `DBNull`.
- [ ] Mixed int/double column: schema `Number`, int значения возвращаются как `double`.
- [ ] Mixed string/int column: schema `Text`, int значения конвертируются в invariant string.
- [ ] `GetDataTypeName()` сохраняет origin `NumberFormat.Type`.
- [x] `GetFieldType()` до первого `Read()` уже корректный.
- [ ] `GetValues()` с массивом меньше `FieldCount`.
- [ ] `GetValues()` с массивом больше `FieldCount`.
- [ ] `Dispose()` закрывает stream.
- [ ] `Close()` закрывает stream.
- [x] `Normalize().Where()` работает с QVD числом.
- [ ] `Normalize().Where()` работает с QVD датой.
- [ ] `CollectMeta()` на QVD считает unique count.
- [ ] `CollectMeta()` на QVD считает density.
- [ ] `CollectMeta()` на QVD считает min/max для чисел.

## Можно позже

- [ ] Реальные Qlik Sense generated files с Lineage/CreatorDoc.
- [ ] Несколько Qlik версий, если будут образцы.
- [ ] Fuzz/property tests для bit-stuffed row index table.
- [ ] Binary/blob-like значения, если найдется реальный способ получить их в QVD.

## Не входит сюда

- [ ] Большой файл 1M+ строк.
- [ ] Скорость чтения row section.
- [ ] Потребление памяти symbol tables.
- [ ] Сравнение batch size.
- [ ] Проверка, что `.Limit()` физически не читает весь row section.
