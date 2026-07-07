# Система типов

## Нормализованные типы

Первый набор типов специально маленький:

- `Text`
- `Integer`
- `Number`
- `DateTime`
- `Date`
- `Time`
- `Boolean`

В коде это `Loader.Core.Data.DataType`.

## Важное ограничение

Файловые источники не умеют надежно отдавать типы до чтения значений.

Примеры:

- CSV по сути содержит только текст.
- В Excel тип может отличаться от клетки к клетке.
- JSON/XML могут иметь форму, но не гарантируют стабильную табличную схему без явного описания.

Поэтому есть три разных понятия схемы:

- Declared schema: пользователь явно описал поля и типы.
- Source schema: провайдер или БД отдали метаинформацию до чтения строк.
- Observed schema: типы выведены по фактически прочитанным значениям.

## Текущее правило

`TypedDbDataReader` строится через `reader.AsTyped()` и сам выводит нормализованную схему из `DbDataReader`.

Для неизвестных CLR-типов используется безопасное сведение к `Text`, поэтому выше по pipeline они видны как `string`.

```mermaid
flowchart TD
    SourceReader[Any DbDataReader] --> AsTyped[reader.AsTyped()]
    AsTyped --> SourceSchema[Schema from DbDataReader]
    SourceSchema --> Normalize[Normalize CLR types]
    Normalize --> TypedReader[TypedDbDataReader]
```

## Позже

Явная схема должна быть близка к Qlik load expressions:

```text
#date(created_at) as created_at
```

Возможная C#-форма:

```csharp
new DataSchema
{
    Fields =
    [
        new DataField { Ordinal = 0, Name = "created_at", DataType = DataType.Date }
    ]
}
```

Финальные правила кастинга и inference пока намеренно не реализованы.
