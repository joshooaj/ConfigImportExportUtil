using ConfigImportExportUtil.Data;
using ConfigImportExportUtil.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using VideoOS.Platform;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.Proxy.ConfigApi;

namespace ConfigImportExportUtil
{
    internal class XProtectMicrophoneDataProcessor : IXProtectDataProcessor
    {
        private XProtectHelper _xprotect;
        public string CsvFile { get; set; }
        public DataProcessorMode Mode { get; set; }
        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public XProtectMicrophoneDataProcessor(XProtectHelper xprotect)
        {
            _xprotect = xprotect;
        }

        public void Process()
        {
            switch (Mode)
            {
                case DataProcessorMode.Export:
                    ExportData();
                    break;
                case DataProcessorMode.Import:
                    ImportData();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExportData()
        {
            var mics = _xprotect.GetMicrophones()
                .Select(o => ConvertToDto(o, _xprotect));
            CsvReadWriteHelper.Write(mics, CsvFile);
        }

        private MicInfo ConvertToDto(Microphone microphone, XProtectHelper xprotect)
        {
            var hardware = new Hardware(Configuration.Instance.ServerFQID.ServerId, microphone.ParentItemPath);
            var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
            return new MicInfo()
            {
                Enabled = microphone.Enabled,
                ReadOnlyId = new Guid(microphone.Id),
                Name = microphone.DisplayName,
                ReadOnlyRecordingServer = recordingServer.DisplayName,
                ReadOnlyChannel = microphone.Channel,
                ReadOnlyHardwareName = hardware.DisplayName,
                RecordingEnabled = microphone.RecordingEnabled
            };

        }

        private void ImportData()
        {
            Log("Reading microphone info from csv");
            var micDtos = CsvReadWriteHelper.Read<MicInfo>(CsvFile);
            Log("Retrieving microphone records from VMS");
            var microphones = _xprotect.GetMicrophones();

            Log($"Processing {micDtos.Count()} microphones from CSV");
            Parallel.ForEach(micDtos, micInfo =>
            {
                var mic = microphones.FirstOrDefault(m =>
                    m.Id.Equals(micInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (mic != null)
                {
                    Log($"Updating {mic.DisplayName}");
                    mic.Name = micInfo.Name;
                    mic.Enabled = micInfo.Enabled;
                    mic.SetProperty("RecordingEnabled", micInfo.RecordingEnabled.ToString());
                    try
                    {
                        mic.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Microphone '{mic.DisplayName}' not updated due to one or more invalid fields:\r\n{micInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No microphone was found on the VMS matching Id '{micInfo.ReadOnlyId}'");
                }
            });
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