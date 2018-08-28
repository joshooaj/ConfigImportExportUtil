using ConfigImportExportUtil.Data;
using ConfigImportExportUtil.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SystemAnalyzer.Common;
using VideoOS.Platform;
using VideoOS.Platform.ConfigurationItems;

namespace ConfigImportExportUtil.Processors
{
    internal class XProtectNewHardwareDataProcessor : IXProtectDataProcessor
    {
        private readonly XProtectHelper _xprotect;
        private readonly List<XProtectHardware> _failedMigrations = new List<XProtectHardware>();
        private RecordingServer _recordingServer;

        public DataProcessorMode Mode { get; set; }

        public string CsvFile { get; set; }

        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public IEnumerable<XProtectHardware> ProfessionalVmsHardware { get; set; }
        public string RecordingServerName { get; set; }
        public string CameraGroupName { get; set; }


        public XProtectNewHardwareDataProcessor(XProtectHelper xprotect)
        {
            _xprotect = xprotect;
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
                    ImportHardwareData();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExportHardwareData()
        {
            var hardware = new List<NewHardwareInfo>
            {
                new NewHardwareInfo()
                {
                    Address = "http://192.168.1.100:80/",
                    Username = "root",
                    Password = "pass",
                    DriverNumber = 123,
                    HardwareName = "New Hardware Device",
                    DeviceNamePrefix = "New Camera",
                    RecorderName = "RecordingServer1",
                    CameraGroupName = "MyCameraGroup"
                }
            };
            CsvReadWriteHelper.Write(hardware, CsvFile);
        }

        private void ImportHardwareData()
        {
            if (ProfessionalVmsHardware == null)
            {
                Log("Reading hardware info from csv");
                var hardwareDtos = CsvReadWriteHelper.Read<NewHardwareInfo>(CsvFile).ToList();

                Log($"Processing {hardwareDtos.Count()} hardware items from CSV");
                Parallel.ForEach(hardwareDtos, hardwareInfo =>
                {
                    try
                    {
                        AddCamera(hardwareInfo);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("An error occurred adding hardware named " + hardwareInfo.HardwareName + ". Error " + ex.Message);
                    }
                });
            }
            else
            {
                if (string.IsNullOrEmpty(RecordingServerName))
                {
                    Console.WriteLine("You must supply the name of the Recording Server to which to migrate cameras from the local Professional VMS");
                    return;
                }
                foreach (var hardwareInfo in ProfessionalVmsHardware)
                {
                    try
                    {
                        if (!AddCamera(hardwareInfo, RecordingServerName, CameraGroupName))
                        {
                            _failedMigrations.Add(hardwareInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _failedMigrations.Add(hardwareInfo);
                        Console.WriteLine("An error occurred adding hardware named " + hardwareInfo.DisplayName + ". Error " + ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }

                ShowFailedMigrationReport();
            }
        }

        private void ShowFailedMigrationReport()
        {
            if (_failedMigrations.Count == 0) return;
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("The following hardware failed to migrate successfully:");
            _failedMigrations.ForEach(m => Console.WriteLine($" - {m.DisplayName} at {m.IPAddress}"));
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
            if (ProfessionalVmsHardware == null && !Verbose) return;
            lock (_xprotect)
            {
                Console.WriteLine(message);
            }
        }

        private bool AddCamera(XProtectHardware newHardwareInfo, string recorderName, string cameraGroupName)
        {
            Log("");
            Log($"Preparing to add {newHardwareInfo.DisplayName}...");
            var managementServer = new ManagementServer(EnvironmentManager.Instance.MasterSite);

            _recordingServer = _recordingServer ?? managementServer.RecordingServerFolder.RecordingServers.FirstOrDefault(x => x.Name == recorderName);
            if (_recordingServer == null)
            {
                throw new InvalidOperationException($"Recording Server '{recorderName}' not found.");
            }

            Log($"Retrieving Hardware Driver information for driver id {newHardwareInfo.DriverId}...");
            var hardwareDriverPath = _recordingServer.HardwareDriverFolder.HardwareDrivers.FirstOrDefault(d => d.Number == newHardwareInfo.DriverId)?.Path;
            if (hardwareDriverPath == null)
            {
                Log($"Hardware driver {newHardwareInfo.DriverId} not found. Skipping {newHardwareInfo.DisplayName}.");
                return false;
            }

            Log($"Attempting to decrypt hardware credentials...");
            var decryptor = new StringDecryptor();
            var creds = decryptor.Decrypt(newHardwareInfo.EncryptedPassword);
            Log($"Result '{creds.Username}:{creds.Password}'");
            Log($"Adding {newHardwareInfo.DisplayName} to Advanced VMS...");
            var uri = new Uri($"http://{newHardwareInfo.IPAddress}:{newHardwareInfo.HttpPort}");
            var addHardwareServerTask = _recordingServer.AddHardware(uri.ToString(), hardwareDriverPath, creds.Username, creds.Password ?? string.Empty);

            var t1 = DateTime.Now;
            while (addHardwareServerTask.State != StateEnum.Error && addHardwareServerTask.State != StateEnum.Success)
            {
                if (DateTime.Now - t1 > TimeSpan.FromMinutes(5))
                {
                    Log("Timeout of 5 minutes reached during add hardware. Current task state is " + addHardwareServerTask.State);
                    break;
                }
                System.Threading.Thread.Sleep(1000);
                addHardwareServerTask.UpdateState();
            }

            if (addHardwareServerTask.State != StateEnum.Success)
            {
                Log($"Operation did not complete. Last state was '{addHardwareServerTask.State}'. Error: {addHardwareServerTask.ErrorText}");
                return false;
            }
            Log($"Hardware added successfully. Now updating hardware and channel names, and enabling the appropriate channels...");

            var hardware = new Hardware(EnvironmentManager.Instance.MasterSite.ServerId, addHardwareServerTask.Path);
            hardware.Name = newHardwareInfo.DisplayName;
            hardware.Enabled = true;
            try
            {
                hardware.Save();
            }
            catch (Exception ex)
            {
                Log("Rename of hardware failed with error " + ex.Message);
            }

            var xppCameras = newHardwareInfo.GetCameras();
            var cameras = hardware.CameraFolder.Cameras.OrderBy(c => c.Channel).ToList();
            foreach (var camera in cameras)
            {
                try
                {
                    var xppCamera = xppCameras.FirstOrDefault(c => c.Channel == camera.Channel);
                    camera.Name = xppCamera?.DisplayName ??
                                  $"Camera {camera.Channel + 1}";
                    camera.Enabled = xppCamera?.Enabled ?? false;
                    camera.Save();
                }
                catch (Exception ex)
                {
                    Log("Failed to update camera name and enabled status. Error " + ex.Message);
                }
            }

            var microphones = hardware.MicrophoneFolder.Microphones.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(microphones, newHardwareInfo.DisplayName, "Microphone");
            var speakers = hardware.SpeakerFolder.Speakers.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(speakers, newHardwareInfo.DisplayName, "Speaker");
            var inputs = hardware.InputEventFolder.InputEvents.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(inputs, newHardwareInfo.DisplayName, "Input");
            var outputs = hardware.OutputFolder.Outputs.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(outputs, newHardwareInfo.DisplayName, "Output");
            var metadata = hardware.MetadataFolder.Metadatas.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(metadata, newHardwareInfo.DisplayName, "Metadata");

            // alter other camera properties(?)
            var cameraGroup = FindOrAddCameraGroup(managementServer, cameraGroupName);
            foreach (var camera in cameras)
            {
                cameraGroup?.CameraFolder.AddDeviceGroupMember(camera.Path);
            }
            Log($"Sucessfully added {newHardwareInfo.DisplayName} and updated channel names and state");
            return true;
        }

        private static bool AddCamera(NewHardwareInfo newHardwareInfo)
        {
            var managementServer = new ManagementServer(EnvironmentManager.Instance.MasterSite);
            var recordingServer = managementServer.RecordingServerFolder.RecordingServers.FirstOrDefault(x => x.Name == newHardwareInfo.RecorderName);
            if (recordingServer == null)
            {
                Console.WriteLine("Error. Did not find recording server: " + newHardwareInfo.RecorderName);
                return false;
            }
            string hardwareDriverPath = recordingServer.HardwareDriverFolder.HardwareDrivers.FirstOrDefault(d => d.Number == newHardwareInfo.DriverNumber)?.Path;
            if (hardwareDriverPath == null)
            {
                Console.WriteLine("Error. Did not find hardware driver: " + newHardwareInfo.DriverNumber);
                return false;
            }
            Console.WriteLine("Will now attempt to add: " + newHardwareInfo.HardwareName);
            ServerTask addHardwareServerTask = recordingServer.AddHardware(newHardwareInfo.Address, hardwareDriverPath, newHardwareInfo.Username, newHardwareInfo.Password ?? string.Empty);
            var t1 = DateTime.Now;
            while (addHardwareServerTask.State != StateEnum.Error && addHardwareServerTask.State != StateEnum.Success)
            {
                System.Threading.Thread.Sleep(100);
                addHardwareServerTask.UpdateState();
            }
            Console.WriteLine("Hardware add task: " + addHardwareServerTask.State);
            if (addHardwareServerTask.State == StateEnum.Error)
            {
                Console.WriteLine("Hardware add error: " + addHardwareServerTask.ErrorText);
                return false;
            }

            var hardware =
                new Hardware(EnvironmentManager.Instance.MasterSite.ServerId, addHardwareServerTask.Path)
                {
                    Name = newHardwareInfo.HardwareName,
                    Enabled = true
                };
            hardware.Save();

            var cameras = hardware.CameraFolder.Cameras.OrderBy(c => c.Channel).Select(c => (dynamic) c);
            UpdateDevices(cameras, newHardwareInfo.DeviceNamePrefix, "Camera", DeviceEnableStrategy.EnableFirstChannelOnly);
            var microphones = hardware.MicrophoneFolder.Microphones.OrderBy(m => m.Channel).Select(m => (dynamic) m);
            UpdateDevices(microphones, newHardwareInfo.DeviceNamePrefix, "Microphone");
            var speakers = hardware.MicrophoneFolder.Microphones.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(speakers, newHardwareInfo.DeviceNamePrefix, "Speaker");
            var inputs = hardware.InputEventFolder.InputEvents.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(inputs, newHardwareInfo.DeviceNamePrefix, "Input");
            var outputs = hardware.OutputFolder.Outputs.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(outputs, newHardwareInfo.DeviceNamePrefix, "Output");
            var metadata = hardware.MetadataFolder.Metadatas.OrderBy(m => m.Channel).Select(m => (dynamic)m);
            UpdateDevices(metadata, newHardwareInfo.DeviceNamePrefix, "Metadata");

            // alter other camera properties(?)
            var cameraGroup = FindOrAddCameraGroup(managementServer, newHardwareInfo.CameraGroupName);
            foreach (var device in cameras)
            {
                var camera = (Camera) device;
                cameraGroup?.CameraFolder.AddDeviceGroupMember(camera.Path);
            }

            return true;
        }

        private enum DeviceEnableStrategy
        {
            EnableAllDevices,
            EnableNoDevices,
            EnableFirstChannelOnly
        }

        private static void UpdateDevices(IEnumerable<dynamic> devices, string prefix, string deviceType, DeviceEnableStrategy strategy = DeviceEnableStrategy.EnableNoDevices)
        {
            try
            {
                foreach (var device in devices)
                {
                    device.Name = $"{prefix} - {deviceType} {device.Channel + 1}";
                    switch (strategy)
                    {
                        case DeviceEnableStrategy.EnableAllDevices:
                            device.Enabled = true;
                            break;
                        case DeviceEnableStrategy.EnableNoDevices:
                            device.Enabled = false;
                            break;
                        case DeviceEnableStrategy.EnableFirstChannelOnly:
                            device.Enabled = device.Channel == 0;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
                    }
                    device.Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Initial device rename failed with error " + ex.Message);
            }
        }

        private static CameraGroup FindOrAddCameraGroup(ManagementServer ms, string groupName)
        {
            var groupExist = ms.CameraGroupFolder.CameraGroups.FirstOrDefault(x => x.Name == groupName);
            if (groupExist != null) return groupExist;

            var folder = ms.CameraGroupFolder;
            var task = folder.AddDeviceGroup(groupName, "group added by tool");
            Console.WriteLine("Camera group add (" + groupName + ") task: " + task.State);
            if (task.State != StateEnum.Success) return null;
            var path = task.Path;
            return new CameraGroup(EnvironmentManager.Instance.MasterSite.ServerId, path);
        }
    }
}