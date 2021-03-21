using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Windows.Data.Json;
using System.Net.Http;
using System.Linq;
using System.Text;
using MQTTnet.Client.Options;
using MQTTnet;
using System.Threading;
using MQTTnet.Client;
using System.Net.NetworkInformation;
using Windows.System.Profile;
using System.Diagnostics;
using System.Globalization;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace BMSNameSpace
{
    /*
    This class will handle
    - Connect to MQTT Broker
    - Report Device information & configure
    - Report Device status

    Termilogy:
    - Device: the object combine by System, Unit, String and Block.
    - SystemUUID: Unique Identifier of Raspberry Pi 3.
    - SystemMacAddress: Mac Address of Raspberry Pi 3 Network Interface.
    - UnitUUID: Unique Identifier for a unit. Have to unique with all system belong to a Cloud
    - StringUUID: Unique Identifier for a String. Have to unique with all system belong to a Cloud
    - BlockMacAddress: Unique Identifer for a Block. Have to unique with all system belong to a Cloud
    - timestamp:  UTC, unix timestamp when MO connect to MQTT broker. Device need convert to unix UTC before send to Cloud.
    */
    public class MQTT
    {
        MainPage rootPage = MainPage.Current;
        private static readonly HttpClient client = new HttpClient
        {
            BaseAddress = new Uri("http://34.87.20.124")
        };
        private static string CREDENTIAL_FILE = "Credential.json";
        private static string CONFIG_FILE = "Configure.json";
        private string PUT_MQTT_CREDENTIAL = "api/devices/credentials";
        public static string Prefix = "bms";
        private static string UUID;
        private static string MacAddress;
        public Topic Topic;
        public IMqttClient mqttClient;

        public MQTT()
        {
            var uuid = GetSystemUUID();
            MacAddress = GetSystemMacAddress();
            UUID = uuid;
            Topic = new Topic
            {
                Info = $"{Prefix}/{uuid}/info",
                Status = $"{Prefix}/{uuid}/state",
            };
        }

        /// <summary>
        /// MQTT Handler function
        /// </summary>
        /// <returns></returns>
        public async Task Handle()
        {
            try
            {
                // 1. Connect to MQTT broker
                await Connect();
                /*
                Avoiding the Thundering Herd
                When a server goes down, there is a possible anti-pattern called the Thundering Herd where all of the clients 
                try to reconnect immediately, thus creating a denial of service attack. In order to prevent this,
                most MQTT client libraries randomize the servers they attempt to connect to. 
                This setting has no effect if only a single server is used, but in the case of a cluster, 
                randomization, or shuffling, will ensure that no one server bears the brunt of the client reconnect attempts.
                */
                // 2. Report Info
                await ReportInfo();
                /*
                Create a job to report status.
                */
                // 3. Report Status
                await ReportStatus();

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }
        /// <summary>
        /// Connect to MQTT Broker
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            try
            {
                // 1. Get Device Credentials from flash disk
                var mqttCredential = await GetCredentials();
                //   1.1. Call Api to get MQTT credentials if no credentials found
                if (mqttCredential == null)
                {
                    var device = new DeviceModel
                    {
                        macAddress = MacAddress,
                        uuid = UUID,
                    };

                    mqttCredential = await PutCredentials(device);
                    if (mqttCredential == null)
                    {
                        throw new Exception("Can not get MQTT credential");
                        //Debug.WriteLine("Can not get MQTT credential");
                    }
                    //   1.2. Store credential to flash disk
                    await SetCredentials(mqttCredential);
                }

                if (mqttClient == null)
                {
                    mqttClient = new MqttFactory().CreateMqttClient();
                }

                // clear the old connection
                try
                {
                    await mqttClient.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("error", ex);
                }

                // 2. Connect to MQTT broker
                var options = new MqttClientOptionsBuilder()
                                .WithTcpServer(mqttCredential.Endpoint, mqttCredential.Port)
                                .WithClientId(mqttCredential.ClientId)
                                .WithCredentials(mqttCredential.Username, mqttCredential.Password)
                                //.WithKeepAlivePeriod(System.TimeSpan.FromSeconds(20))
                                //.WithCommunicationTimeout(System.TimeSpan.FromSeconds(60))
                                .WithCleanSession(false)
                                .Build();
                await mqttClient.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Connect mqtt ", ex);
            }
        }
        /// <summary>
        /// Report Device Information & Configuration 
        /// </summary>
        /// <returns>boolean</returns>
        public async Task<bool> ReportInfo()
        {
            try
            {
                if (mqttClient != null && !mqttClient.IsConnected)
                {
                    await mqttClient.ReconnectAsync();
                }
                Debug.WriteLine("report info ...", DateTime.Now);

                // TODO: 3. Get Device Information
                var deviceInfo = await GetDeviceInfo(CONFIG_FILE);
                //var deviceInfo = await GetDeviceInfo(CONFIG_FILE); // info should be an object ==> DeviceInfo

                if (deviceInfo == null)
                {
                    Debug.WriteLine("Can not get device info from file config");
                    return false;
                }
                var dt = DateTime.ParseExact(deviceInfo.date, "dd/MM/yy HH:mm:ss", CultureInfo.InvariantCulture).ToUniversalTime();
                var timestamp = ((DateTimeOffset)dt).ToUnixTimeSeconds();

                var units = new List<object>();

                if (deviceInfo.Units != null)
                {
                    // step 1. get unitid & stringid from UNIT_STRING_ID.JSON
                    var hardcodeId = await GetDeviceInfoFromStorage<JsonHardcodeModel>("UNIT_STRINGID.json");
                    if (hardcodeId == null)
                    {
                        // step 1.2. store unitids & stringId to UNIT_STRING_ID.JSON
                        var stringId = Guid.NewGuid();
                        var unitIds = deviceInfo.Units.Select(x => new UnitHardcodeModel
                        {
                            Address = x.Address,
                            Id = Guid.NewGuid().ToString()
                        });
                        hardcodeId = new JsonHardcodeModel
                        {
                            StringId = stringId.ToString(),
                            UnitIds = unitIds.ToList()
                        };
                        try
                        {
                            await SetDeviceInfoToStorage(hardcodeId, "UNIT_STRINGID.json");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Can not set unit_string_id to storage", ex);
                        }
                    }
                    foreach (var unit in deviceInfo.Units)
                    {
                        units.Add(new
                        {
                            id = hardcodeId.UnitIds.FirstOrDefault(f => f.Address == unit.Address).Id,
                            name = unit.Name,
                            address = unit.Address,
                            blkCapacity = unit.BlkCapacity,
                            enable = unit.Config.Enable,
                            operationMode = unit.Config.OperationMode,
                            refreshDuration = unit.Config.RefreshDuration,
                            voltageLevel = unit.Config.VoltageLevel,
                            deviceType = unit.Config.DeviceType.ToString(),
                            cableType = unit.Config.CableType.ToString(),
                            capacity = unit.Config.Capacity
                        });
                    }
                }
                var strings = new List<object>();
                if (deviceInfo.Strings != null)
                {
                    foreach (var str in deviceInfo.Strings)
                    {
                        strings.Add(new
                        {
                            id = str.Id,
                            unitId = str.UnitId,
                            name = str.Name,
                            address = str.Address,
                            blkCapacity = str.BlkCapacity,
                            refreshDuration = str.RefreshDuration,
                            enable = str.Enable,
                            operationMode = str.OperationMode,
                            voltageLevel = str.VoltageLevel,
                            deviceType = str.DeviceType,
                            cableType = str.CableType,
                            capacity = str.Capacity
                        });
                    }
                }
                var blocks = new List<object>();
                if (deviceInfo.Blocks != null)
                {
                    foreach (var block in deviceInfo.Blocks)
                    {
                        blocks.Add(new
                        {
                            stringId = block.StringId.ToString(),
                            unitId = block.UnitId.ToString(),
                            macAddress = string.Format("{0}-{1}-{2}-{3}", block.Address.Address16, block.Address.Address24, block.Address.Address32, block.Address.Address40),
                            //name = "doesnot exist in configure.json",
                            //address = block.Address,
                            //blkCapacity = "doesnot exist in configure.json",
                            refreshDuration = block.Config.RefreshDuration,
                            enable = block.Config.Enable,
                            operationMode = block.Config.OperationMode,
                            voltageLevel = block.Config.VoltageLevel,
                            deviceType = block.Config.DeviceType.ToString(),
                            cableType = block.Config.CableType.ToString(),
                            capacity = block.Config.Capacity
                        });
                    }
                }

                // follow sampleData on cloud
                var _payloadConfig = new
                {
                    cloudVersion = 0,
                    localVersion = 1,
                    systemId = UUID,
                    system = new
                    {
                        name = deviceInfo.System.Name,
                        culture = deviceInfo.System.Culture,
                        //siteName = "BMS",
                        siteName = rootPage.configSettingData.SystemName,
                        //timezone = deviceInfo.System.TimeZone.ToString(),
                        timezone = rootPage.TimeZoneDisplayNamesList[rootPage.timeZoneIndex],
                        r1 = deviceInfo.System.R1,
                        r2 = deviceInfo.System.R2,
                        vscale = deviceInfo.System.VScale,
                        address16 = deviceInfo.System.Address.Address16,
                        address24 = deviceInfo.System.Address.Address24,
                        address32 = deviceInfo.System.Address.Address32,
                        address40 = deviceInfo.System.Address.Address40,
                        enable = deviceInfo.System.Config.Enable,
                        operationMode = deviceInfo.System.Config.OperationMode,
                        refreshDuration = deviceInfo.System.Config.RefreshDuration,
                        voltageLevel = deviceInfo.System.Config.VoltageLevel,
                        deviceType = deviceInfo.System.Config.DeviceType.ToString(),
                        cableType = deviceInfo.System.Config.CableType.ToString(),
                        capacity = deviceInfo.System.Config.Capacity,
                        rUpper = deviceInfo.System.Alarm.RUpper,
                        vUpper = deviceInfo.System.Alarm.VUpper,
                        vLower = deviceInfo.System.Alarm.VLower,
                        eUpper = deviceInfo.System.Alarm.EUpper,
                        eLower = deviceInfo.System.Alarm.ELower,
                        tUpper = deviceInfo.System.Alarm.TUpper,
                        tLower = deviceInfo.System.Alarm.TLower
                    },
                    units = units.Count() > 0 ? units : new List<object>(),
                    strings = strings.Count() > 0 ? strings : new List<object>(),
                    blocks = blocks.Count() > 0 ? blocks : new List<object>(),
                    config = new
                    {
                        alarm = new
                        {
                            rUpper = deviceInfo.System.Alarm.RUpper,
                            vUpper = deviceInfo.System.Alarm.VUpper,
                            vLower = deviceInfo.System.Alarm.VLower,
                            eUpper = deviceInfo.System.Alarm.EUpper,
                            eLower = deviceInfo.System.Alarm.ELower,
                            tUpper = deviceInfo.System.Alarm.TUpper,
                            tLower = deviceInfo.System.Alarm.TLower
                        }
                    },
                    timestamp = timestamp,
                };
                var payload = JsonConvert.SerializeObject(_payloadConfig);

                //var payload = new StringContent(JsonConvert.SerializeObject(_payload), Encoding.UTF8, "application/json");
                // 4. Report Device information
                var message = new MqttApplicationMessageBuilder()
                                        .WithTopic(Topic.Info)
                                        .WithPayload(payload)
                                        .WithAtLeastOnceQoS()
                                        .Build();
                await this.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Report device info " + ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Report Device Status
        /// </summary>
        /// <returns>boolean</returns>
        public async Task<bool> ReportStatus()
        {
            try
            {              
                Debug.WriteLine("report status ...", DateTime.Now);
                // TODO: 4. Get Device Status 
                var deviceStatus = await GetDeviceStatus(); // status should be an object
                if (deviceStatus == null)
                {
                    Debug.WriteLine("Can not get device status");
                    return false;
                }

                // follow sampleData on cloud
                // system
                var system = new
                {
                    id = UUID,
                    r1 = deviceStatus.System.R1,
                    r2 = deviceStatus.System.R2,
                    vscale = deviceStatus.System.VScale
                };
                // blocks
                var blocks = new List<Object>();
                if (deviceStatus.Blocks != null)
                {
                    foreach (var blkStatus in deviceStatus.Blocks)
                    {
                        blocks.Add(new
                        {
                            unitId = blkStatus.UnitId,
                            stringId = blkStatus.StringId,
                            macAddress = blkStatus.MacAddress,
                            enable = blkStatus.Enable,
                            r1 = blkStatus.R1,
                            r2 = blkStatus.R2,
                            v0 = blkStatus.V0,
                            v1 = blkStatus.V1,
                            v2 = blkStatus.V2,
                            e = blkStatus.E,
                            r = blkStatus.R,
                            t = blkStatus.T,
                            soc = blkStatus.SOC,
                            totalDischargeSecond = blkStatus.TotalDischargeSecond,
                            totalDischargeCycle = blkStatus.TotalDischargeCycle,
                            expectedLifeTimeRest = blkStatus.ExpectedLifeTimeRest,
                            vscale = blkStatus.VScale,
                            address16 = blkStatus.Address16,
                            address24 = blkStatus.Address24,
                            address32 = blkStatus.Address32,
                            address40 = blkStatus.Address40,
                            operationMode = blkStatus.OperationMode,
                            refreshDuration = blkStatus.RefreshDuration,
                            voltageLevel = blkStatus.VoltageLevel,
                            deviceType = blkStatus.DeviceType.ToString(),
                            cableType = blkStatus.CableType.ToString(),
                            capacity = blkStatus.Capacity,
                            blkCapacity = blkStatus.Capacity
                        });
                    }
                }
                var _payload = new
                {
                    system = system,
                    blocks = blocks,
                    timestamp = deviceStatus.TimeStamp
                };
                //var payload = new StringContent(JsonConvert.SerializeObject(_payload), Encoding.UTF8, "application/json");
                var payload = JsonConvert.SerializeObject(_payload);

                // 4. Report Device information
                var message = new MqttApplicationMessageBuilder()
                                        .WithTopic(Topic.Status)
                                        .WithPayload(payload)
                                        .WithAtLeastOnceQoS()
                                        .Build();
                await this.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Report device status", ex);
                return false;
            }
            return true;
        }
        private async Task PublishAsync (MqttApplicationMessage message)
        {
            if(mqttClient == null || message == null || String.IsNullOrEmpty(message.Payload.ToString()))
            {
                return;
            }
            if (!mqttClient.IsConnected)
            {
                await mqttClient.ReconnectAsync();
            }
            await mqttClient.PublishAsync(message, CancellationToken.None);
        }
        /// <summary>
        /// Get Device Credential from flash disk
        /// </summary>
        /// <returns>MQTTCredential</returns>
        private async Task<MQTTCredential> GetCredentials()
        {
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFile sFile = null;
            var files = await folder.GetFilesAsync();
            if (!files.Any())
            {
                return null;
            }
            var file = files.FirstOrDefault(x => x.Name == CREDENTIAL_FILE);
            if (file == null)
            {
                return null;
            }
            //get mqttcredential from file
            var contentFromFile = await Windows.Storage.FileIO.ReadTextAsync(file);
            if (String.IsNullOrEmpty(contentFromFile))
            {
                return null;
            }
            var mqttCredential = JsonConvert.DeserializeObject<MQTTCredential>(contentFromFile);
            return mqttCredential;
        }

        /// <summary>
        /// PutCredentials Request Cloud to generate a new MQTT credential
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private async Task<MQTTCredential> PutCredentials(DeviceModel device)
        {
            if (device == null)
            {
                return null;
            }
            var content = new StringContent(JsonConvert.SerializeObject(device), Encoding.UTF8, "application/json");

            var response = await client.PutAsync(PUT_MQTT_CREDENTIAL, content);
            if (response == null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var mqttCredential = JsonConvert.DeserializeObject<MQTTCredential>(responseString);
            return mqttCredential;

        }

        /// <summary>
        /// SetCredentials Store MQTT credentials in flash disk to use later.
        /// </summary>
        /// <param name="mqttCredential"></param>
        /// <returns></returns>
        private async Task SetCredentials(MQTTCredential mqttCredential)
        {
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var storeCredential = await folder.CreateFileAsync(CREDENTIAL_FILE, Windows.Storage.CreationCollisionOption.ReplaceExisting);
            await Windows.Storage.FileIO.WriteTextAsync(storeCredential, JsonConvert.SerializeObject(mqttCredential));
        }
        private async Task SetDeviceInfoToStorage(object obj, string fileName)
        {
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var dataSave = await folder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
            await Windows.Storage.FileIO.WriteTextAsync(dataSave, JsonConvert.SerializeObject(obj));
        }

        /// <summary>
        /// GetSystemMacAddress get mac address from a system. 
        /// </summary>
        /// <returns></returns>
        private string GetSystemMacAddress()
        {
            var adapters = AdapterHelper.GetAdapters();
            if (!adapters.Any())
            {
                throw new Exception("Cannot get Hardware Mac Address");
            }
            var ethernet = adapters.FirstOrDefault(x => x.Type == "Ethernet" && !String.IsNullOrEmpty(x.DhcpServer));
            if (ethernet != null)
            {
                return ethernet.MAC;
            }
            return adapters.FirstOrDefault().MAC;
        }

        /// <summary>
        /// GetSystemUUID Get system Unique Identifier
        /// </summary>
        /// <returns>uuid</returns>
        private string GetSystemUUID()
        {
            var deviceInformation = new EasClientDeviceInformation();
            var Id = deviceInformation.Id.ToString();
            return Id;
        }

        /// <summary>
        /// GetDeviceStatus Get Status of a Device (System, Unit, String, Block)
        /// When to use:
        /// - (future) Device detect a change of status
        /// - Device polling to get a new status, default is 10 minutes.
        /*
            {
            "system":{
                "id": "123", //systemId
                "r1": 2258,// only send when it's changed
                "r2": 1212, // only send when it's changed
                "vscale":123 // only send when it's changed
            },
            "blocks":[
                {
                    "unitId": "123",
                    "stringId": "123",
                    "macAddress": "123",
                    "enable": true,
                    "r1": 2162,
                    "r2": 507,
                    "v0": 507,
                    "v1": 507,
                    "v2": 507,
                    "e": 507,
                    "r": 507,
                    "t": 507,
                    "soc": 100,
                    totalDischargeSecond: 1,
                    totalDischargeCycle: 1,
                    expectedLifeTimeRest: 1,
                    "vscale": 4282,
                    "address16": 18725,
                    "address24": 73,
                    "address32": 73,
                    "address40": 74,
                    "operationMode": 0,
                    "refreshDuration": 0,
                    "voltageLevel": 3,
                    "deviceType": "",
                    "cableType": "",
                    "capacity": 0
                }
            ],
            "timestamp": int // UTC, unix timestamp when MO connect to MQTT broker
            }
        */
        /// </summary>
        /// <returns></returns>
        private async Task<DeviceStatus> GetDeviceStatus()
        {
            //return null;
            // TODO: 
            // 2. Get block status
            try
            {
                if (rootPage.outstandingQueue.Count() == 0)
                {
                    return null;
                }
                var result = new DeviceStatus();
                if (rootPage.outstandingQueue.Count() > 1)// There is a lost one     
                {
                    foreach (MeasurementDataPackage dp in rootPage.outstandingQueue)
                    {
                        dp.status = Constants.PACKAGE_STATUS_LOST;
                    }
                }
                // TODO: Need to investigate much time, so will ignore these data on this phase               

                var sysStatus = new SystemStatus
                {
                    //R1 = rootPage.settingData[0].R1,
                    //R2 = rootPage.settingData[0].R2,
                    //VScale = rootPage.settingData[0].VSCALE
                };
                // Blocks
                int unit, str, blk;
                unit = str = blk = 0;
                var blocks = new List<BlockStatus>();
                for (int i = 1; i < rootPage.totalKit + 1; i++)
                {
                    rootPage.GetId(rootPage.unitData, i, ref unit, ref str, ref blk);
                    blocks.Add(new BlockStatus
                    {
                        UnitId = unit.ToString(),
                        StringId = str.ToString(),
                        MacAddress = string.Format("{0}-{1}-{2}-{3}", rootPage.settingData[i].ADDRESS16, rootPage.settingData[i].ADDRESS24, rootPage.settingData[i].ADDRESS32, rootPage.settingData[i].ADDRESS40),
                        Enable = rootPage.settingData[i].CONFIG_STRUCT.enable,
                        R1 = rootPage.settingData[i].R1,
                        R2 = rootPage.settingData[i].R2,
                        V0 = rootPage.displayData[i].V0,
                        V1 = rootPage.displayData[i].V1,
                        V2 = rootPage.displayData[i].V2,
                        E = rootPage.displayData[i].E,
                        R = rootPage.displayData[i].R,
                        T = rootPage.displayData[i].T,
                        SOC = rootPage.displayData[i].SOC,
                        TotalDischargeSecond = rootPage.extendedDataList[i].TotalDischargeSecond,
                        TotalDischargeCycle = rootPage.extendedDataList[i].TotalDischargeCycle,
                        ExpectedLifeTimeRest = rootPage.extendedDataList[i].ExpectedLifeTimeRest,
                        VScale = rootPage.settingData[i].VSCALE,
                        Address16 = rootPage.settingData[i].ADDRESS16,
                        Address24 = rootPage.settingData[i].ADDRESS24,
                        Address32 = rootPage.settingData[i].ADDRESS32,
                        Address40 = rootPage.settingData[i].ADDRESS40,
                        OperationMode = rootPage.settingData[i].CONFIG_STRUCT.operationMode,
                        RefreshDuration = rootPage.settingData[i].CONFIG_STRUCT.refreshDuration,
                        VoltageLevel = rootPage.settingData[i].CONFIG_STRUCT.voltageLevel,
                        DeviceType = rootPage.settingData[i].CONFIG_STRUCT.deviceType,
                        CableType = rootPage.settingData[i].CONFIG_STRUCT.cableType,
                        Capacity = rootPage.settingData[i].CONFIG_STRUCT.blkCapacity
                    });
                }
                //
                var dataPackage = rootPage.outstandingQueue.FirstOrDefault();
                var timestamp = ((DateTimeOffset)(new DateTime(dataPackage.timeTicks).ToUniversalTime())).ToUnixTimeSeconds();
                result = new DeviceStatus
                {
                    System = sysStatus,
                    Blocks = blocks,
                    TimeStamp = timestamp
                };

                rootPage.outstandingQueue.Clear();

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// GetDeviceInfo Get information and configuration of a Device (System, Unit, String, Block)
        /// When to use:
        /// - Device connect to MQTT 
        /// - Device information was changed 
        /// Example: 
        /*    {
                "cloudVersion": 0, // cloud version of config
                "localVersion": 0, // local version of config
                "systemId":"40580e3d-2c96-42b3-a70f-97e336f03d90", // system UUID
                "system": {
                    "name": "BMS", // system name
                    "culture": 0, 
                    "siteName": "",
                    "timezone": "Asia/Ho_Chi_Minh",
                    "r1": 2162,
                    "r2": 507,
                    "vscale": 4282,
                    "address16": 59367,
                    "address24": 231,
                    "address32": 231,
                    "address40": 231,
                    "enable": true,
                    "operationMode": "",
                    "refreshDuration": 1,
                    "voltageLevel": 1,
                    "deviceType": "",
                    "cableType": "",
                    "capacity": 1,
                    "rUpper": 1,
                    "vUpper": 1,
                    "vLower": 1,
                    "eUpper": 1,
                    "eLower": 1,
                    "tUpper": 1,
                    "tLower": 1,
                },
            "units":[{
                "id":"40580e3d-2c96-42b3-a70f-97e336f03d90",
                "name":"Local Unit",
                "address":13,
                "blkCapacity":0,
                "refreshDuration":0,
                "enable":true,
                "operationMode":0,
                "voltageLevel": 1,
                "deviceType": "",
                "cableType": "",
                "capacity": 0
            }],
            "strings":[{
                "id":"40580e3d-2c96-42b3-a70f-97e336f03d90",
                "unitId":"40580e3d-2c96-42b3-a70f-97e336f03d90",
                "name":"Local String",
                "address":13,
                "blkCapacity":0,
                "refreshDuration":0,
                "enable":true,
                "operationMode":0,
                "voltageLevel": 1,
                "deviceType": "",
                "cableType": "",
                "capacity": 0
            }],
            "blocks":[{
                "stringId":"40580e3d-2c96-42b3-a70f-97e336f03d90",
                "unitId":"40580e3d-2c96-42b3-a70f-97e336f03d90",
                "macAddress":"11:22:33:33:22:11",
                "name":"Local String",
                "address":13,
                "blkCapacity":0,
                "refreshDuration":0,
                "enable":true,
                "operationMode":0,
                "voltageLevel": 1,
                "deviceType": "",
                "cableType": "",
                "capacity": 0
            }],
            "config":{
                // DEFAULT_HIGH_LEVEL = 9;
                // DEFAULT_MID_LEVEL = 5;
                // DEFAULT_LOW_LEVEL = 0;
                "alarm":{
                "rUpper": 1,
                "vUpper": 1,
                "vLower": 1,
                "eUpper": 1,
                "eLower": 1,
                "tUpper": 1,
                "tLower": 1,
                }
            }
            "timestamp": int // UTC, unix timestamp when MO connect to MQTT broker
          } */
        /// </summary>
        /// <returns></returns>
        public async Task<DeviceInfo> GetDeviceInfo(string fileName)
        {
            // TODO: 
            // 1. Get Current System Information
            // 2. Get Current Unit Information
            // 3. Get Current String Information
            // 4. Get Current Block Information
            // 5. (Future) Detect a change
            try
            {
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var files = await folder.GetFilesAsync();
                if (!files.Any())
                {
                    return null;
                }
                var file = files.FirstOrDefault(x => x.Name == fileName);
                if (file == null)
                {
                    return null;
                }
                string data = await Windows.Storage.FileIO.ReadTextAsync(file);
                dynamic result = JsonConvert.DeserializeObject<DeviceInfo>(data);
                var deviceInfo = new DeviceInfo
                {
                    date = result.date,
                    System = result.System,
                    Units = result.Units,
                    Strings = result.Strings,
                    Blocks = result.Blocks,
                };
                return deviceInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<T> GetDeviceInfoFromStorage<T>(string fileName)
        {
            try
            {
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var files = await folder.GetFilesAsync();
                if (!files.Any())
                {
                    return default;
                }
                var file = files.FirstOrDefault(x => x.Name == fileName);
                if (file == null)
                {
                    return default;
                }
                var data = await Windows.Storage.FileIO.ReadTextAsync(file);
                var result = JsonConvert.DeserializeObject<T>(data);
                return result;
            }

            catch (Exception ex)
            {
                Debug.WriteLine("GetDeviceInfoFromStorage", ex);
                return default;
            }
        }
    }
}