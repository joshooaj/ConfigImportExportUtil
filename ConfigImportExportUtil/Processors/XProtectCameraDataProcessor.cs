using ConfigImportExportUtil.Data;
using ConfigImportExportUtil.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoOS.Platform;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.Proxy.ConfigApi;

namespace ConfigImportExportUtil
{
    internal class XProtectCameraDataProcessor : IXProtectDataProcessor
    {
        private XProtectHelper _xprotect;
        public string CsvFile { get; set; }
        public DataProcessorMode Mode { get; set; }
        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public XProtectCameraDataProcessor(XProtectHelper xprotect)
        {
            _xprotect = xprotect;
        }

        public void Process()
        {
            switch (Mode)
            {
                case DataProcessorMode.Export:
                    ExportCameraData();
                    break;
                case DataProcessorMode.Import:
                    ImportCameraData();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExportCameraData()
        {
            var cameras = _xprotect.GetCameras()
                .Select(c => ConvertCameraToCameraInfo(c, _xprotect));
            CsvReadWriteHelper.Write(cameras, CsvFile);
        }

        private CameraInfo ConvertCameraToCameraInfo(Camera camera, XProtectHelper xprotect)
        {
            var hardware = new Hardware(Configuration.Instance.ServerFQID.ServerId, camera.ParentItemPath);
            var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
            var driver = HardwareDriverCacheHelper.GetDriver(hardware, recordingServer);
            return new CameraInfo()
            {
                Enabled = camera.Enabled,
                ReadOnlyId = new Guid(camera.Id),
                Name = camera.DisplayName,
                ReadOnlyResolution = GetDefaultStreamResolution(camera),
                ReadOnlyRecordingServer = recordingServer.DisplayName,
                ReadOnlyChannel = camera.Channel,
                ReadOnlyHardwareName = hardware.DisplayName,
                RecordingEnabled = camera.RecordingEnabled,
                ReadOnlyDriverName = driver?.DisplayName,
                ReadOnlyDriverNumber = driver?.Number ?? -1
            };
        }

        private void ImportCameraData()
        {
            Log("Reading camera info from csv");
            var cameraDtos = CsvReadWriteHelper.Read<CameraInfo>(CsvFile).ToList();
            Log("Retrieving camera records from VMS");
            var cameras = _xprotect.GetCameras();

            Log($"Processing {cameraDtos.Count()} cameras from CSV");
            Parallel.ForEach(cameraDtos, cameraInfo =>
            {
                var camera = cameras.FirstOrDefault(h =>
                    h.Id.Equals(cameraInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (camera != null)
                {
                    Log($"Updating {camera.DisplayName}");
                    camera.Name = cameraInfo.Name;
                    camera.Enabled = cameraInfo.Enabled;
                    camera.SetProperty("RecordingEnabled", cameraInfo.RecordingEnabled.ToString());
                    try
                    {
                        camera.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Camera '{camera.DisplayName}' not updated due to one or more invalid fields:\r\n{cameraInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No camera was found on the VMS matching Id '{cameraInfo.ReadOnlyId}'");
                }
            });
        }

        private static string GetDefaultStreamResolution(Camera camera)
        {
            const string defaultResult = "Not Available";
            var generalSettings = camera.DeviceDriverSettingsFolder.DeviceDriverSettings.FirstOrDefault()?.DeviceDriverSettingsChildItem;

            // If Resolution property is found in General driver settings, assume this is the only resolution value available and return it
            if (generalSettings != null && generalSettings.GetPropertyKeys().Any(k => k.Contains("Resolution")))
            {
                return generalSettings.GetProperty(generalSettings.GetPropertyKeys().First(k => k.Contains("Resolution")));
            }

            // Get first (and should be only) stream identified as the default live stream
            var stream = camera.StreamFolder?.Streams?.FirstOrDefault()?.StreamUsageChildItems
                ?.FirstOrDefault(s => s.LiveDefault);

            // This should be the stream name displayed in the settings tab of the camera
            var name = stream?.StreamReferenceIdValues.First(s => s.Value == stream.StreamReferenceId).Key;

            // If this camera doesn't support streams, name should be null so just return it since we
            // failed to find the resolution in the General tab so it might be a weird camera where 
            // resolution isn't available as an option or if it is, it's stored oddly in separate width/height properties perhaps.
            if (string.IsNullOrEmpty(name)) return defaultResult;

            var streamSetting = camera.DeviceDriverSettingsFolder.DeviceDriverSettings.First().StreamChildItems.FirstOrDefault(s =>
                s.DisplayName.Equals(name, StringComparison.InvariantCultureIgnoreCase));

            // Odd, no stream settings available with a matching name.
            if (streamSetting == null) return defaultResult;

            // Found the matching stream settings, but there's no Resolution key.
            if (!streamSetting.Properties.Keys.Any(k => k.Equals("Resolution"))) return defaultResult;

            return streamSetting.Properties.GetValue("Resolution");
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