using ConfigImportExportUtil.ConfigurationService;
using ConfigImportExportUtil.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using VideoOS.ConfigurationAPI;
using VideoOS.Platform;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.SDK.Platform;
using Environment = VideoOS.Platform.SDK.Environment;

namespace ConfigImportExportUtil
{
    internal class XProtectHelper : IDisposable
    {
        private Uri _uri;
        private XProtectConfigurationService _configService;

        public XProtectState State
        {
            get
            {
                var i = 0;
                if (Environment.IsLoggedIn(_uri)) i += 1;
                if (Environment.IsServerConnected(_uri)) i += 2;
                return (XProtectState)i;
            }
        }

        public string LastError { get; set; }

        public XProtectHelper()
        {
            Environment.Initialize();
        }

        public void Connect(Uri uri)
        {
            _uri = uri;
            try
            {
                Environment.AddServer(uri, CredentialCache.DefaultNetworkCredentials);
                Environment.Login(uri);
                _configService = new XProtectConfigurationService(uri);
            }
            catch (ServerNotFoundMIPException)
            {
                LastError = $"Login to {_uri} failed. Server not found.";
            }
            catch (InvalidCredentialsMIPException ex)
            {
                LastError = $"Login to {_uri} failed. {ex.Message}";
            }
            catch (LoginFailedInternalMIPException ex)
            {
                LastError = $"Login to {_uri} failed. {ex.Message}";
            }
            catch (Exception e)
            {
                LastError = e.ToString();
            }
        }

        public string GetMacAddress(Hardware hardware)
        {
            var settings = hardware.HardwareDriverSettingsFolder.HardwareDriverSettings.FirstOrDefault();
            var properties = settings?.HardwareDriverSettingsChildItems.FirstOrDefault()?.Properties;
            if (properties == null) return null;
            return !properties.Keys.Contains("MacAddress") ? null : properties.GetValue("MacAddress");
        }

        public void Dispose()
        {
            try
            {
                if (State.HasFlag(XProtectState.LoggedIn))
                {
                    Environment.Logout(_uri);
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
        }

        [Flags]
        public enum XProtectState
        {
            LoggedIn = 1,
            Connected = 2
        }

        public List<Output> GetOutputs()
        {
            var configItemList = new List<Output>();
            Parallel.ForEach(GetHardware(), hardware =>
            {
                var configItem = _configService.Instance.GetChildItems($"{hardware.Path}/{ItemTypes.OutputFolder}");
                lock (configItemList)
                {
                    configItemList.AddRange(configItem.Select(o =>
                        new Output(Configuration.Instance.ServerFQID.ServerId, o.Path)));
                }
            });
            return configItemList;
        }

        public List<InputEvent> GetInputs()
        {
            var configItemList = new List<InputEvent>();
            Parallel.ForEach(GetHardware(), hardware =>
            {
                var configItem = _configService.Instance.GetChildItems($"{hardware.Path}/{ItemTypes.InputEventFolder}");
                lock (configItemList)
                {
                    configItemList.AddRange(configItem.Select(o =>
                        new InputEvent(Configuration.Instance.ServerFQID.ServerId, o.Path)));
                }
            });
            return configItemList;
        }

        public List<Metadata> GetMetadatas()
        {
            var metaDataList = new List<Metadata>();
            Parallel.ForEach(GetHardware(), hardware =>
            {
                var metadata = _configService.Instance.GetChildItems($"{hardware.Path}/{ItemTypes.MetadataFolder}");
                lock (metaDataList)
                {
                    metaDataList.AddRange(metadata.Select(o =>
                        new Metadata(Configuration.Instance.ServerFQID.ServerId, o.Path)));
                }
            });
            return metaDataList;
        }

        public List<Speaker> GetSpeakers()
        {
            var speakerList = new List<Speaker>();
            Parallel.ForEach(GetHardware(), hardware =>
            {
                var speakers = _configService.Instance.GetChildItems($"{hardware.Path}/{ItemTypes.SpeakerFolder}");
                lock (speakerList)
                {
                    speakerList.AddRange(speakers.Select(o =>
                        new Speaker(Configuration.Instance.ServerFQID.ServerId, o.Path)));
                }
            });
            return speakerList;
        }

        public List<Microphone> GetMicrophones()
        {
            var micList = new List<Microphone>();
            Parallel.ForEach(GetHardware(), hardware =>
            {
                var cameras = _configService.Instance.GetChildItems($"{hardware.Path}/{ItemTypes.MicrophoneFolder}");
                lock (micList)
                {
                    micList.AddRange(cameras.Select(o =>
                        new Microphone(Configuration.Instance.ServerFQID.ServerId, o.Path)));
                }
            });
            return micList;
        }

        public List<Camera> GetCameras()
        {
            var cameraList = new List<Camera>();
            Parallel.ForEach(GetHardware(), hardware =>
            {
                var cameras = _configService.Instance.GetChildItems($"{hardware.Path}/{ItemTypes.CameraFolder}");
                lock (cameraList)
                {
                    cameraList.AddRange(cameras.Select(c =>
                                new Camera(Configuration.Instance.ServerFQID.ServerId, c.Path))); 
                }
            });
            return cameraList;
        }

        private List<Item> GetItemsOfKindInFlatList(Guid kind)
        {
            var list = new List<Item>();
            GetItemsByKindRecursivelyAndAddToList(Configuration.Instance.GetItemsByKind(kind).FirstOrDefault(), list);
            Console.WriteLine($"Found {list.Count} cameras");
            Console.WriteLine(list[0].Name);
            return list;
        }

        private void GetItemsByKindRecursivelyAndAddToList(Item item, List<Item> itemList)
        {
            if (item.FQID.Kind == Kind.Camera && item.FQID.FolderType == FolderType.No)
            {
                itemList.Add(item);
            }

            if (item.HasChildren != HasChildren.No)
            {
                item.GetChildren().ForEach(i => GetItemsByKindRecursivelyAndAddToList(i, itemList));
            }
        }

        public List<Hardware> GetHardware(IEnumerable<HardwareInfo> info)
        {
            var recorders = new List<string>();
            foreach (var entry in info)
            {
                if (recorders.Contains(entry.ReadOnlyRecordingServer)) continue;
                recorders.Add(entry.ReadOnlyRecordingServer.ToLower());
            }

            var hardwareList = new List<Hardware>();
            Parallel.ForEach(GetRecordingServers().Where(r => recorders.Contains(r.Id.ToLower())), recordingServer =>
            {
                Console.WriteLine("Retrieving hardware info for devices on " + recordingServer.DisplayName);
                var hardwares =
                    _configService.Instance.GetChildItems($"{recordingServer.Path}/{ItemTypes.HardwareFolder}");
                lock (hardwareList)
                {
                    Console.WriteLine($"Adding {hardwares.Length} hardware entries from {recordingServer.DisplayName}");
                    hardwareList.AddRange(hardwares.Select(hw =>
                        new Hardware(Configuration.Instance.ServerFQID.ServerId, hw.Path)));
                }
                Console.WriteLine($"All hardware from {recordingServer.DisplayName} successfully retrieved");
            });
            return hardwareList;

        }

        public List<Hardware> GetHardware()
        {
            var hardwareList = new List<Hardware>();
            Parallel.ForEach(GetRecordingServers(), recordingServer =>
            {
                Console.WriteLine("Retrieving hardware info for devices on " + recordingServer.DisplayName);
                var hardwares =
                    _configService.Instance.GetChildItems($"{recordingServer.Path}/{ItemTypes.HardwareFolder}");
                lock (hardwareList)
                {
                    Console.WriteLine($"Adding {hardwares.Length} hardware entries from {recordingServer.DisplayName}");
                    hardwareList.AddRange(hardwares.Select(hw =>
                                new Hardware(Configuration.Instance.ServerFQID.ServerId, hw.Path))); 
                }
                Console.WriteLine($"All hardware from {recordingServer.DisplayName} successfully retrieved");
            });
            return hardwareList;
        }

        public IEnumerable<RecordingServer> GetRecordingServers()
        {
            var servers = new List<RecordingServer>();
            servers.AddRange(_configService.Instance.GetChildItems($"/{ItemTypes.RecordingServerFolder}")
                .Select(configItem => new RecordingServer(Configuration.Instance.ServerFQID.ServerId, configItem.Path)));
            return servers;
        }

        /// <summary>
        /// We ridiculously have to call the exact same method to retrieve the password TWICE
        /// before we can retrieve a non-null value from the Password property. Also, it doesn't
        /// appear to work to call the hardware.ReadPasswordHardware() method. Not even if you
        /// call it twice...
        /// </summary>
        /// <param name="hardware"></param>
        /// <returns>Password</returns>
        public string GetPassword(Hardware hardware)
        {
            var configItem = _configService.Instance.GetItem(hardware.Path);
            configItem = _configService.Instance.InvokeMethod(configItem, "ReadPasswordHardware");
            configItem = _configService.Instance.InvokeMethod(configItem, "ReadPasswordHardware");
            return configItem.Properties
                .SingleOrDefault(p => string.Equals(p.Key, "Password", StringComparison.OrdinalIgnoreCase))?
                .Value;
        }

        public async Task<string> GetPasswordAsync(Hardware hardware)
        {
            string password = null;
            try
            {
                await Task.Run(() => password = GetPassword(hardware));
            }
            catch (Exception)
            {
            }
            return password;
        }

        /// <summary>
        /// Apparently the Config API does not support deleting hardware, or at least not like this. Leaving the code here for now though as I hope it will later on.
        /// </summary>
        /// <param name="hwPath"></param>
        public void DeleteConfigurationItem(string hwPath)
        {
            try
            {
                var serverId = EnvironmentManager.Instance.MasterSite.ServerId;
                var hw = new Hardware(serverId, hwPath);
                var hwFolder = new HardwareFolder(serverId, hw.ParentPath);
                Console.WriteLine("Deleting " + hw.DisplayName + "...");
                var task = hwFolder.DeleteHardware(hwPath);
                while (task.Progress < 100)
                {
                    Console.Write(".");
                    Task.Delay(1000);
                    task.UpdateState();
                }
                if (task.State == StateEnum.Error)
                {
                    throw new Exception($"DeleteHardware task did not succeed. Error \"{task.ErrorText}\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting {hwPath}. {ex.Message}");
            }
        }
    }
}