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
    internal class XProtectMetadataDataProcessor : IXProtectDataProcessor
    {
        private readonly XProtectHelper _xprotect;
        public string CsvFile { get; set; }
        public DataProcessorMode Mode { get; set; }
        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        public XProtectMetadataDataProcessor(XProtectHelper xprotect)
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
            var metadatas = _xprotect.GetMetadatas()
                .Select(o => ConvertToDto(o, _xprotect));
            CsvReadWriteHelper.Write(metadatas, CsvFile);
        }

        private MetadataInfo ConvertToDto(Metadata metaData, XProtectHelper xprotect)
        {
            var hardware = new Hardware(Configuration.Instance.ServerFQID.ServerId, metaData.ParentItemPath);
            var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
            return new MetadataInfo
            {
                Enabled = metaData.Enabled,
                ReadOnlyId = new Guid(metaData.Id),
                Name = metaData.DisplayName,
                ReadOnlyRecordingServer = recordingServer.DisplayName,
                ReadOnlyChannel = metaData.Channel,
                ReadOnlyHardwareName = hardware.DisplayName,
                RecordingEnabled = metaData.RecordingEnabled
            };

        }

        private void ImportData()
        {
            Log("Reading metadata info from csv");
            var metadataDtos = CsvReadWriteHelper.Read<MetadataInfo>(CsvFile);
            Log("Retrieving metadata records from VMS");
            var metadatas = _xprotect.GetMetadatas();

            Log($"Processing {metadataDtos.Count()} metadata records from CSV");
            Parallel.ForEach(metadataDtos, metadataInfo =>
            {
                var metadata = metadatas.FirstOrDefault(m =>
                    m.Id.Equals(metadataInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (metadata != null)
                {
                    Log($"Updating {metadata.DisplayName}");
                    metadata.Name = metadataInfo.Name;
                    metadata.Enabled = metadataInfo.Enabled;
                    metadata.SetProperty("RecordingEnabled", metadataInfo.RecordingEnabled.ToString());
                    try
                    {
                        metadata.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Metadata '{metadata.DisplayName}' not updated due to one or more invalid fields:\r\n{metadataInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No metadata record was found on the VMS matching Id '{metadataInfo.ReadOnlyId}'");
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

    internal class XProtectInputDataProcessor : XProtectIoDataProcessor
    {
        public XProtectInputDataProcessor(XProtectHelper xprotect) : base(xprotect)
        {
            this.TypeOfIo = IoType.Input;
        }
    }

    internal class XProtectOutputDataProcessor : XProtectIoDataProcessor
    {
        public XProtectOutputDataProcessor(XProtectHelper xprotect) : base(xprotect)
        {
            this.TypeOfIo = IoType.Output;
        }
    }

    internal class XProtectIoDataProcessor : IXProtectDataProcessor
    {
        private readonly XProtectHelper _xprotect;
        public string CsvFile { get; set; }
        public DataProcessorMode Mode { get; set; }
        public bool Verbose { get; set; }
        public bool Concurrency { get; set; }

        protected IoType TypeOfIo { get; set; }

        protected enum IoType
        {
            Input,
            Output
        }

        public XProtectIoDataProcessor(XProtectHelper xprotect)
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
                    if (TypeOfIo == IoType.Input)
                        ImportInputData();
                    else
                        ImportOutputData();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExportData()
        {
            if (TypeOfIo == IoType.Input)
            {
                var inputs = _xprotect.GetInputs()
                    .Select(o => (InputInfo) ConvertToDto(o, _xprotect));
                CsvReadWriteHelper.Write(inputs, CsvFile);
            }
            else
            {
                var outputs = _xprotect.GetOutputs()
                    .Select(o => (OutputInfo)ConvertToDto(o, _xprotect));
                CsvReadWriteHelper.Write(outputs, CsvFile);
            }
        }

        private object ConvertToDto(object io, XProtectHelper xprotect)
        {
            switch (io)
            {
                case InputEvent input:
                {
                    var hardware = new Hardware(Configuration.Instance.ServerFQID.ServerId, input.ParentItemPath);
                    var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
                    return new InputInfo
                    {
                        Enabled = input.Enabled,
                        ReadOnlyId = new Guid(input.Id),
                        Name = input.DisplayName,
                        ReadOnlyRecordingServer = recordingServer.DisplayName,
                        ReadOnlyChannel = input.Channel,
                        ReadOnlyHardwareName = hardware.DisplayName
                    };
                }
                case Output output:
                {
                    var hardware = new Hardware(Configuration.Instance.ServerFQID.ServerId, output.ParentItemPath);
                    var recordingServer = new RecordingServer(Configuration.Instance.ServerFQID.ServerId, hardware.ParentItemPath);
                    return new OutputInfo
                    {
                        Enabled = output.Enabled,
                        ReadOnlyId = new Guid(output.Id),
                        Name = output.DisplayName,
                        ReadOnlyRecordingServer = recordingServer.DisplayName,
                        ReadOnlyChannel = output.Channel,
                        ReadOnlyHardwareName = hardware.DisplayName
                    };
                }
            }
            return null;
        }

        private void ImportInputData()
        {
            Log($"Reading input info from csv");
            var inputDtos = CsvReadWriteHelper.Read<InputInfo>(CsvFile).ToList();
            Log($"Retrieving input records from VMS");
            var inputs = _xprotect.GetInputs();

            Log($"Processing {inputDtos.Count} input records from CSV");
            Parallel.ForEach(inputDtos, inputInfo =>
            {
                var input = inputs.FirstOrDefault(m =>
                    m.Id.Equals(inputInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (input != null)
                {
                    Log($"Updating {input.DisplayName}");
                    input.Name = inputInfo.Name;
                    input.Enabled = inputInfo.Enabled;
                    try
                    {
                        input.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Input '{input.DisplayName}' not updated due to one or more invalid fields:\r\n{inputInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No input record was found on the VMS matching Id '{inputInfo.ReadOnlyId}'");
                }
            });
        }

        private void ImportOutputData()
        {
            Log($"Reading output info from csv");
            var outputDtos = CsvReadWriteHelper.Read<OutputInfo>(CsvFile).ToList();
            Log($"Retrieving output records from VMS");
            var outputs = _xprotect.GetOutputs();

            Log($"Processing {outputDtos.Count()} output records from CSV");
            Parallel.ForEach(outputDtos, outputInfo =>
            {
                var output = outputs.FirstOrDefault(m =>
                    m.Id.Equals(outputInfo.ReadOnlyId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (output != null)
                {
                    Log($"Updating {output.DisplayName}");
                    output.Name = outputInfo.Name;
                    output.Enabled = outputInfo.Enabled;
                    try
                    {
                        output.Save();
                    }
                    catch (ValidateResultException)
                    {
                        Log(
                            $"Output '{output.DisplayName}' not updated due to one or more invalid fields:\r\n{outputInfo.ToPrettyString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
                else
                {
                    Log($"No output record was found on the VMS matching Id '{outputInfo.ReadOnlyId}'");
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