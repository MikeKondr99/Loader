using System.Collections;
using System.Data.Common;

namespace Loader.Core.Decorators.Etl;

/// <summary>
/// DbDataReader-декоратор, который пропускает строки доменного reader-а до первой строки, прошедшей predicate.
/// </summary>
internal sealed class WhereDomainDataReader : DomainDataReaderDecorator
{
    private readonly Func<Row, bool> _predicate;
    private readonly Row _row;

    public WhereDomainDataReader(DomainDataReader inner, Func<Row, bool> predicate)
        : base(inner)
    {
        _predicate = predicate;
        _row = new Row(inner);
    }

    public override bool Read()
    {
        // 1. Двигаем domain reader по исходному потоку.
        while (Inner.Read())
        {
            // 2. Predicate читает текущий buffered row без повторного доступа к provider.
            if (_predicate(_row))
            {
                HasReadableRow = true;
                return true;
            }
        }

        // 3. Если подходящих строк больше нет, завершаем stream.
        HasReadableRow = false;
        return false;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        // 1. Асинхронно двигаем domain reader по исходному потоку.
        while (await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // 2. Predicate читает текущий buffered row без повторного доступа к provider.
            if (_predicate(_row))
            {
                HasReadableRow = true;
                return true;
            }
        }

        // 3. Если подходящих строк больше нет, завершаем stream.
        HasReadableRow = false;
        return false;
    }

    public override IEnumerator GetEnumerator()
    {
        return new DbEnumerator(this);
    }
}
