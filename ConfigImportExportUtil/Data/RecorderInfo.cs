using System;

namespace ConfigImportExportUtil.Data
{
    internal class RecorderInfo
    {
        public string Name { get; set; }

        public string ReadOnlyHostName { get; set; }
        public int ReadOnlyPort { get; set; }
        public string ReadOnlyVersion { get; set; }
        public string ReadOnlyDevicePack { get; set; }
        public string ReadOnlyTimeZoneName { get; set; }
        public string ReadOnlyManagementServer { get; set; }
        public Guid ReadOnlyId { get; set; }
    }
}