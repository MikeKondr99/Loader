using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class AggregationFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Function("COUNT")
            .Doc("Возвращает общее количество строк")
            .ReturnsAggregatedNotNull(DataType.Integer)
            .Template($"COUNT(*)");

        foreach (var type in AllWithoutBool())
        {
            Function("COUNT")
                .Doc("Подсчитывает количество отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregatedNotNull(DataType.Integer)
                .Template($"COUNT({0})");

            Function("MIN")
                .Doc("Находит минимальное значение среди отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregated(type)
                .CustomNullPropagation(_ => true)
                .Template($"MIN(toNullable({0}))");

            Function("MAX")
                .Doc("Находит максимальное значение среди отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregated(type)
                .CustomNullPropagation(_ => true)
                .Template($"MAX(toNullable({0}))");

            Function("MODE")
                .Doc("Возвращает наиболее часто встречающееся значение среди отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregated(type)
                .CustomNullPropagation(_ => true)
                .Template($"if(COUNT({0}) = 0, NULL, arrayElement(topK(1)({0}), 1))");
        }

        foreach (var type in new[] { DataType.Integer, DataType.Number, DataType.DateTime, DataType.Text })
        {
            Function("COUNT_DISTINCT")
                .Doc("Подсчитывает количество уникальных отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregatedNotNull(DataType.Integer)
                .Template($"COUNT(DISTINCT {0})");

            Function("ONLY")
                .Doc("Возвращает значение, если оно уникально в наборе данных, иначе NULL")
                .Arg("value", type)
                .ReturnsAggregated(type)
                .CustomNullPropagation(_ => true)
                .Template($"(CASE WHEN COUNT(DISTINCT {0}) = 1 THEN MAX({0}) ELSE NULL END)");
        }

        foreach (var type in Numbers())
        {
            Function("SUM")
                .Doc("Вычисляет сумму всех отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregatedNotNull(type)
                .Template($"COALESCE(SUM({0}), 0)");

            Function("AVG")
                .Doc("Вычисляет среднее арифметическое отличных от NULL значений")
                .Arg("value", type)
                .ReturnsAggregated(DataType.Number)
                .CustomNullPropagation(_ => true)
                .Template($"CASE WHEN isNaN(AVG({0})) THEN NULL ELSE AVG({0}) END");
        }

        Function("AVG")
            .Doc("Вычисляет среднюю дату с точностью в секунду")
            .Arg("value", DataType.DateTime)
            .ReturnsAggregated(DataType.DateTime)
            .CustomNullPropagation(_ => true)
            .Template($"fromUnixTimestamp(CASE WHEN isNaN(AVG(toUnixTimestamp({0}))) THEN NULL ELSE toInt64(AVG(toUnixTimestamp({0}))) END)");

        Function("MEDIAN")
            .Doc("Возвращает медиану среди отличных от NULL значений")
            .Arg("value", DataType.Number)
            .ReturnsAggregated(DataType.Number)
            .CustomNullPropagation(_ => true)
            .Template($"quantileExactInclusive(0.5)({0})");

        Function("CONCAT")
            .Doc("Aggregates all non-NULL values into a single string without delimiter")
            .Arg("value", DataType.Text)
            .ReturnsAggregated(DataType.Text)
            .CustomNullPropagation(_ => true)
            .Template($"if(empty(groupArray({0})), NULL, arrayStringConcat(groupArray({0}), ''))");

        Function("CONCAT")
            .Doc("Aggregates all non-NULL values into a single string with delimiter")
            .Arg("value", DataType.Text)
            .Arg("delimiter", DataType.Text)
            .ReturnsAggregated(DataType.Text)
            .CustomNullPropagation(_ => true)
            .Template($"if(empty(groupArray({0})), NULL, arrayStringConcat(groupArray({0}), {1}))");

        foreach (var type in AllWithoutBool())
        {
            Function("CONCAT")
                .Doc("Aggregates values into a string with delimiter after sorting by specified column")
                .Arg("value", DataType.Text)
                .Arg("delimiter", DataType.Text)
                .Arg("sort", type)
                .ReturnsAggregated(DataType.Text)
                .CustomNullPropagation(_ => true)
                .Template($"""
                           if(empty(groupArray({0})), NULL,
                           arrayStringConcat(
                               arrayMap(
                                   x -> x.1,
                                   arraySort(
                                       x -> x.2,
                                       groupArray(({0}, {2}))
                                   )
                               ),
                               {1}
                           ))
                           """);
        }
    }

    private static IEnumerable<DataType> Numbers()
    {
        yield return DataType.Integer;
        yield return DataType.Number;
    }

    private static IEnumerable<DataType> AllWithoutBool()
    {
        yield return DataType.Text;
        yield return DataType.Integer;
        yield return DataType.Number;
        yield return DataType.DateTime;
        yield return DataType.Date;
        yield return DataType.Time;
    }
}
