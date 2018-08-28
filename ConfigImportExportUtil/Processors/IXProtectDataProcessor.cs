namespace ConfigImportExportUtil
{
    internal interface IXProtectDataProcessor
    {
        string CsvFile { get; set; }
        DataProcessorMode Mode { get; set; }
        bool Verbose { get; set; }
        bool Concurrency { get; set; }

        void Process();
    }
}