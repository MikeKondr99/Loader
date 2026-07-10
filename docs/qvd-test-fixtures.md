# QVD test fixtures

Нужны маленькие `.qvd` файлы для provider-level тестов.

## 1. basic_text.qvd

- Колонки: `id`, `name`, `city`
- Все значения текстовые
- 2-3 строки
- Проверяем: схема, имена колонок, чтение строк, порядок строк

## 2. primitive_types.qvd

- Колонки: `text_value`, `int_value`, `num_value`, `bool_text`
- Значения:
  - строка
  - целое число
  - дробное число
  - `true` / `false` как текст, если QVD не хранит bool отдельно
- Проверяем: фактические типы symbols и итоговую нормализацию

## 3. date_time.qvd

- Колонки: `date_value`, `time_value`, `timestamp_value`
- Значения:
  - дата
  - время
  - timestamp
- Проверяем: QVD `NumberFormat.Type = DATE/TIME/TIMESTAMP`

## 4. nulls.qvd

- Колонки: `id`, `optional_text`, `optional_num`
- В нескольких строках должны быть null/пустые значения
- Проверяем: `DBNull` в reader

## 5. mixed_dual_values.qvd

- Колонка, где есть и числовое, и строковое представление
- Например: `code` со значениями `001`, `002`, `A03`
- Проверяем: поле не должно ломать схему; если типы смешаны, provider должен уходить в `Text`

## 6. high_cardinality.qvd

- 1000+ строк
- Колонка `id` с уникальными значениями
- Колонка `category` с малым числом повторов
- Проверяем: чтение большого количества rows и что symbol tables предзагружаются, а rows читаются последовательно

## 7. empty_table.qvd

- Есть схема, но 0 строк
- Проверяем: schema доступна, rows пустые

## 8. special_names.qvd

- Колонки с пробелами, кириллицей, спецсимволами, разным регистром
- Например: `Order ID`, `Город`, `amount.value`, `Name`
- Проверяем: имена колонок сохраняются как в QVD

## 9. binary_or_unsupported.qvd

- Если возможно создать binary/blob-like поле
- Проверяем: пока возвращаем `DBNull` или явно documented unsupported behavior
- Если такой QVD сложно сделать, можно отложить

## 10. corrupted_layout.qvd

- Намеренно поврежденный QVD
- Например: обрезанный файл или неверный binary section
- Проверяем: provider exception на поврежденный формат
- Можно сделать вручную из любого маленького QVD путем truncation

