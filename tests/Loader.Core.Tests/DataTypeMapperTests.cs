using Loader.Core.Data;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class DataTypeMapperTests
{
    [Test]
    public async Task Maps_common_clr_types()
    {
        await Assert.That(DataTypeMapper.FromClrType(typeof(string))).IsEqualTo(DataType.Text);
        await Assert.That(DataTypeMapper.FromClrType(typeof(Guid))).IsEqualTo(DataType.Text);
        await Assert.That(DataTypeMapper.FromClrType(typeof(int))).IsEqualTo(DataType.Integer);
        await Assert.That(DataTypeMapper.FromClrType(typeof(decimal))).IsEqualTo(DataType.Number);
        await Assert.That(DataTypeMapper.FromClrType(typeof(DateTime))).IsEqualTo(DataType.DateTime);
        await Assert.That(DataTypeMapper.FromClrType(typeof(DateOnly))).IsEqualTo(DataType.Date);
        await Assert.That(DataTypeMapper.FromClrType(typeof(TimeOnly))).IsEqualTo(DataType.Time);
        await Assert.That(DataTypeMapper.FromClrType(typeof(bool))).IsEqualTo(DataType.Boolean);
    }

    [Test]
    public async Task Unknown_clr_type_throws()
    {
        await Assert.That(() => DataTypeMapper.FromClrType(typeof(object)))
            .ThrowsExactly<NotSupportedException>()
            .WithMessage("CLR type 'System.Object' is not supported by Loader data type mapper.");
    }
}
