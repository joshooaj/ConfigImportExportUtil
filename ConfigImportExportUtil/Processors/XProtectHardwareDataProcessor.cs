using ConfigImportExportUtil.Data;
using ConfigImportExportUtil.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SystemAnalyzer.Common;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.Proxy.ConfigApi;
using Configuration = VideoOS.Platform.Configuration;

namespace ConfigImportExportUtil.Processors
{
    internal class XProtectHardwareDataProcessor : IXProtectDataProcessor
    {
        private readonly XProtectHelper _xprotect;
        private Options _options;

        public DataProcessorMode Mode { get; set; }

        public string CsvFile { get; set; }

        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public XProtectHardwareDataProcessor(XProtectHelper xprotect, Options options = null)
        {
            _xprotect = xprotect;
            _options = options;
        }

        public void Process()
        {
            switch (Mode)
            {
                case DataProcessorMode.Export:
                    ExportHardwareData();
                    break;
                case DataProcessorMode.Import:
                    ImportHardwareData();
                    break;
                case DataProcessorMode.Migrate:
                    var newHardwareProcessor = new XProtectNewHardwareDataProcessor(_xprotect);
                    newHardwareProcessor.Verbose = _options.Verbose;
                    newHardwareProcessor.ProfessionalVmsHardware = GetHardwareFromProfessional();
                    newHardwareProcessor.RecordingServerName = _options?.RecorderName;
                    newHardwareProcessor.CameraGroupName = _options?.CameraGroup;
                    newHardwareProcessor.Process();
                    break;
                case DataProcessorMode.Update:
                    if (_options.Property.Equals("password", StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Retrieving hardware records from VMS");
                        var hardware = _xprotect.GetHardware();
                        Parallel.ForEach(hardware, hw =>
                        {
                            ChangePassword(hw, _options.PropertyValue);
                        });
                        Log($"All passwords updated.");
                    }
                    break;
                case DataProcessorMode.Delete:
                    DeleteHardware();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DeleteHardware()
        {
            Log("Reading hardware info from csv");
            var hardwareDtos = CsvReadWriteHelper.Read<HardwareInfo>(CsvFile).ToList();
            Log("Retrieving hardware records from VMS");
            var hardware = _xprotect.GetHardware();
            foreach (var hw in hardware.Where(h => hardwareDtos.Any(d => string.Equals(d.ReadOnlyId.ToString(), h.Id, StringComparison.CurrentCultureIgnoreCase))))
            {
                _xprotect.DeleteConfigurationItem(hw.Path);
            }
        }

        private void ChangePassword(Hardware hw, string newPassword)
        {
            Log($"Updating password for {hw.Address}...");
            try
            {
                var task = hw.ChangePasswordHardware(newPassword);
                var i = 0;
                while (task.State == StateEnum.InProgress & i++ < 100)
                {
                    Task.Delay(100).Wait();
                    task.UpdateState();
                }
            }
            catch (Exception ex)
            {
                Log($"An error occurred while updating password for {hw.Address}. {ex.Message}");
            }
        }

        private IEnumerable<XProtectHardware> GetHardwareFromProfessional()
        {
            const string configPath = @"C:\ProgramData\Milestone\Milestone Surveillance\Configuration.xml";
            return XProtectHardware.GetHardwareFromProfessionalConfigFile(configPath);
        }

        private void ExportHardwareData()
        {
            var hardware = _xprotect.GetHardware()
                .Select(h => ConvertHardwareToHardwareInfo(h, _xprotect).Result);
            CsvReadWriteHelper.Write(hardware, CsvFile);
        }


        private void ImportHardwareData()
        {
            Log("Reading hardware info from csv");
            var hardwareDtos = CsvReadWriteHelper.Read<HardwareInfo>(CsvFile).ToList();
            Log("Retrieving hardware records from VMS");
            var hardware = _xprotect.GetHardware();
            //var hardware = _xprotect.GetHardware(hardwareDtos);

            Log($"Processing {hardwareDtos.Count()} hardware items from CSV");
            Parallel.ForEach(hardwareDtos, hardwareInfo =>
            {
                var hw = hardware.FirstOrDefault(h =>
                    h.Id.Equals(hardwareInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (hw != null)
                {
                    Log($"Updating {hw.DisplayName}");
                    hw.Address = hardwareInfo.Address;
                    hw.Name = hardwareInfo.Name;
                    hw.UserName = hardwareInfo.UserName;
                    try
                    {
                        hw.ChangePasswordHardware(hardwareInfo.Password);
                    }
                    catch (Exception ex)
                    {
                        Log($"Unable to set password. Error '{ex.Message}'");
                    }
                    try
                    {
                        hw.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Hardware '{hw.DisplayName}' not updated due to one or more invalid fields:\r\n{hardwareInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        
                    }
                }
                else
                {
                    Log($"No hardware was found on the VMS matching Id '{hardwareInfo.ReadOnlyId}'");
                }
            });
        }

        private static async Task<HardwareInfo> ConvertHardwareToHardwareInfo(Hardware hardware, XProtectHelper xprotect)
        {
            var password = xprotect.GetPasswordAsync(hardware);
            var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
            var driver = HardwareDriverCacheHelper.GetDriver(hardware, recordingServer);

            return new HardwareInfo()
            {
                Address = hardware.Address,
                Enabled = hardware.Enabled,
                ReadOnlyId = new Guid(hardware.Id),
                ReadOnlyMac = xprotect.GetMacAddress(hardware),
                Name = hardware.DisplayName,
                Password = await password,
                UserName = hardware.UserName,
                ReadOnlyRecordingServer = recordingServer.DisplayName,
                ReadOnlyDriverName = driver?.DisplayName,
                ReadOnlyDriverNumber = driver?.Number ?? -1
            };
        }

        private static async Task<HardwareDriver> GetHardwareDriverAsync(Hardware hardware, RecordingServer recordingServer)
        {
            HardwareDriver driver = null;
            await Task.Run(() =>
                driver = recordingServer.HardwareDriverFolder.HardwareDrivers.FirstOrDefault(d =>
                    d.Path == hardware.HardwareDriverPath));
            return driver;
        }

        private void Log(string message)
        {
            if (!Verbose) return;
            lock (_xprotect)
            {
                Console.WriteLine(message); 
            }
        }
    }
}