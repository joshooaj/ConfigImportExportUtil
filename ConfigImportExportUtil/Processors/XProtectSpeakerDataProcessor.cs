using System;
using System.Linq;
using System.Threading.Tasks;
using ConfigImportExportUtil.Data;
using ConfigImportExportUtil.Helpers;
using VideoOS.Platform;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.Proxy.ConfigApi;

namespace ConfigImportExportUtil
{
    internal class XProtectSpeakerDataProcessor : IXProtectDataProcessor
    {
        private readonly XProtectHelper _xprotect;
        public string CsvFile { get; set; }
        public DataProcessorMode Mode { get; set; }
        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public XProtectSpeakerDataProcessor(XProtectHelper xprotect)
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
            var speakers = _xprotect.GetSpeakers()
                .Select(o => ConvertToDto(o, _xprotect));
            CsvReadWriteHelper.Write(speakers, CsvFile);
        }

        private SpeakerInfo ConvertToDto(Speaker speaker, XProtectHelper xprotect)
        {
            var hardware = new Hardware(Configuration.Instance.ServerFQID.ServerId, speaker.ParentItemPath);
            var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
            return new SpeakerInfo
            {
                Enabled = speaker.Enabled,
                ReadOnlyId = new Guid(speaker.Id),
                Name = speaker.DisplayName,
                ReadOnlyRecordingServer = recordingServer.DisplayName,
                ReadOnlyChannel = speaker.Channel,
                ReadOnlyHardwareName = hardware.DisplayName,
                RecordingEnabled = speaker.RecordingEnabled
            };

        }

        private void ImportData()
        {
            Log("Reading speaker info from csv");
            var speakerDtos = CsvReadWriteHelper.Read<SpeakerInfo>(CsvFile);
            Log("Retrieving speaker records from VMS");
            var speakers = _xprotect.GetSpeakers();

            Log($"Processing {speakerDtos.Count()} speakers from CSV");
            Parallel.ForEach(speakerDtos, speakerInfo =>
            {
                var speaker = speakers.FirstOrDefault(m =>
                    m.Id.Equals(speakerInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (speaker != null)
                {
                    Log($"Updating {speaker.DisplayName}");
                    speaker.Name = speakerInfo.Name;
                    speaker.Enabled = speakerInfo.Enabled;
                    speaker.SetProperty("RecordingEnabled", speakerInfo.RecordingEnabled.ToString());
                    try
                    {
                        speaker.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Speaker '{speaker.DisplayName}' not updated due to one or more invalid fields:\r\n{speakerInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No speaker was found on the VMS matching Id '{speakerInfo.ReadOnlyId}'");
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