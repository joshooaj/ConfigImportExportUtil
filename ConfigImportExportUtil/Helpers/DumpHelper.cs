using Newtonsoft.Json;

namespace ConfigImportExportUtil.Data
{
    internal static class DumpHelper
    {
        public static string ToPrettyString(this object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }
    }
}
