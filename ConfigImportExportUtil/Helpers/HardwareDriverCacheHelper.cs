using System;
using System.Collections.Generic;
using System.Linq;
using VideoOS.Platform.ConfigurationItems;

namespace ConfigImportExportUtil.Helpers
{
    internal static class HardwareDriverCacheHelper
    {
        private static readonly Dictionary<Guid, HardwareDriver> DriverCache = new Dictionary<Guid, HardwareDriver>();

        public static HardwareDriver GetDriver(Hardware hardware, RecordingServer recorder)
        {
            var hardwareId = new Guid(hardware.Id);
            HardwareDriver driver = null;
            
            lock (DriverCache)
            {
                if (DriverCache.ContainsKey(hardwareId))
                {
                    driver = DriverCache[hardwareId];
                }
                else
                {
                    driver = recorder.HardwareDriverFolder.HardwareDrivers.FirstOrDefault(d =>
                        d.Path == hardware.HardwareDriverPath);
                    DriverCache[hardwareId] = driver;
                }
            }
            return driver;
        }
    }
}
