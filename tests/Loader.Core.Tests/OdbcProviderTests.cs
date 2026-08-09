using Loader.Core.Providers;
using Loader.Core.Providers.Odbc;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;

namespace Loader.Core.Tests;

public sealed class OdbcProviderTests
{
    private static readonly OdbcProvider Provider = new();

    [Test]
    [DisplayName("ODBC provider возвращает ожидаемый provider kind")]
    public async Task Kind_is_odbc()
    {
        await Assert.That(Provider.Kind).IsEqualTo("odbc");
    }

    [Test]
    [DisplayName("ODBC ошибка соединения оборачивается в DbExecutionException с информацией о драйвере")]
    public async Task Connection_error_is_wrapped_in_provider_exception_with_driver_info()
    {
        var exception = await Assert.That(async () => await Provider.OpenReaderAsync(
                new ConnectionStringSource
                {
                    ConnectionString = "Driver={__loader_missing_odbc_driver__};Server=localhost"
                },
                new SqlTableConfig
                {
                    Sql = "SELECT * FROM missing_table"
                }))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'odbc' using ODBC driver '__loader_missing_odbc_driver__' (Unknown): SELECT * FROM missing_table");

        await Assert.That(exception!.Data["OdbcDriverKind"]).IsEqualTo(OdbcDriverKind.Unknown.ToString());
        await Assert.That(exception.Data["OdbcDriverName"]).IsEqualTo("__loader_missing_odbc_driver__");
    }
}