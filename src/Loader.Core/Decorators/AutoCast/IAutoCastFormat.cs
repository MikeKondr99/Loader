namespace Loader.Core.Decorators.AutoCast;

/// <summary>
/// Формат, который умеет привести raw/domain значение к целевому типу.
/// Реализация ожидается stateless: один instance можно переиспользовать в разных analyzer и reader.
/// </summary>
public interface IAutoCastFormat
{
    string Name { get; }

    DataType DataType { get; }

    Type ClrType { get; }

    bool TryConvert(string value, out object converted);
}
