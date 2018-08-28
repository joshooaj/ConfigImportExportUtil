using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SystemAnalyzer.Common;
using ConfigImportExportUtil.Data;
using ConfigImportExportUtil.Helpers;
using Microsoft.Win32;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.Proxy.ConfigApi;

namespace ConfigImportExportUtil
{
    internal class XProtectRecorderDataProcessor : IXProtectDataProcessor
    {
        private readonly XProtectHelper _xprotect;

        public DataProcessorMode Mode { get; set; }

        public string CsvFile { get; set; }

        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public XProtectRecorderDataProcessor(XProtectHelper xprotect)
        {
            _xprotect = xprotect;
        }

        public void Process()
        {
            switch (Mode)
            {
                case DataProcessorMode.Export:
                    ExportRecorderData();
                    break;
                case DataProcessorMode.Import:
                    ImportRecorderData();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExportRecorderData()
        {
            var recorders = _xprotect.GetRecordingServers()
                .Select(h => ConvertRecorderToRecorderInfo(h, _xprotect));
            CsvReadWriteHelper.Write(recorders, CsvFile);
        }


        private void ImportRecorderData()
        {
            Log("Reading recorder info from csv");
            var recorderDtos = CsvReadWriteHelper.Read<RecorderInfo>(CsvFile);
            Log("Retrieving recorder records from VMS");
            var recordingServers = _xprotect.GetRecordingServers();

            Log($"Processing {recorderDtos.Count()} recorders from CSV");
            Parallel.ForEach(recorderDtos, recorderInfo =>
            {
                var recorder = recordingServers.FirstOrDefault(h =>
                    h.Id.Equals(recorderInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (recorder != null)
                {
                    Log($"Updating {recorder.DisplayName}");
                    recorder.Name = recorderInfo.Name;
                    try
                    {
                        recorder.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Recorder '{recorder.DisplayName}' not updated due to one or more invalid fields:\r\n{recorderInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No recorder was found on the VMS matching Id '{recorderInfo.ReadOnlyId}'");
                }
            });
        }

        private RecorderInfo ConvertRecorderToRecorderInfo(RecordingServer recorder, XProtectHelper xprotect)
        {
            var ms = new ManagementServer(recorder.ServerId);
            return new RecorderInfo()
            {
                ReadOnlyId = new Guid(recorder.Id),
                Name = recorder.DisplayName,
                ReadOnlyVersion = GetRecorderVersion(recorder),
                ReadOnlyDevicePack = GetDevicePack(recorder),
                ReadOnlyHostName = recorder.HostName,
                ReadOnlyManagementServer = ms.DisplayName,
                ReadOnlyPort = recorder.PortNumber,
                ReadOnlyTimeZoneName = recorder.TimeZoneName
            };
        }

        private string GetRecorderVersion(RecordingServer recorder)
        {
            try
            {
                var localPath = Service.GetImagePath("Milestone XProtect Recording Server", recorder.HostName);
                var match = Regex.Match(localPath, @"^(?<drive>\w):\\(?<path>.*)");
                if (!match.Success) return null;

                var uncPath = $@"\\{recorder.HostName}\{match.Groups["drive"].Value}$\{match.Groups["path"].Value}";

                var fileInfo = FileVersionInfo.GetVersionInfo(uncPath);
                return $"{fileInfo.ProductMajorPart}.{fileInfo.ProductMinorPart}.{fileInfo.ProductBuildPart}.{fileInfo.ProductPrivatePart}";
            }
            catch (Exception ex)
            {
                return "Unknown";
            }
        }

        private string GetDevicePack(RecordingServer recorder)
        {
            try
            {
                var rkey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, recorder.HostName);
                var subkey = rkey.OpenSubKey(@"SOFTWARE\WOW6432Node\VideoOS\DeviceDrivers");
                return subkey?.GetValue("VersionC")?.ToString();
            }
            catch (Exception ex)
            {
                return "Unknown";
            }
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