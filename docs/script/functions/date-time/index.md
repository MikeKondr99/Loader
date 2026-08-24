# Date/Time Functions

Date/time functions работают с доменными типами `date` и `time`.

`date` в текущем query layer технически представлен как date-time значение: дата плюс время. Если время не указано, оно равно `00:00:00`.

`time` представляет время суток без пользовательской даты. В DWH оно технически хранится на базовой дате `1970-01-01`.

## Функции

- [Date / Time constructors](/docs/script/functions/conversions/index.md) - создание и парсинг `Date(...)`, `Time(...)`.
- [Text(date/time)](/docs/script/functions/conversions/text.md) - форматирование даты и времени в текст.
- [AddDays](/docs/script/functions/date-time/add-days.md) - сдвиг даты на дни.
- [AddMonths](/docs/script/functions/date-time/add-months.md) - сдвиг даты на месяцы.
- [AddYears](/docs/script/functions/date-time/add-years.md) - сдвиг даты на годы.
- [DateOnly](/docs/script/functions/date-time/date-only.md) - обнуление времени.
- [Year](/docs/script/functions/date-time/year.md) - год.
- [Month](/docs/script/functions/date-time/month.md) - месяц.
- [Day](/docs/script/functions/date-time/day.md) - день месяца.
- [Hour](/docs/script/functions/date-time/hour.md) - час из `date` или `time`.
- [Minute](/docs/script/functions/date-time/minute.md) - минута из `date` или `time`.
- [Second](/docs/script/functions/date-time/second.md) - секунда из `date` или `time`.
- [Quarter](/docs/script/functions/date-time/quarter.md) - квартал года.
- [YearMonth](/docs/script/functions/date-time/year-month.md) - год и месяц.
- [YearQuarter](/docs/script/functions/date-time/year-quarter.md) - год и квартал.
- [YearWeek](/docs/script/functions/date-time/year-week.md) - ISO-год и ISO-неделя.
- [DayOfYear](/docs/script/functions/date-time/day-of-year.md) - день года.
- [DayOfWeek](/docs/script/functions/date-time/day-of-week.md) - день недели.
- [Week](/docs/script/functions/date-time/week.md) - ISO-неделя.
- [Now](/docs/script/functions/date-time/now.md) - текущие дата и время.
- [Today](/docs/script/functions/date-time/today.md) - текущая дата с нулевым временем.
