using System;
using System.Collections.Generic;
using System.Data;

namespace ECommerce.Infrastructure.Services
{
    // Lightweight IDataReader over an in-memory list to stream rows to SqlBulkCopy without creating a DataTable
    internal sealed class ListProductDataReader : IDataReader
    {
        private readonly IList<(string Name, string Description, decimal Price, int CategoryId, int Stock)> _items;
        private readonly string _jobId;
        private int _index = -1;

        public ListProductDataReader(string jobId, IList<(string Name, string Description, decimal Price, int CategoryId, int Stock)> items)
        {
            _jobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            // No-op change to refresh file timestamp.
        }

        public object GetValue(int i)
        {
            var current = _items[_index];
            return i switch
            {
                0 => _jobId,
                1 => current.Name,
                2 => current.Description,
                3 => current.Price,
                4 => current.CategoryId,
                5 => current.Stock,
                _ => throw new IndexOutOfRangeException()
            };
        }

        public bool Read()
        {
            _index++;
            return _index < _items.Count;
        }

        // Synchronous-only reader; callers expecting async should still use ExecuteReaderAsync which returns a DbDataReader.
        public ValueTask<bool> ReadAsync(CancellationToken cancellationToken) => new ValueTask<bool>(Read());

        public int FieldCount => 6;

        public void Dispose() { }

        #region NotUsed IDataReader members
        public string GetName(int i) => i switch
        {
            0 => "JobId",
            1 => "Name",
            2 => "Description",
            3 => "Price",
            4 => "CategoryId",
            5 => "Stock",
            _ => throw new IndexOutOfRangeException()
        };

        public int GetOrdinal(string name) => name switch
        {
            "JobId" => 0,
            "Name" => 1,
            "Description" => 2,
            "Price" => 3,
            "CategoryId" => 4,
            "Stock" => 5,
            _ => -1
        };

        public bool NextResult() => false;
        public void Close() { }
        public DataTable GetSchemaTable() => null!;
        public bool IsClosed => false;
        public int RecordsAffected => -1;
        public int Depth => 0;
        public bool GetBoolean(int i) => (bool)GetValue(i);
        public byte GetByte(int i) => (byte)GetValue(i);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => (char)GetValue(i);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < count; i++)
                values[i] = GetValue(i)!;
            return count;
        }

        public bool IsDBNull(int i) => GetValue(i) == null;
        public IDataReader GetData(int i) => null!;
        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
        public decimal GetDecimal(int i) => (decimal)GetValue(i);
        public double GetDouble(int i) => (double)GetValue(i);
        public Type GetFieldType(int i) => i switch
        {
            0 => typeof(string),
            1 => typeof(string),
            2 => typeof(string),
            3 => typeof(decimal),
            4 => typeof(int),
            5 => typeof(int),
            _ => typeof(object)
        };
        public float GetFloat(int i) => (float)GetValue(i);
        public Guid GetGuid(int i) => (Guid)GetValue(i);
        public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
        public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
        public string GetString(int i) => (string)GetValue(i);
        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));
        #endregion
    }
}
