using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HumanGhost.Data.Csv
{
    public sealed class CsvRow
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        public int SourceLine
        {
            get;
        }

        internal CsvRow(int sourceLine, IReadOnlyList<string> headers, IReadOnlyList<string> cells)
        {
            SourceLine = sourceLine;
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Count; index++)
                copy.Add(headers[index], cells[index]);
            _values = new ReadOnlyDictionary<string, string>(copy);
        }

        public string Get(string columnName) => _values[columnName];
        public bool TryGet(string columnName, out string value) => _values.TryGetValue(columnName, out value);
    }
}
