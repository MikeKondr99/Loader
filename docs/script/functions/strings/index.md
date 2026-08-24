# String Functions

String functions работают с доменным типом `text`.

## Функции

- [Concat +](/docs/script/functions/strings/concat.md) - склеивание строк через оператор `+`.
- [Upper](/docs/script/functions/strings/upper.md) - перевод латинского ASCII-текста в верхний регистр.
- [Lower](/docs/script/functions/strings/lower.md) - перевод латинского ASCII-текста в нижний регистр.
- [Trim / TrimLeft / TrimRight](/docs/script/functions/strings/trim.md) - удаление обычных пробелов по краям строки.
- [PadLeft / PadRight](/docs/script/functions/strings/pad.md) - дополнение или обрезка строки.
- [Substring](/docs/script/functions/strings/substring.md) - получение части строки.
- [Reverse](/docs/script/functions/strings/reverse.md) - разворот строки.
- [EmptyIsNull](/docs/script/functions/strings/empty-is-null.md) - замена пустой строки на `null`.
- [Replace](/docs/script/functions/strings/replace.md) - замена точных текстовых вхождений.
- [Repeat](/docs/script/functions/strings/repeat.md) - повторение строки.
- [ExcludeChars](/docs/script/functions/strings/exclude-chars.md) - удаление перечисленных символов.
- [KeepChars](/docs/script/functions/strings/keep-chars.md) - сохранение только перечисленных символов.
- [SubField](/docs/script/functions/strings/subfield.md) - получение части строки по delimiter.
- [Index](/docs/script/functions/strings/index-position.md) - первое вхождение подстроки.
- [LastIndex](/docs/script/functions/strings/last-index.md) - последнее вхождение подстроки.
- [Len](/docs/script/functions/strings/len.md) - длина строки.
- [Contains](/docs/script/functions/strings/contains.md) - проверка вхождения подстроки.
- [StartsWith](/docs/script/functions/strings/starts-with.md) - проверка начала строки.
- [EndsWith](/docs/script/functions/strings/ends-with.md) - проверка конца строки.
- [Chr](/docs/script/functions/strings/chr.md) - символ по Unicode-коду.

## Общая семантика

Строковые функции работают с Unicode-текстом. Для функций, где важна длина или позиция символа, ожидается поведение по символам, а не по байтам.

Если входной text равен `null`, большинство string functions возвращает `null`. Точное поведение описывается на странице конкретной функции.
