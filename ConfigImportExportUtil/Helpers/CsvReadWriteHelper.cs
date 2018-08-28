using System.Collections.Generic;
using System.IO;

namespace ConfigImportExportUtil.Helpers
{
    internal static class CsvReadWriteHelper
    {
        public static void Write<T>(IEnumerable<T> dto, string file)
        {
            using (var stream = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream))
            {
                var csv = new CsvHelper.CsvWriter(writer);
                csv.WriteRecords(dto);
            }
        }

        public static IEnumerable<T> Read<T>(string file)
        {
            var list = new List<T>();
            using (var reader = File.OpenText(file))
            {
                var csv = new CsvHelper.CsvReader(reader);
                list.AddRange(csv.GetRecords<T>());
            }
            return list;
        }
    }
}
