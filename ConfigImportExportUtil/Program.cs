using CommandLine;
using System;
using ConfigImportExportUtil.Processors;

namespace ConfigImportExportUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            var isValid = Parser.Default.ParseArguments<Options>(args);
            isValid.WithParsed(Run);
        }

        private static void Run(Options options)
        {
            using (var xprotect = new XProtectHelper())
            {
                xprotect.Connect(new Uri($"http://{options.Server}:{options.Port}/"));
                if (!xprotect.State.HasFlag(XProtectHelper.XProtectState.Connected))
                {
                    Console.WriteLine(xprotect.LastError);
                    return;
                }

                IXProtectDataProcessor dataProcessor = null;
                if (options.Kind.StartsWith("hardware", StringComparison.OrdinalIgnoreCase) && !options.NewHardware)
                {
                    dataProcessor = new XProtectHardwareDataProcessor(xprotect, options);
                }
                if (options.Kind.StartsWith("hardware", StringComparison.OrdinalIgnoreCase) && options.NewHardware)
                {
                    dataProcessor = new XProtectNewHardwareDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("cam", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectCameraDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("record", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectRecorderDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("mic", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectMicrophoneDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("speak", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectSpeakerDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("meta", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectMetadataDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("in", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectInputDataProcessor(xprotect);
                }
                else if (options.Kind.StartsWith("out", StringComparison.OrdinalIgnoreCase))
                {
                    dataProcessor = new XProtectOutputDataProcessor(xprotect);
                }

                if (dataProcessor != null)
                    ProcessData(dataProcessor, options);
            }
        }

        private static void ProcessData(IXProtectDataProcessor dataProcessor, Options options)
        {
            dataProcessor.Mode = 
                options.Export ? DataProcessorMode.Export :
                options.Import ? DataProcessorMode.Import : 
                options.Migrate ?  DataProcessorMode.Migrate : 
                options.Delete ? DataProcessorMode.Delete :
                DataProcessorMode.Update;

            dataProcessor.CsvFile = options.CsvFile;
            dataProcessor.Verbose = options.Verbose;
            dataProcessor.Concurrency = options.Concurrency;
            dataProcessor.Process();
        }
    }
}
