using System.Collections;
using System.Data;
using System.Data.Common;
using Loader.Core.Providers.Excel;

namespace Loader.Core.Tests;

public sealed class ExcelRangeDataReaderTests
{
    [Test]
    [DisplayName("Excel range при пропуске строк не читает значения до StartRow")]
    public async Task Skipped_rows_are_not_materialized()
    {
        await using var inner = new ThrowingSkippedRowsReader(startRow: 5);
        await using var rangeReader = await ExcelRangeDataReader.CreateAsync(
            inner,
            new ExcelCellRange
            {
                StartRow = 5,
                StartColumn = 2,
                EndRow = 6,
                EndColumn = 3
            },
            hasHeader: true,
            CancellationToken.None);

        await Assert.That(rangeReader.GetName(0)).IsEqualTo("Name");
        await Assert.That(rangeReader.GetName(1)).IsEqualTo("Amount");

        await Assert.That(await rangeReader.ReadAsync()).IsTrue();
        await Assert.That(rangeReader.GetValue(0)).IsEqualTo("Alice");
        await Assert.That(rangeReader.GetValue(1)).IsEqualTo("10");

        await Assert.That(await rangeReader.ReadAsync()).IsFalse();
    }

    [Test]
    [DisplayName("Excel range не читает строку после EndRow")]
    public async Task Read_does_not_advance_inner_reader_after_end_row()
    {
        await using var inner = new ThrowingSkippedRowsReader(startRow: 1, throwAfterRow: 2);
        await using var rangeReader = await ExcelRangeDataReader.CreateAsync(
            inner,
            new ExcelCellRange
            {
                StartRow = 1,
                StartColumn = 1,
                EndRow = 2,
                EndColumn = 2
            },
            hasHeader: true,
            CancellationToken.None);

        await Assert.That(await rangeReader.ReadAsync()).IsTrue();
        await Assert.That(rangeReader.GetValue(0)).IsEqualTo("wrong");
        await Assert.That(await rangeReader.ReadAsync()).IsFalse();
    }

    [Test]
    [DisplayName("Excel range typed getters читают remapped ordinal из диапазона")]
    public async Task Typed_getters_use_projected_ordinal()
    {
        await using var inner = new ThrowingSkippedRowsReader(startRow: 1);
        await using var rangeReader = await ExcelRangeDataReader.CreateAsync(
            inner,
            new ExcelCellRange
            {
                StartRow = 1,
                StartColumn = 2,
                EndRow = 2,
                EndColumn = 3
            },
            hasHeader: true,
            CancellationToken.None);

        await Assert.That(await rangeReader.ReadAsync()).IsTrue();
        await Assert.That(rangeReader.GetString(0)).IsEqualTo("Alice");
        await Assert.That(rangeReader.GetInt32(1)).IsEqualTo(10);
    }

    [Test]
    [DisplayName("Excel range parser отклоняет координаты за пределами листа без исключения")]
    public async Task Range_parser_rejects_values_outside_excel_limits()
    {
        await Assert.That(ExcelCellRange.TryParse("A1:XFD1048576", out _)).IsTrue();
        await Assert.That(ExcelCellRange.TryParse("A1:XFE2", out _)).IsFalse();
        await Assert.That(ExcelCellRange.TryParse("A1:XFD1048577", out _)).IsFalse();
        await Assert.That(ExcelCellRange.TryParse("A1:ZZZZZZZZZZ2", out _)).IsFalse();
    }

    private sealed class ThrowingSkippedRowsReader : DbDataReader
    {
        private readonly int _startRow;
        private readonly int? _throwAfterRow;
        private int _row;

        public ThrowingSkippedRowsReader(int startRow, int? throwAfterRow = null)
        {
            _startRow = startRow;
            _throwAfterRow = throwAfterRow;
        }

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override int Depth => 0;

        public override int FieldCount => 3;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => -1;

        public override bool Read()
        {
            if (_throwAfterRow is not null && _row >= _throwAfterRow.Value)
            {
                throw new InvalidOperationException($"Row after {_throwAfterRow.Value} was read.");
            }

            if (_row >= 6)
            {
                return false;
            }

            _row++;
            return true;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read());
        }

        public override bool NextResult()
        {
            return false;
        }

        public override bool IsDBNull(int ordinal)
        {
            ThrowIfSkippedRowWasTouched();
            return false;
        }

        public override object GetValue(int ordinal)
        {
            ThrowIfSkippedRowWasTouched();
            return (_row, ordinal) switch
            {
                (1, 0) => "ignored",
                (1, 1) => "Name",
                (1, 2) => "Amount",
                (2, 0) => "wrong",
                (2, 1) => "Alice",
                (2, 2) => "10",
                (5, 1) => "Name",
                (5, 2) => "Amount",
                (6, 1) => "Alice",
                (6, 2) => "10",
                _ => DBNull.Value
            };
        }

        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                values[ordinal] = GetValue(ordinal);
            }

            return count;
        }

        public override string GetName(int ordinal)
        {
            return $"Column{ordinal + 1}";
        }

        public override int GetOrdinal(string name)
        {
            throw new NotSupportedException();
        }

        public override string GetDataTypeName(int ordinal)
        {
            return "String";
        }

        public override Type GetFieldType(int ordinal)
        {
            return typeof(string);
        }

        public override IEnumerator GetEnumerator()
        {
            while (Read())
            {
                yield return this;
            }
        }

        public override bool GetBoolean(int ordinal)
        {
            return Convert.ToBoolean(GetValue(ordinal));
        }

        public override byte GetByte(int ordinal)
        {
            return Convert.ToByte(GetValue(ordinal));
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        {
            throw new NotSupportedException();
        }

        public override char GetChar(int ordinal)
        {
            throw new NotSupportedException();
        }

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        {
            throw new NotSupportedException();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return Convert.ToDateTime(GetValue(ordinal));
        }

        public override decimal GetDecimal(int ordinal)
        {
            return Convert.ToDecimal(GetValue(ordinal));
        }

        public override double GetDouble(int ordinal)
        {
            return Convert.ToDouble(GetValue(ordinal));
        }

        public override float GetFloat(int ordinal)
        {
            return Convert.ToSingle(GetValue(ordinal));
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new NotSupportedException();
        }

        public override short GetInt16(int ordinal)
        {
            return Convert.ToInt16(GetValue(ordinal));
        }

        public override int GetInt32(int ordinal)
        {
            return Convert.ToInt32(GetValue(ordinal));
        }

        public override long GetInt64(int ordinal)
        {
            return Convert.ToInt64(GetValue(ordinal));
        }

        public override string GetString(int ordinal)
        {
            return (string)GetValue(ordinal);
        }

        private void ThrowIfSkippedRowWasTouched()
        {
            if (_row < _startRow)
            {
                throw new InvalidOperationException($"Skipped row {_row} was materialized.");
            }
        }
    }
}
