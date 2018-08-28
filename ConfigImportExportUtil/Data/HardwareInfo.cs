using System;

namespace ConfigImportExportUtil.Data
{
    internal class HardwareInfo
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Address { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public string ReadOnlyMac { get; set; }
        public string ReadOnlyDriverName { get; set; }
        public int ReadOnlyDriverNumber { get; set; }
        public string ReadOnlyRecordingServer { get; set; }
        public Guid ReadOnlyId { get; set; }
    }
}