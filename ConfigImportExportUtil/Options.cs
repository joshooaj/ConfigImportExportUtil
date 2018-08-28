using System;
using CommandLine;

namespace ConfigImportExportUtil
{
    internal class Options
    {
        [Option('i', "import", Required = false, HelpText = "Import data from CSV.", SetName = "action")]
        public bool Import { get; set; }

        [Option('e', "export", Required = false, HelpText = "Export data to CSV.", SetName = "action")]
        public bool Export { get; set; }

        [Option('m', "migrate", Required = false, HelpText = "Export from a local Professional VMS installation to an Advanced VMS system. Use with -k hardware", SetName = "action")]
        public bool Migrate { get; set; }

        [Option('u', "update", Required = false, HelpText = "Update a specific property like hardware password. Use with --kind hardware --property password", SetName = "action")]
        public bool Update { get; set; }

        
        [Option('d', "delete", Required = false, HelpText = "Delete one or more configuration items (supported for Hardware only)", SetName = "action")]
        public bool Delete { get; set; }

        [Option('r', "recorder", HelpText = "Name of the Recording Server to which cameras will be migrated from a local Professional VMS installation. Use with -m -k hardware -g groupName")]
        public string RecorderName { get; set; }

        [Option('g', "group", HelpText = "Name of the Camera Group to which cameras will be migrated from a local Professional VMS installation. Use with -m -k hardware")]
        public string CameraGroup { get; set; }

        [Option('k', "kind", Required = true, HelpText = "Kind of data to import/export. Options: recorder, hardware, camera, microphone, speaker, metadata, input, output")]
        public string Kind { get; set; }

        [Option('o', "property", Required = false, HelpText = "Use with --update. Current supported use is --kind hardware --property password --value newpassword")]
        public string Property { get; set; }

        [Option('a', "value", Required = false, HelpText = "Use with --update. Current supported use is --kind hardware --property password --value newpassword")]
        public string PropertyValue { get; set; }

        [Option('n', "new", HelpText = "Import new hardware from CSV or export a CSV template with the -e flag. Note that only the first camera channel will be enabled. All other channels and attached devices will be disabled by default.")]
        public bool NewHardware { get; set; }

        [Option('s', "server", Required = false, Default = "localhost", HelpText = "Milestone XProtect Management Server hostname or IP")]
        public string Server { get; set; }

        [Option('p', "port", Required = false, Default = 80, HelpText = "HTTP port used by Milestone XProtect Management Server. Default: 80.")]
        public int Port { get; set; }

        [Option('f', "file", Required = false, HelpText = "Filename of source or destination CSV file.")]
        public string CsvFile { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Log verbose info to console")]
        public bool Verbose { get; set; }

        [Option('c', "concurrency", Required = false, HelpText = "Enable concurrency")]
        public bool Concurrency { get; set; }
    }
}