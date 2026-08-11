# LOAD stabilization

Оставшиеся критерии стабилизации LOAD для MVP. Выполненные пункты здесь не храним, чтобы документ оставался рабочим списком.

## Error Contract

- [ ] Добавить `StatementAlias` в `LoadScriptException` и playground response.
- [ ] Любая ошибка LOAD должна быть отнесена к конкретному stage, а не выходить наружу raw `InvalidOperationException`, `IndexOutOfRangeException`, `NullReferenceException` или driver exception без контекста.
- [ ] Playground должен отображать LOAD-domain ошибку как: message, statement index, statement alias, stage, errors, details/stack trace в collapsed block.

## ProviderResolution

- [ ] Если source option отсутствует полностью, ошибка должна указывать на `FROM`/source call span.

## SourceOpen

- [ ] Missing file должен оборачиваться как stage `SourceOpen`, а не выходить raw provider/file exception.
- [ ] Недоступная DB/source table должна оборачиваться как stage `SourceOpen`.
- [ ] Ошибка user SQL внутри DB source должна указывать stage `SourceOpen` и span SQL block, если SQL задан отдельной инструкцией.
- [ ] Ошибка открытия source должна сохранять исходную причину в inner exception, но пользовательское сообщение должно быть доменным.

## TempTableWrite

- [ ] Provider schema с `0` полей должен останавливаться доменной ошибкой до `CREATE TABLE`.
- [ ] ClickHouse write/create ошибка при temp table должна оборачиваться как stage `TempTableWrite`.
- [ ] Ошибка сериализации provider values в ClickHouse должна показывать, что проблема возникла на записи temp table, а не в source SQL.
- [ ] Temp table cleanup выполняется в `finally`, если temp table была создана.

## QueryResolution

- [ ] Error message для нескольких ошибок не должен слипаться в одну нечитаемую строку.
- [ ] Если возможно, resolver должен возвращать все найденные ошибки за один проход, а не только первую.

## FinalTableWrite

- [ ] ClickHouse query/write ошибка при final table должна оборачиваться как stage `FinalTableWrite`.
- [ ] Если final table materialization failed, созданная final table должна удаляться.
- [ ] Если final table уже успешно принята как loaded table, cleanup не должен удалять ее как failed table.

## Schema Invariants

- [ ] File providers должны разъединять одинаковые имена колонок до query mapping: `Field`, `Field (2)`, `Field (3)`.
- [ ] Provider schema должна быть валидирована перед temp/final table write.
- [ ] ClickHouse `Variant(...)` должен иметь явную политику: запрет, query-level cast или отдельный DataType.

## Integration Coverage

- [ ] Добавить script integration test для Excel либо явно вынести из MVP.
- [ ] Добавить script integration test для QVD либо явно вынести из MVP.

## Check vs Execute

- [ ] Спроектировать `Check`, который запускается часто при редактировании script и не делает полноценный load.
- [ ] Не дублировать отдельный pipeline, чтобы ошибки `Check` и `Execute` не разошлись.
- [ ] Сделать check-режим неинтрузивным для автора statement executor-а: один набор шагов, разные capabilities/side effects.
