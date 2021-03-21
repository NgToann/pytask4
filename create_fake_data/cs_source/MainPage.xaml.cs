//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using EASendMailRT;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Gpio;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Diagnostics;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System.Threading;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BMSNameSpace
{
    public sealed partial class MainPage : Page
    {

        public BSL.Api bsm;
        public ConfigSettingData configSettingData ;
        //public MqttFactory factory;
        //public IMqttClient mqttClient;
        public MQTTClient mqttClientInstance;

        public MQTT mqttClient;

        public MainPage()
        {
            Debug.WriteLine("Mainpage");
            //factory = new MqttFactory();
            //mqttClient = factory.CreateMqttClient();
                      
            Current = this;
            bsm = new BSL.Api();
            bsm.OnWiFiConnect += Bsm_OnWiFiConnectAsync;
            configSettingData = new ConfigSettingData();
            configSettingData.OnConfigReady += a_OnConfigDataReady;
            //Initialise();
            this.InitializeComponent();
            SampleTitle.Text = FEATURE_NAME;
            _ = InitGPIOAsync();

            server = new HTTPServer();
            server.Initialise();

            uart3G = new UART();
            uart3G.Initialise(Constants.BAUD_RATE);
            //            _ = InitialiseAsync();
            bsm.checkUpdate();

            // Create mqqtClient
            mqttClient = new MQTT();
        }
        //public async Task<int> MQTTSetup()
        //{
        //    var options = new MqttClientOptionsBuilder()
        //                            //.WithTcpServer("test.mosquitto.org", 1883)
        //                            .WithTcpServer("broker.hivemq.com", 1883)
        //                            //.WithClientId("tranlysfw")
        //                            .WithCredentials("tranlysfw", "zxcvbnm1")
        //                            //.WithTls()
        //                            .WithCleanSession(false)
        //                            //.WithKeepAlivePeriod(System.TimeSpan.FromSeconds(3))
        //                            //.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
        //                            .Build();

        //    await mqttClient.ConnectAsync(options, CancellationToken.None);

        //    var message = new MqttApplicationMessageBuilder()
        //                            .WithTopic("testtopic/mot")
        //                            .WithPayload("Hello World")
        //                            .WithAtLeastOnceQoS()
        //                            .Build();

        //    await mqttClient.PublishAsync(message, CancellationToken.None); // Since 3.0.5 with CancellationToken         
        //    //await Task.Delay(TimeSpan.FromSeconds(5));
        //    await mqttClient.DisconnectAsync();
        //    return 1;
        //}
        private async void Bsm_OnWiFiConnectAsync(string connectResult, string ssidName, string ssidPass, string ssidAvalableList)
        {
            await bsm.DeleteDebugFileAsync();
        }
        private void a_OnConfigDataReady(object sender, configEventArgs e)
        {
            _ = InitialiseAsync(); 
        }

        public void TimeZoneChanged()
        {
            TimeZoneDisplayNamesList = Windows.System.TimeZoneSettings.SupportedTimeZoneDisplayNames.ToArray<string>();
            string zoneName = TimeZoneDisplayNamesList[timeZoneIndex];
            bool canChange = Windows.System.TimeZoneSettings.CanChangeTimeZone;
            if (canChange) Windows.System.TimeZoneSettings.ChangeTimeZoneByDisplayName(zoneName);
            var tzis = TimeZoneInfo.GetSystemTimeZones();
            currentTZ = tzis[0];
            foreach (TimeZoneInfo tz in tzis)
            {
                if (zoneName == tz.DisplayName)
                {
                    currentTZ = tz;
                    break;
                }
            }
        }
        private void Initialise()
        {
            configSettingData.ParseConfigureData(ref unitData, ref settingData, ref commSettingData, ref alarmSettingData);
            ProcessFlags.configDataAvailable = false;
        }
        private async Task InitialiseAsync()
        {
            ProcessFlags.configDataAvailable = LoadConfig();
            await SettingAsync();
            await bsm.saveConfigFileToServerAsync(systemName);
            //bsm.checkUpdate();
            TimeZoneChanged();
            scenario0_Monitor.Initialise();

            // Connect to broker
            await mqttClient.Connect();
        }
        private async Task InitGPIOAsync()
        {
            // Get the default GPIO controller on the system
            gpio = GpioController.GetDefault();
            if (gpio == null)
                return;
            // Open GPIO 4 - POWER UC20
            PWR_CTRLpin = gpio.OpenPin(Constants.PWR_3G_PIN);
            PWR_CTRLpin.Write(GpioPinValue.High);
            PWR_CTRLpin.SetDriveMode(GpioPinDriveMode.Output);
            // Open GPIO 17 - RESET UC20
            RESETpin = gpio.OpenPin(Constants.RESET_3G_PIN);
            RESETpin.Write(GpioPinValue.Low);
            RESETpin.SetDriveMode(GpioPinDriveMode.Output);
            // Open GPIO 18 - STATUS UC20
            STATUSpin = gpio.OpenPin(Constants.STATUS_3G_PIN);
            STATUSpin.Write(GpioPinValue.High);
            STATUSpin.SetDriveMode(GpioPinDriveMode.Input);
            // Open GPIO 22 - DTR UC20
            DTRpin = gpio.OpenPin(Constants.DTR_3G_PIN);
            DTRpin.Write(GpioPinValue.Low);
            DTRpin.SetDriveMode(GpioPinDriveMode.Output);
            // Open GPIO 23 - AP_READY UC20
            AP_READYpin = gpio.OpenPin(Constants.AP_READY_3G_PIN);
            AP_READYpin.Write(GpioPinValue.Low);
            AP_READYpin.SetDriveMode(GpioPinDriveMode.Output);
            // Open GPIO 27 - PWR UC20
            //           if (STATUSpin.Read() == GpioPinValue.High)
            {
                PWRUPpin = gpio.OpenPin(Constants.PWRUP_3G_PIN);
                PWRUPpin.Write(GpioPinValue.High);
                PWRUPpin.SetDriveMode(GpioPinDriveMode.Output);
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                PWRUPpin.Write(GpioPinValue.Low);
            }
            return;
        }
        public async Task<UInt64> GetFreeSpace()
        {
            StorageFolder local = ApplicationData.Current.LocalFolder;
            var retrivedProperties = await local.Properties.RetrievePropertiesAsync(new string[] { "System.FreeSpace" });
            return (UInt64)retrivedProperties["System.FreeSpace"];
        }
//        public async Task<bool> LoadConfigAsync()
        public bool LoadConfig()
        {
            // Setting initializing . The default settings shall be loaded first . It should be collected from Setting file loaded from json file afterward.
            //            bool configLoaded = await configSettingData.Initialise();
            settingData = configSettingData.SettingDataList;
            unitData = configSettingData.UnitDataList;
            commSettingData = configSettingData.CommSetting;
            alarmSettingData = configSettingData.AlarmSetting;
            systemName = configSettingData.SystemName;
            cultureIndex = configSettingData.cultureIndex;
            timeZoneIndex = configSettingData.timeZoneIndex;
            return true;
        }
        public bool saveSettingData()
        {
            bool settingDataSaved;
            settingDataSaved = configSettingData.CreateJsonFromConfigureData(unitData, settingData, commSettingData, alarmSettingData);
            settingDataSaved &= configSettingData.UpdateConfigureData(unitData, settingData, commSettingData, alarmSettingData);
            _ = bsm.saveConfigFileToServerAsync(systemName);
          //  bsm.checkUpdate();

            return settingDataSaved;
        }
        private void btn_Home_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void a_OnReceivedAll(object sender, receivedEventArgs e)
        {
            _ = ProcessAsync();
        }
        // Setting system
        public async Task SettingAsync()
        {

            totalUnit = totalStr = 0;
            totalUnitKit = totalKit = totalBlk = totalRecord = 0;
            for (int i = 0; i < Constants.MAXUNIT; i++)
            {
                totalBlk += (ushort)(unitData[i].TOTAL_BLOCK);
                totalUnitKit = (ushort)(unitData[i].TOTAL_BLOCK+ unitData[i].TOTAL_STRING);
                totalKit += (ushort)totalUnitKit;
                totalRecord = (ushort)(totalKit + 1);

                totalStr += unitData[i].TOTAL_STRING;
                if (unitData[i].TOTAL_BLOCK != 0) totalUnit++;
                if (i == 0)                                                           // Local unit related
                {
                    unitData[i].RECORD_INDEX = 1;
                    unitData[i].NB_PAGE = (byte)(totalUnitKit / Constants.NBRECORDPERPAGE);
                    if ((totalUnitKit % Constants.NBRECORDPERPAGE) != 0)
                        unitData[i].NB_PAGE++;
                }
                else
                {
                    unitData[i].RECORD_INDEX = (ushort)(unitData[i - 1].RECORD_INDEX + (unitData[i - 1].TOTAL_BLOCK+1)*unitData[i - 1].TOTAL_STRING);  // Each string has 1 SMK accordingly  
                    unitData[i].NB_PAGE = (byte)(totalUnitKit / Constants.NBLORARECORDPERPAGE);
                    if ((totalUnitKit % Constants.NBLORARECORDPERPAGE) != 0)
                        unitData[i].NB_PAGE++;
                }
                unitData[i].CURRENT_PAGE = unitData[i].REPEAD_PAGE = 0;
                if (minRefreshDurationIndex < unitData[i].REFRESH_DURATION) minRefreshDurationIndex = unitData[i].REFRESH_DURATION;
            }

            if ((totalKit < Constants.MIN_NBKIT) || (totalKit > Constants.MAX_NBKIT) ||
                (totalStr < Constants.MIN_NBSTR) || (totalStr > Constants.MAX_NBSTR) ||
                (totalUnit < Constants.MIN_NBUNIT) || (totalUnit > Constants.MAX_NBUNIT))
            {
                totalKit = Constants.DEFAULT_NBKIT;
                totalStr = Constants.DEFAULT_NBSTR;
                totalUnit = Constants.DEFAULT_NBUNIT;
            }
            nbKitperPage = Constants.DEFAULT_NBKITPERPAGE;
            nbPage = (ushort)(totalKit/ Constants.DEFAULT_NBKITPERPAGE);
            if ((totalKit % Constants.DEFAULT_NBKITPERPAGE) != 0) nbPage++;
            lastReceivedPageIndex = (ushort)(nbPage - 1);

            if (settingData.Count() != totalKit + 1)                                 // Adding missed block if any
            {
                int startAddres16Index = 0;
                int startAddres24Index = 0;
                int startAddres32Index = 0;
                if (settingData.Count() >= 2)                                       // There is a sample setting for first block
                {
                    ushort startAddress16;
                    byte startAddress24 = 0, startAddress32 = 0;
                    startAddress16 = settingData[1].ADDRESS16;
                    startAddress24 = settingData[1].ADDRESS24;
                    startAddress32 = settingData[1].ADDRESS32;
                    for (startAddres16Index = 0; startAddres16Index < Constants.ADDRESS16_SIZE; startAddres16Index++)
                    {
                        if (startAddress16 == ADDRESS16_VAULT[startAddres16Index]) break;
                    }
                    for (startAddres24Index = 0; startAddres24Index < Constants.ADDRESS8_SIZE; startAddres24Index++)
                    {
                        if (startAddress24 == ADDRESS8_VAULT[startAddres24Index]) break;
                    }
                    for (startAddres32Index = 0; startAddres32Index < Constants.ADDRESS8_SIZE; startAddres32Index++)
                    {
                        if (startAddress32 == ADDRESS8_VAULT[startAddres32Index]) break;
                    }
                }
                settingData.RemoveRange(1, settingData.Count() - 1);
                int unit, str, relativeIndex;
                byte voltageLevelIndex, SMKVoltageLevelIndex;
                for (int i = 1; i < totalKit + 1; i++)
                {
                    settingData.Add(new SettingDataType() { BLOCK = 0, STRING = 0, UNIT = 0, ADDRESS16 = ADDRESS16_VAULT[0], ADDRESS24 = ADDRESS8_VAULT[0], ADDRESS32 = ADDRESS8_VAULT[0], ADDRESS40 = ADDRESS8_VAULT[0], R1 = (ushort)Constants.R_DEFFAULT[0, 0], R2 = (ushort)Constants.R_DEFFAULT[1, 0], VSCALE = Constants.VOLTAGE_SCALE[0] });
                }
                for (unit = 0; unit < Constants.MAXUNIT; unit++)
                {
                    if ((unitData[unit].TOTAL_BLOCK != 0) && (unitData[unit].TOTAL_STRING != 0))
                    {
                        int nbKitperStr = unitData[unit].TOTAL_BLOCK/ unitData[unit].TOTAL_STRING + 1;              // Plus SMK
                        voltageLevelIndex = unitData[unit].CONFIG_STRUCT.voltageLevel;
                        for (SMKVoltageLevelIndex = 0; SMKVoltageLevelIndex < Constants.SMK_VOLTAGE_LEVEL_SIZE; SMKVoltageLevelIndex++)
                            if ((nbKitperStr-1) * Constants.VOLTAGE_LEVEL_PROXY[voltageLevelIndex] / 1000 > Constants.SMK_VOLTAGE_LEVEL_PROXY[SMKVoltageLevelIndex]) break;
                        if (SMKVoltageLevelIndex >= Constants.SMK_VOLTAGE_LEVEL_SIZE) SMKVoltageLevelIndex = Constants.SMK_VOLTAGE_LEVEL_SIZE - 1;
                        for (int index = unitData[unit].RECORD_INDEX; index < unitData[unit].RECORD_INDEX + unitData[unit].TOTAL_BLOCK + unitData[unit].TOTAL_STRING; index++)
                        {
                            relativeIndex = index - unitData[unit].RECORD_INDEX;
                            str = relativeIndex / nbKitperStr;
                            
                            settingData[index].BLOCK = (byte)relativeIndex;
                            settingData[index].STRING = (byte)str;
                            settingData[index].UNIT = (byte)unit;
                            if (((relativeIndex + 1) % nbKitperStr) != 0)                 // BMK
                                {
                                settingData[index].ADDRESS16 = ADDRESS16_VAULT[(index - 1) + startAddres16Index];
                                settingData[index].ADDRESS24 = ADDRESS8_VAULT[startAddres24Index];
                                settingData[index].ADDRESS32 = ADDRESS8_VAULT[startAddres32Index];
                                settingData[index].ADDRESS40 = Constants.ADDRESS40_DEFAULT[voltageLevelIndex];

                                settingData[index].R1 = (ushort)Constants.R_DEFFAULT[0, voltageLevelIndex];
                                settingData[index].R2 = (ushort)Constants.R_DEFFAULT[1, voltageLevelIndex];
                                settingData[index].VSCALE = Constants.VOLTAGE_SCALE[voltageLevelIndex];
                                settingData[index].CONFIG_STRUCT.blkCapacity = unitData[unit].CONFIG_STRUCT.blkCapacity;
                                settingData[index].CONFIG_STRUCT.deviceType = Constants.BMK_TYPE;
                                settingData[index].CONFIG_STRUCT.cableType = unitData[unit].CONFIG_STRUCT.cableType;
                                settingData[index].CONFIG_STRUCT.enable = true;
                                settingData[index].CONFIG_STRUCT.operationMode = Constants.NORMAL_OP;
                                settingData[index].CONFIG_STRUCT.voltageLevel = unitData[unit].CONFIG_STRUCT.voltageLevel;
                                settingData[index].CONFIG_STRUCT.refreshDuration = unitData[unit].CONFIG_STRUCT.refreshDuration;
                            }
                            else
                            {
                                settingData[index].ADDRESS16 = ADDRESS16_VAULT[str];
                                settingData[index].ADDRESS24 = ADDRESS8_VAULT[0];
                                settingData[index].ADDRESS32 = ADDRESS8_VAULT[0];         
                                settingData[index].ADDRESS40 = Constants.SMK_ADDRESS40_DEFAULT[SMKVoltageLevelIndex];

                                settingData[index].R1 = (ushort)Constants.R_DEFFAULT[0, SMKVoltageLevelIndex];
                                settingData[index].R2 = (ushort)Constants.R_DEFFAULT[1, SMKVoltageLevelIndex];
                                settingData[index].VSCALE = Constants.SMK_VOLTAGE_SCALE[SMKVoltageLevelIndex];
                                settingData[index].CONFIG_STRUCT.blkCapacity = unitData[unit].CONFIG_STRUCT.blkCapacity;
                                settingData[index].CONFIG_STRUCT.deviceType = Constants.SMK_TYPE;
                                settingData[index].CONFIG_STRUCT.cableType = unitData[unit].CONFIG_STRUCT.cableType;
                                settingData[index].CONFIG_STRUCT.enable = true;
                                settingData[index].CONFIG_STRUCT.operationMode = Constants.NORMAL_OP;
                                settingData[index].CONFIG_STRUCT.voltageLevel = SMKVoltageLevelIndex;
                                settingData[index].CONFIG_STRUCT.refreshDuration = unitData[unit].CONFIG_STRUCT.refreshDuration;
                            }
                        }
                    }
                }
                saveSettingData();
            }
            for (int i = 0; i < totalKit + 1; i++)
            {
                measurementData.Add(new MeasurementDataType() { V0 = 0, V1 = 0, V2 = 0, T = 0, sV1 = 0, sV2 = 0, E = 0 });
                measurementData[i].STATUS_STRUCT.Status = 0;
                measurementVData.Add(new MeasurementDataVType() { V0 = 0, V1 = 0, V2 = 0, V3 = 0, V4 = 0, V5 = 0, V6 = 0 });
                measurementVData[i].STATUS_STRUCT.Status = 0;
                measurementLastVData.Add(new MeasurementDataVType() { V0 = 0, V1 = 0, V2 = 0, V3 = 0, V4 = 0, V5 = 0, V6 = 0 });
                measurementLastVData[i].STATUS_STRUCT.Status = 0;
                varianceData.Add(new List<VarianceDataType>());
                displayData.Add(new DisplayDataType() { V0 = 0, V1 = 0, V2 = 0, E = 0, R = 0, T = 0 });
                alarmData.Add(new AlarmDataType() { FromBegining = true, FromLastHour = false, Reported = true, NB_R_Upper = 0, NB_V_Upper = 0, NB_V_Lower = 0, NB_E_Upper = 0, NB_E_Lower = 0, NB_T_Upper = 0, NB_T_Lower = 0 });
                lastHourAlarmData.Add(new AlarmDataType() { FromBegining = true, FromLastHour = false, Reported = true, NB_R_Upper = 0, NB_V_Upper = 0, NB_V_Lower = 0, NB_E_Upper = 0, NB_E_Lower = 0, NB_T_Upper = 0, NB_T_Lower = 0 });
                readBackSettingData.Add(new SettingDataType() { BLOCK = 0, STRING = 0, UNIT = 0, ADDRESS16 = 0, ADDRESS24 = 0, ADDRESS32 = 0, ADDRESS40 = 0, R1 = 0, R2 = 0, VSCALE = 0 });
                readBackSettingData[i].CONFIG_STRUCT.Config = 0x0000;
                calibrationData.Add(new SettingDataType() { BLOCK = 0, STRING = 0, UNIT = 0, ADDRESS16 = 0, ADDRESS24 = 0, ADDRESS32 = 0, ADDRESS40 = 0, R1 = 0, R2 = 0, VSCALE = 0 });
                calibrationData[i].CONFIG_STRUCT.Config = 0x0000;
            }

            delayCountToEchoServer = Constants.DELAY_COUNT_ECHO_SERVER - 2;
            activeBlkList.Clear();
            beenActiveBlkList.Clear();

            // Culture & time zone setting
            CultureInfo.CurrentCulture = MainPage.cultureList[cultureIndex];

            // Header update
            Header.Text = Constants.HEADER + "- " + systemName;

            // HTTP server basic data transfer    
            server.nbKit = totalKit;
            server.nbKitperPage = nbKitperPage;
            server.nbPage = nbPage;
            server.DataPackageList = mDataPackageList;

            // Timer settting
            dispatcher = Window.Current.Dispatcher;
            ptimer.Tick += DispatcherTimer_Tick;
            ptimer.Interval = TimeSpan.FromSeconds(Constants.PAGE_REFRESH_INTERVAL);

            prequestdataEvent.Tick += RequestDataTimer_Tick;

            // Calculate for appropriate refresh duration . Refresh duration must be higher than two time of nbPage*Constants.PAGE_REFRESH_INTERVAL
            minRefreshDurationIndex = 0;
            while (REFRESH_DURATION_PROXY[minRefreshDurationIndex] * 60 <= 2 * nbPage * Constants.PAGE_REFRESH_INTERVAL)
            {
                minRefreshDurationIndex++;
            }
            //            startupRefreshDuration = REFRESH_DURATION_PROXY[minRefreshDurationIndex];
            if (settingData[0].CONFIG_STRUCT.refreshDuration < minRefreshDurationIndex) settingData[0].CONFIG_STRUCT.refreshDuration = minRefreshDurationIndex;
            refreshDurationIndex = settingData[0].CONFIG_STRUCT.refreshDuration;                        // Backup the validated settinng value
                                                                                                        //            settingData[0].CONFIG_STRUCT.refreshDuration = minRefreshDurationIndex;
                                                                                                        //            systemRefreshDuration = REFRESH_DURATION_PROXY[minRefreshDurationIndex];                    // Minimum refresh duration applied
            systemRefreshDuration = REFRESH_DURATION_PROXY[refreshDurationIndex];                    // Setting refresh duration applied
            systemRefreshDurationInSecond = systemRefreshDuration * 60;
            nbRefreshingThreshold = systemRefreshDurationInSecond / Constants.SYSTEM_REFRESH_DURATION;
            progressBarRefreshCycle.Maximum = Constants.SYSTEM_REFRESH_DURATION;
            progressBarUpdateCycle.Maximum = systemRefreshDurationInSecond;

            nbDataRefreshed = 0;
            totalNbRefreshing = 0;
            //            nbDataRefreshed = (ushort)((int)systemRefreshDuration * 60 / Constants.SYSTEM_REFRESH_DURATION);
            //            nbDataRefreshed += 1;

            for (int i = 1; i < totalKit + 1; i++)
            {
                settingData[i].CONFIG_STRUCT.refreshDuration = settingData[0].CONFIG_STRUCT.refreshDuration;
            }

            prequestdataEvent.Interval = TimeSpan.FromSeconds(Constants.SYSTEM_REFRESH_DURATION);
            // Initializing SPI service 
            pspiMaster = new SPIRaspberry();
            pspiMaster.nbPage = nbPage;
            pspiMaster.OnReceivedAll += a_OnReceivedAll;
            pspiMaster.measurementData = measurementData;
            pspiMaster.displayData = displayData;
            pspiMaster.settingData = settingData;


            // Initializing disk space scanner
            sdFreeSpace = await GetFreeSpace();
            freeSpacePerCentage = sdFreeSpace / Constants.SD_CAPACITY;
            txt_SDFreeSpace.Text = freeSpacePerCentage.ToString("N1") + " %";

            // Initializing Flags
            ProcessFlags.startupFinished = false;
            ProcessFlags.transferDataError = false;
            ProcessFlags.dataRefreshRequest = false;
            ProcessFlags.updateDataAvailable = false;
            ProcessFlags.settingDataAvailable = false;
            ProcessFlags.calibDataAvailable = false;
            ProcessFlags.enumDataAvailable = false;
            ProcessFlags.settingDataError = false;
            ProcessFlags.settingDataModified = false;
            ProcessFlags.dataDispatchingRequired = false;
            ProcessFlags.dataProcessingRequired = false;
            ProcessFlags.alarmProcessingRequired = false;
            ProcessFlags.pivotUpdateRequired = false;
            ProcessFlags.TCPProcessingRequired = false;
            ProcessFlags.submitToSendEmail = false;
            ProcessFlags.sendEmailRequestOnNewDay = false;
            ProcessFlags.serverEchoInProgress = false;

            // Initialize reference data     
            InitializeReferenceData();

            // Initialize process     
            await InitializeProcess();

            // Initialize dynamic control items
            InitializeControlItems();

            // Update first information
            //            currentMsg = Constants.SETTING_MSG;                              // The first msg is SETTING MSG
            //            nextMsg = Constants.SETTING_MSG;
            currentMsg = Constants.UPDATE_MSG;                              // The first msg is SETTING MSG
            nextMsg = Constants.UPDATE_MSG;
            currentMonitorPage = 1;

            processMessage = "";

            //            scenario0_Monitor.MonitorUpdate(displayData, currentMonitorPage);

            ProcessFlags.startupFinished = true;
            isInternetNeeded = true;
            internetLostCount = Constants.INTERNET_CHECK_CYCLE;
            // Start Request Data timer
            //            ProcessFlags.dataRefreshRequest = true;
            ptimer.Start();
            prequestdataEvent.Start();
        }
        public async Task InitializeProcess()
        {
            Debug.WriteLine("Initialize MQTT");
            //TCPServerAddress = commSettingData.TCP_IP3 + "." + commSettingData.TCP_IP2 + "." + commSettingData.TCP_IP1 + "." + commSettingData.TCP_IP0;
            //tcpClient = new TCPClient();
            mqttClientInstance = new MQTTClient();
            mqttClientInstance.MqttInitialize();

            dataProcess = new DataProcess();
            /*
                        WIFIAccessStat = await WiFiAdapter.RequestAccessAsync();
                        if (WIFIAccessStat == WiFiAccessStatus.Allowed)
                        {
                            var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
                            if (result.Count >= 1)
                            {
                                firstAdapter = await WiFiAdapter.FromIdAsync(result[0].Id);
                            }
                            else
                            {
                                firstAdapter = null;
                            }
                        }
                        internetAvailable = IsInternetService(ref wifiSignal, ref internetSSID);
            */

//            await InitializeWiFi();
        }
        public async Task InitializeWiFi()
        {
            firstAdapter = null;
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                NotifyUser("Access denied", NotifyType.ErrorMessage);
            }
            else
            {
                DataContext = this;
                var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
                if (result.Count >= 1)
                {
                    firstAdapter = await WiFiAdapter.FromIdAsync(result[0].Id);
                }
                else
                {
                    NotifyUser("No WiFi Adapters detected on this machine.", NotifyType.ErrorMessage);
                }
            }
            if (firstAdapter != null)
            {
                try
                {
                    await firstAdapter.ScanAsync();
                }
                catch (Exception err)
                {
                    NotifyUser(String.Format("Error scanning WiFi adapter: 0x{0:X}: {1}", err.HResult, err.Message), NotifyType.ErrorMessage);
                    return;
                }
                DisplayNetworkReport(firstAdapter.NetworkReport);

                DoWifiConnect();
            }
        }
        public string GetCurrentWifiNetwork()
        {
            var connectionProfiles = NetworkInformation.GetConnectionProfiles();

            if (connectionProfiles.Count < 1)
            {
                return null;
            }

            var validProfiles = connectionProfiles.Where(profile =>
            {
                return (profile.IsWlanConnectionProfile && profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None);
            });

            if (validProfiles.Count() < 1)
            {
                return null;
            }

            ConnectionProfile firstProfile = validProfiles.First();

            return firstProfile.ProfileName;
        }
        public ConnectionProfile GetCurrentWifiProfile()
        {
            var connectionProfiles = NetworkInformation.GetConnectionProfiles();

            if (connectionProfiles.Count < 1)
            {
                return null;
            }

            var validProfiles = connectionProfiles.Where(profile =>
            {
                return (profile.IsWlanConnectionProfile && profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None);
            });

            if (validProfiles.Count() < 1)
            {
                return null;
            }

            ConnectionProfile firstProfile = validProfiles.First();
            
            return firstProfile;
        }

        private bool IsConnected(WiFiAvailableNetwork network)
        {
            if (network == null)
                return false;

            string profileName = GetCurrentWifiNetwork();
            if (!String.IsNullOrEmpty(network.Ssid) &&
                !String.IsNullOrEmpty(profileName) &&
                (network.Ssid == profileName))
            {
                return true;
            }

            return false;
        }
        public void DisplayNetworkReport(WiFiNetworkReport report)
        {
            NotifyUser(string.Format("Network Report Timestamp: {0}", report.Timestamp), NotifyType.StatusMessage);

            ResultCollection.Clear();
            ConcurrentDictionary<string, WiFiNetworkDisplay> dictionary = new ConcurrentDictionary<string, WiFiNetworkDisplay>();

            foreach (var network in report.AvailableNetworks)
            {
                var item = new WiFiNetworkDisplay(network, firstAdapter);
                if (!String.IsNullOrEmpty(network.Ssid))
                {
                    dictionary.TryAdd(network.Ssid, item);
                }
                else
                {
                    string bssid = network.Bssid.Substring(0, network.Bssid.LastIndexOf(":"));
                    dictionary.TryAdd(bssid, item);
                }
            }

            var values = dictionary.Values;
            foreach (var item in values)
            {
                item.Update();
                if (IsConnected(item.AvailableNetwork))
                {
                    ResultCollection.Insert(0, item);
                }
                else
                {
                    ResultCollection.Add(item);
                }
            }
        }
        private async void DoWifiConnect()
        {
            //            var selectedNetwork = ResultCollection[0] as WiFiNetworkDisplay;
            foreach (WiFiNetworkDisplay selectedNetwork in ResultCollection)
            {
                if (selectedNetwork == null || firstAdapter == null)
                {
                    NotifyUser("Network not selected", NotifyType.ErrorMessage);
                    return;
                }

                var ssid = selectedNetwork.AvailableNetwork.Ssid;
                if (string.IsNullOrEmpty(ssid))
                {
                    if (string.IsNullOrEmpty(selectedNetwork.HiddenSsid))
                    {
                        NotifyUser("Ssid required for connection to hidden network.", NotifyType.ErrorMessage);
                        return;
                    }
                    else
                    {
                        ssid = selectedNetwork.HiddenSsid;
                    }
                }

                WiFiReconnectionKind reconnectionKind = WiFiReconnectionKind.Manual;
                if (selectedNetwork.ConnectAutomatically)
                {
                    reconnectionKind = WiFiReconnectionKind.Automatic;
                }

                Task<WiFiConnectionResult> didConnect = null;
                WiFiConnectionResult result = null;
                {
                    PasswordCredential credential = new PasswordCredential();
                    if (selectedNetwork.IsEapAvailable && selectedNetwork.UsePassword)
                    {
                        if (!String.IsNullOrEmpty(selectedNetwork.Domain))
                        {
                            credential.Resource = selectedNetwork.Domain;
                        }

                        credential.UserName = selectedNetwork.UserName ?? "";
                        credential.Password = selectedNetwork.Password ?? "";
                    }
                    else if (!String.IsNullOrEmpty(selectedNetwork.Password))
                    {
                        credential.Password = selectedNetwork.Password;
                    }

                    if (selectedNetwork.IsHiddenNetwork)
                    {
                        // Hidden networks require the SSID to be supplied
                        didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, credential, ssid).AsTask();
                    }
                    else
                    {
                        didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, credential).AsTask();
                    }
                }
                if (didConnect != null)
                {
                    result = await didConnect;
                }

                if (result != null && result.ConnectionStatus == WiFiConnectionStatus.Success)
                {
                    NotifyUser(string.Format("Successfully connected to {0}.", selectedNetwork.Ssid), NotifyType.StatusMessage);
                    internetAvailable = true;
                    wifiSignal = selectedNetwork.AvailableNetwork.SignalBars;
                    internetSSID = selectedNetwork.Ssid;
                    ResultCollection.Remove(selectedNetwork);
                    ResultCollection.Insert(0, selectedNetwork);
                    break;
                }
                else
                {
                    // Entering the wrong password may cause connection attempts to timeout
                    // Disconnecting the adapter will return it to a non-busy state
                    if (result.ConnectionStatus == WiFiConnectionStatus.Timeout)
                    {
                        firstAdapter.Disconnect();
                    }
                    NotifyUser(string.Format("Could not connect to {0}. Error: {1}", selectedNetwork.Ssid, (result != null ? result.ConnectionStatus : WiFiConnectionStatus.UnspecifiedFailure)), NotifyType.ErrorMessage);
                }
            }

            // Since a connection attempt was made, update the connectivity level displayed for each
            foreach (var network in ResultCollection)
            {
                var task = network.UpdateConnectivityLevelAsync();
            }
        }
        private async void ReConnectWiFi(WiFiNetworkDisplay selectedNetwork)
        {
            var ssid = selectedNetwork.AvailableNetwork.Ssid;

            WiFiReconnectionKind reconnectionKind = WiFiReconnectionKind.Manual;
            if (selectedNetwork.ConnectAutomatically)
            {
                reconnectionKind = WiFiReconnectionKind.Automatic;
            }

            Task<WiFiConnectionResult> didConnect = null;
            WiFiConnectionResult result = null;
            PasswordCredential credential = new PasswordCredential();
            if (selectedNetwork.IsEapAvailable && selectedNetwork.UsePassword)
            {
                if (!String.IsNullOrEmpty(selectedNetwork.Domain))
                {
                    credential.Resource = selectedNetwork.Domain;
                }

                credential.UserName = selectedNetwork.UserName ?? "";
                credential.Password = selectedNetwork.Password ?? "";
            }
            else if (!String.IsNullOrEmpty(selectedNetwork.Password))
            {
                credential.Password = selectedNetwork.Password;
            }

            try
            {
                if (selectedNetwork.IsHiddenNetwork)
                {
                    // Hidden networks require the SSID to be supplied
                    didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, credential, ssid).AsTask();
                }
                else
                {
                    didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, credential).AsTask();
                }
                if (didConnect != null)
                {
                    result = await didConnect;
                }
            }
            catch
            {
                result = null;
                NotifyUser(string.Format("Reconnect to {0} unsuccessully.", selectedNetwork.Ssid), NotifyType.WarningMessage);
            }

            if (result != null && result.ConnectionStatus == WiFiConnectionStatus.Success)
            {
                NotifyUser(string.Format("Successfully connected to {0}.", selectedNetwork.Ssid), NotifyType.StatusMessage);
                internetAvailable = true;
                wifiSignal = selectedNetwork.AvailableNetwork.SignalBars;
                internetSSID = selectedNetwork.Ssid;
                ResultCollection.Remove(selectedNetwork);
                ResultCollection.Insert(0, selectedNetwork);
            }
            else
            {
                // Entering the wrong password may cause connection attempts to timeout
                // Disconnecting the adapter will return it to a non-busy state
                if (result != null)
                {
                    if (result.ConnectionStatus == WiFiConnectionStatus.Timeout)
                    {
                        firstAdapter.Disconnect();
                    }
                    NotifyUser(string.Format("Could not connect to {0}. Error: {1}", selectedNetwork.Ssid, (result != null ? result.ConnectionStatus : WiFiConnectionStatus.UnspecifiedFailure)), NotifyType.ErrorMessage);
                }
                internetAvailable = false;
                wifiSignal = 0;
                internetSSID = "";
            }
            var task = selectedNetwork.UpdateConnectivityLevelAsync();
        }
        
/*        private async void ReConnectWiFi(WiFiNetworkDisplay selectedNetwork)
        {
            Task<WiFiConnectionResult> didConnect = null;
            WiFiConnectionResult result = null;
            PasswordCredential credential = new PasswordCredential();
            if (selectedNetwork.IsEapAvailable && selectedNetwork.UsePassword)
            {
                if (!String.IsNullOrEmpty(selectedNetwork.Domain))
                {
                    credential.Resource = selectedNetwork.Domain;
                }

                credential.UserName = selectedNetwork.UserName ?? "";
                credential.Password = selectedNetwork.Password ?? "";
            }
            else if (!String.IsNullOrEmpty(selectedNetwork.Password))
            {
                credential.Password = selectedNetwork.Password;
            }
            didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, WiFiReconnectionKind.Automatic, credential).AsTask();
            if (didConnect != null)
            {
                result = await didConnect;
            }

            if (result != null && result.ConnectionStatus == WiFiConnectionStatus.Success)
            {
                NotifyUser(string.Format("Successfully connected to {0}.", selectedNetwork.Ssid), NotifyType.StatusMessage);
                internetAvailable = true;
                wifiSignal = selectedNetwork.AvailableNetwork.SignalBars;
                internetSSID = selectedNetwork.Ssid;
                ResultCollection.Remove(selectedNetwork);
                ResultCollection.Insert(0, selectedNetwork);
            }
            else
            {
                if (result.ConnectionStatus == WiFiConnectionStatus.Timeout)
                {
                    firstAdapter.Disconnect();
                }
                NotifyUser(string.Format("Could not connect to {0}. Error: {1}", selectedNetwork.Ssid, (result != null ? result.ConnectionStatus : WiFiConnectionStatus.UnspecifiedFailure)), NotifyType.ErrorMessage);
                internetAvailable = false;
                wifiSignal = 0;
                internetSSID = "";
            }
            var task = selectedNetwork.UpdateConnectivityLevelAsync();
        }
*/
        void InitializeReferenceData()
        {
            ResultCollection = new ObservableCollection<WiFiNetworkDisplay>();
            currentDate = lastDate = reportDate=DateTime.Now.Date;
            currentHour = lastHour = DateTime.Now.Hour;
            elapsedTime = 0;
            totalElapsedTime = 0;
            sessionDuration = (ushort)(systemRefreshDuration / Constants.NB_SESSION_SEQUENCE);
            MasterOn = 0;
            MasterOff = (ushort)(sessionDuration - Constants.SESSION_TIMING_GAP);
            LoraOn = (ushort)(MasterOff + Constants.SESSION_TIMING_GAP);
            LoraOff = (ushort)(LoraOn + 4 * sessionDuration - Constants.SESSION_TIMING_GAP);
            RFOn = (ushort)(LoraOff + Constants.SESSION_TIMING_GAP);
            RFOff = (ushort)(RFOn + sessionDuration - Constants.SESSION_TIMING_GAP);

            for (int i = 0; i < totalKit + 1; i++)                                       // Should be added system records as the first record 
            {
                ExtendedDataType pExtendedData = new ExtendedDataType();
                extendedDataList.Add(pExtendedData);
            }
            for (int unit = 0; unit < Constants.MAXUNIT; unit++)
            {
                int capacityIndex = unitData[unit].CONFIG_STRUCT.blkCapacity * 4 + unitData[unit].CONFIG_STRUCT.cableType;                // Consolidate blkCapacity(4bits) with cableType (2 bits) for up to 64 different capacities
                referenceR[unit] = ReferenceRCalcul(unitData[unit].CONFIG_STRUCT.voltageLevel, capacityIndex);
            }
            // Common combo box data source 
            blkSettingArray = new byte[Constants.MAXBLK_PER_UNIT];
            for (int unitIndex = 0; unitIndex < Constants.MAXUNIT; unitIndex++)
            {
                if ((unitData[unitIndex].TOTAL_BLOCK != 0) && (unitData[unitIndex].TOTAL_STRING != 0))
                {
                    byte nbBlkperStr = (byte)(unitData[unitIndex].TOTAL_BLOCK / unitData[unitIndex].TOTAL_STRING);
                    blkArray[unitIndex] = new byte[nbBlkperStr];
                    for (int blkIndex = 0; blkIndex < nbBlkperStr; blkIndex++)
                    {
                        blkSettingArray[blkIndex] = (byte)(blkIndex + 1);
                        blkArray[unitIndex][blkIndex] = (byte)(blkIndex + 1);
                    }
                    strArray[unitIndex] = new byte[unitData[unitIndex].TOTAL_STRING];
                    for (int strIndex = 0; strIndex < unitData[unitIndex].TOTAL_STRING; strIndex++)
                    {
                        strSettingArray[strIndex] = (byte)(strIndex + 1);
                        strArray[unitIndex][strIndex] = (byte)(strIndex + 1);
                    }
                    unitSettingArray[unitIndex] = (byte)(unitIndex + 1);
                    unitArray[unitIndex] = (byte)(unitIndex + 1);
                }
            };

            // Threshold initializing
            referenceT = Constants.STANDARD_TEMPERATURE;
            for (int unit = 0; unit < Constants.MAXUNIT; unit++)
            {
                for (int index = 0; index < Constants.THRESHOLD_SIZE; index++)
                {
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MAX, (int)DATA_INDEX.R, index] = (float)Math.Round(referenceR[unit] * (Constants.R_THESHOLD_OFFSET + Constants.R_THESHOLD_STEP * index), 2);        // Max range starts from 150% to 500% of reference value
                    int voltageLevelIndex = settingData[0].CONFIG_STRUCT.voltageLevel;
                    double ratioU = (1 + 0.01 * (index + 1)) / 1000;
                    double ratioL = (1 - 0.01 * (index + 1)) / 1000;
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MAX, (int)DATA_INDEX.V, index] = (float)Math.Round(FLOATING_VOLTAGE_LEVEL_PROXY[voltageLevelIndex] * ratioU, 2);       // Max range starts from 101% of reference value
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MIN, (int)DATA_INDEX.V, index] = (float)Math.Round(END_VOLTAGE_LEVEL_PROXY[voltageLevelIndex] * ratioL, 2);            // Min range starts from 99% of reference value
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MAX, (int)DATA_INDEX.E, index] = (float)Math.Round(OPEN_VOLTAGE_LEVEL_PROXY[voltageLevelIndex] * ratioU, 2);            // Max range starts from 101% of reference value
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MIN, (int)DATA_INDEX.E, index] = (float)Math.Round(OPEN_VOLTAGE_LEVEL_PROXY[voltageLevelIndex] * ratioL, 2);            // Min range starts from 99% of reference value
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MAX, (int)DATA_INDEX.T, index] = (float)((int)(referenceT + 1 + index));                                                // Max range starts from reference value plus 1 degree
                    thresholdAlarm[(int)unit, (int)RANGE_INDEX.MIN, (int)DATA_INDEX.T, index] = (float)((int)(referenceT - 1 - index));                                                // Min range starts from reference value minus 1 degree
                }
                for (int i = 0; i < Constants.VOLTAGE_LEVEL_SIZE; i++)
                {
                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MAX, i, (int)VOLTAGE_TYPE.OPEN] = (float)OPEN_VOLTAGE_LEVEL_PROXY[i] * 1.2f / 1000;
                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MIN, i, (int)VOLTAGE_TYPE.OPEN] = (float)OPEN_VOLTAGE_LEVEL_PROXY[i] * 0.8f / 1000;

                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MAX, i, (int)VOLTAGE_TYPE.FLOATING] = (float)FLOATING_VOLTAGE_LEVEL_PROXY[i] * 1.2f / 1000;
                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MIN, i, (int)VOLTAGE_TYPE.FLOATING] = (float)FLOATING_VOLTAGE_LEVEL_PROXY[i] * 0.8f / 1000;

                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MAX, i, (int)VOLTAGE_TYPE.BOOST] = (float)BOOST_VOLTAGE_LEVEL_PROXY[i] * 1.2f / 1000;
                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MIN, i, (int)VOLTAGE_TYPE.BOOST] = (float)BOOST_VOLTAGE_LEVEL_PROXY[i] * 0.8f / 1000;

                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MAX, i, (int)VOLTAGE_TYPE.END] = (float)END_VOLTAGE_LEVEL_PROXY[i] * 1.2f / 1000;
                    thresholdVoltage[(int)unit, (int)RANGE_INDEX.MIN, i, (int)VOLTAGE_TYPE.END] = (float)END_VOLTAGE_LEVEL_PROXY[i] * 0.8f / 1000;
                }
            }

        }
        private void InitializeControlItems()
        {
        }
        public double ReferenceRCalcul(int voltageLevelIndex, int capacityIndex)
        {
            double referenceR = 0;
            double capacity = CAPACITY_PROXY[voltageLevelIndex][capacityIndex];
            switch (voltageLevelIndex)
            {
                case (byte)VOLTAGE_LEVEL.LEVEL_12000:
                case (byte)VOLTAGE_LEVEL.LEVEL_6000:
                    referenceR = 66.746 * Math.Pow(capacity, -0.601);                                                               // [mOhm] according trend line of standard resitance given in BM.xlsx\Capacity   
                    break;
                case (byte)VOLTAGE_LEVEL.LEVEL_2000:
                    referenceR = 27.898 * Math.Pow(capacity, -0.647);                                                               // [mOhm] according trend line of standard resitance given in BM.xlsx\Capacity 
                    break;
                case (byte)VOLTAGE_LEVEL.LEVEL_1200:
                    referenceR = 285.8 / capacity;                                                                                  // [mOhm] according trend line of standard resitance given in BM.xlsx\Capacity
                    break;
            }
            return referenceR;
        }
        private void DataDispatch(ref int index, ref byte[] pack)  // Copy up to nbKitperPage ( 16 ) records , RECORDWIDTH (16) bytes each to pack at begining of pack . Adjust index ( currentBlk ) accordingly
        {
            byte[] record = new byte[Constants.RECORDWIDTH];
            int lenght;
            if (index + nbKitperPage < totalKit + 1) lenght = nbKitperPage;
            else lenght = totalKit + 1 - index;
            for (int i = index; i < index + lenght; i++)
            {
                if (i == 0)
                {
                    for (int j = 0; j < Constants.MAXUNIT; j++)
                        unitData[j].Copy(record, (byte)j);
                }
                else settingData[i].Copy(record);
                Array.Copy(record, 0, pack, (i - index) * Constants.RECORDWIDTH, Constants.RECORDWIDTH);
            }
            index += lenght;
        }

        // Called at first second after last page of SPI sesssion received ( ProcessFlags.updateDataAvailable & ProcessFlags.dataDispatchingRequired ) 
        public async Task DataDispatching()
        {
            Console.WriteLine("Data dispatching ");
            StoreData storeData = new StoreData();
            DateTime moment = DateTime.Now;
            var ticks = moment.Ticks;
            currentDate = DateTime.Now.Date;
            currentHour = DateTime.Now.Hour;
            lastTotalBlkPresented = totalBlkPresented;
            totalBlkPresented = 0;
            // Update active list & has_been_active list ( list of active blk during systemRefresh cycle )
            activeBlkList.RemoveAll(item => item <= totalKit);
            foreach (MeasurementDataType c in measurementData)
            {
                if (c.STATUS_STRUCT.presented)
                {
                    // Update active list & was_been_actived list
                    totalBlkPresented++;
                    if (!beenActiveBlkList.Exists(item => item == measurementData.IndexOf(c)))
                    {
                        beenActiveBlkList.Add(measurementData.IndexOf(c));
                        if (beenActiveBlkList.Count != 0) beenActiveBlkList.Sort();
                    }
                    activeBlkList.Add(measurementData.IndexOf(c));
                    if (currentHour != lastHour)                                        // Clear shorterm alarm data log
                    {
                        lastHourAlarmData[measurementData.IndexOf(c)].Clear();
                    }
                }
            }
            // Add recent data package to list
            if (recordMustBeSavedDueR)                      //  R data collected
            {
                MeasurementDataPackage mDataPackage = new MeasurementDataPackage();
                mDataPackage.timeTicks = ticks;
                mDataPackage.status = Constants.PACKAGE_STATUS_SENT;
                mDataPackage.Retrieve(displayData);
                mDataPackageList.Add(mDataPackage);
                outstandingQueue.Enqueue(mDataPackage);
                totalElapsedTime = 0;
                recordMustBeSent = true;
            }
            if (recordMustBeSavedDueV)                     // There is a variance of V during last rereshing cycle      
            {
                // Check for variance existing
                if (beenActiveBlkList.Count != 0)
                {
                    int nbHasVariance = 0;
                    int maxNbVariance = 0;
                    int indexHasMaxVariance = 0;
                    foreach (int i in beenActiveBlkList)
                    {
                        if (varianceData[i].Count != 0)
                        {
                            nbHasVariance++;
                            if (varianceData[i].Count > maxNbVariance)
                            {
                                maxNbVariance = varianceData[i].Count;
                                indexHasMaxVariance = i;
                            }
                        }
                    }
                    if (nbHasVariance > 0)
                    {
                        for (int j = 0; j < maxNbVariance; j++)
                        {
                            MeasurementDataPackage mDataPackage = new MeasurementDataPackage();
                            DateTime tempMoment = moment;
                            tempMoment.AddSeconds(-Constants.SYSTEM_REFRESH_DURATION + varianceData[indexHasMaxVariance][j].time);
                            mDataPackage.timeTicks = tempMoment.Ticks;
                            mDataPackage.status = Constants.PACKAGE_STATUS_SENT;
                            mDataPackage.Retrieve(displayData);
                            int unit, str, blk;
                            unit = str = blk = 0;
                            foreach (int k in beenActiveBlkList)
                            {
                                try
                                {
                                    if (varianceData[k].Count > j)
                                        mDataPackage.displayData[k].V0 = varianceData[k][j].V * (float)settingData[k].VSCALE / 10000 / 1000;
                                    if (varianceData[k][j].isDischarging) extendedDataList[k].TotalDischargeSecond++;
                                    if (varianceData[k][j].isDischarging && varianceData[k][j].isDecreasing) extendedDataList[k].TotalDischargeCycle++;
                                }
                                catch (Exception ep)
                                {
                                    String message = ep.Message;
                                    processMessage = String.Format("Failed to register the voltage variation with the following error: {0}", ep.Message);
                                }
                                GetId(unitData,k, ref unit, ref str, ref blk);
                                float rootLife = (float)(((mDataPackage.displayData[k].R / referenceR[unit] - 1) / Constants.LIFE_COEF));
                                float designLife = Constants.LIFE_EXPECTANCY[unitData[unit].CONFIG_STRUCT.operationMode];
                                extendedDataList[k].ExpectedLifeTimeRest = designLife - rootLife * rootLife;
                                extendedDataList[k].ExpectedLifeTimeRest -= designLife * extendedDataList[k].TotalDischargeSecond / 60 / 60 / Constants.LIFE_CYCLE;
                                if (extendedDataList[k].ExpectedLifeTimeRest < 0) extendedDataList[k].ExpectedLifeTimeRest = 0;
                            }
                            mDataPackageList.Add(mDataPackage);
                            outstandingQueue.Enqueue(mDataPackage);
                            recordMustBeSent = true;
                        }
                    }
                }
            }
            if (beenActiveBlkList.Count != 0)
            {
                // Check for saving data of last day or intermitent request 
                if (ProcessFlags.submitToSendEmail)
                {
                    if (internetAvailable)
                    {
                        ProcessFlags.submitToSendEmail = false;
                        if (ProcessFlags.sendEmailRequestOnNewDay)
                        {
                            ProcessFlags.sendEmailRequestOnNewDay = false;
                            delayTimeToSendEmail = delayTimeToSendEmailMax = 0;
                            ProcessFlags.sendEmailSuccessfully=await SendEmailAsync(dataPath, reportDate);
                            if (!ProcessFlags.sendEmailSuccessfully)
                            {
                                nbRetryToSendEmail++;
                                if (nbRetryToSendEmail < Constants.NB_RETRY_MAX)
                                {
                                    ProcessFlags.sendEmailRequestOnNewDay = true;
                                    delayTimeToSendEmailMax = Constants.DELAY_TIME_EMAILING_MAX;
                                }
                                else nbRetryToSendEmail = 0;
                            }
                        }
                        else
                        {
                            string name = await storeData.SaveCSV(mDataPackageList, beenActiveBlkList, alarmData);
                            ProcessFlags.sendEmailSuccessfully=await SendEmailAsync(name, lastDate);
                        }
                    }
                    else
                    {
                        if (uart3G.UartPort != null)
                        {
                            if (uart3G.validDbName == string.Empty)
                            {
                                string name = await storeData.SaveCSV(mDataPackageList, beenActiveBlkList, alarmData);
                                ProcessFlags.sendEmailSuccessfully=await SendEmailAsync(name, lastDate);
                            }
                            else
                            {
                                ProcessFlags.sendEmailSuccessfully=await SendEmailAsync(uart3G.validDbName, uart3G.DateValue);
                            }
                            ProcessFlags.submitToSendEmail = false;
                        }
                    }

                }
                if (currentDate.Day != lastDate.Day)
                {
                    reportDate = lastDate;
                    lastDate = currentDate;
                    ProcessFlags.sendEmailRequestOnNewDay = true;
                    Random random = new Random();
                    delayTimeToSendEmailMax = random.Next(1, Constants.DELAY_TIME_EMAILING_MAX);
                    dataPath = await storeData.SaveCSV(mDataPackageList, beenActiveBlkList, alarmData);
                    mDataPackageList.Clear();
                }
                if (totalNbRefreshing == Constants.NB_REFRESH_STARTUP)
                {
                    for (int i = 1; i < totalKit + 1; i++)
                    {
                        settingData[i].CONFIG_STRUCT.refreshDuration = refreshDurationIndex;            // Restore validate backup refresh duration index 
                    }
                    nextMsg = Constants.SETTING_MSG;
                }
                if (totalNbRefreshing == (Constants.NB_REFRESH_STARTUP + 1))
                {
                    settingData[0].CONFIG_STRUCT.refreshDuration = refreshDurationIndex;
                    systemRefreshDuration = REFRESH_DURATION_PROXY[settingData[0].CONFIG_STRUCT.refreshDuration];
                    //                    prequestdataEvent.Interval = TimeSpan.FromSeconds(systemRefreshDuration);
                    nextMsg = Constants.SETTING_MSG;
                }
            }
            if (recordMustBeSavedDueR)
            {
                beenActiveBlkList.Clear();
                foreach (List<VarianceDataType> varianceKitData in varianceData)
                    varianceKitData.Clear();
            }
            recordMustBeSavedDueR = recordMustBeSavedDueV = false;
        }
        // Proccess called once ReceivedAll event coming
        private async Task ProcessAsync()
        {
            watch.Restart();
            ProcessFlags.updateDataAvailable = false;
   
            // Store , manage the pre processing data from SPI service
            await DataDispatching();
            // Process the list of data package by evaluating , refreshing the extended data records , statistical data calculation 
            dataProcess.DataPackageProcessing(mDataPackageList);
            // Check for alarms 
            if (mDataPackageList.Count != 0) dataProcess.AlarmCheck(mDataPackageList.Last(), measurementData, settingData);
            // Refreshing the pivot screens
            Scenario s = ScenarioControl.SelectedItem as Scenario;
            switch (s.Id)
            {
                case SELWINDOWS.SELWINDOWS_MONITOR:
                    if (mDataPackageList.Count != 0) scenario0_Monitor.MonitorUpdate(mDataPackageList.Last().displayData, 0);
                    break;
                case SELWINDOWS.SELWINDOWS_GRAPH:
                    scenario1_Graph.GraphUpdate();
                    break;
                case SELWINDOWS.SELWINDOWS_EVENTS:
                    scenario2_Events.EventUpdate();
                    break;
                case SELWINDOWS.SELWINDOWS_ALARMS:
                    scenario3_Alarms.AlarmUpdate();
                    break;
                case SELWINDOWS.SELWINDOWS_SETTINGS:
                    break;
                case SELWINDOWS.SELWINDOWS_COMMUNICATION:
                    break;
            }
            UpdateStatus();

            // Sending the data to server   
            if (outstandingQueue.Count() != 0)
            {
                await mqttClientInstance.MqttPublishPayload();

                // publish payload to cloud
                await mqttClient.ReportStatus();
                await mqttClient.ReportInfo();
            }


            if (outstandingQueue.Count() == 0)
            {
                ProcessFlags.TCPProcessingRequired = false; // MQTT too
                isInternetNeeded = false;
            }

            // Sending data by means of 3G 
            //if ((uart3G.UartPort != null) && uart3G.Module3GReady)
            //{
            //    if (mDataPackageList.Count > 0)
            //    {
            //        if (uart3G.replyRequest) uart3G.SendHeader();
            //        else if (uart3G.replyHeaderSent) await uart3G.SendContent(systemName, settingData, commSettingData, mDataPackageList.Last(), activeBlkList, alarmData);
            //        else if (uart3G.replyResetRequest && ProcessFlags.submitToReset) CloseApp();
            //    }
            //}
            // Wait for all finished
            await Task.Yield();

            watch.Stop();
            systemElapsedTime = watch.ElapsedMilliseconds;
        }

        private void DispatcherTimer_Tick(object sender, object e)
        {
            // Update date and time
            DateTime dt = DateTime.Now;
//            if(currentTZ!=null) dt = TimeZoneInfo.ConvertTime(dt, currentTZ);
            txt_date.Text = "" + dt.Date.ToString("d", cultureList[cultureIndex]);
            txt_time.Text = "" + dt.ToString("HH:mm:ss", cultureList[cultureIndex]);
//            txt_date.Text = dt.ToString("dd/MM/yyyy");
//            txt_time.Text = dt.ToString("HH:mm:ss");

            elapsedTime++;
            totalElapsedTime++;
            if (elapsedTime > Constants.SYSTEM_REFRESH_DURATION) elapsedTime = 0;
            if (totalElapsedTime > systemRefreshDurationInSecond)
            {
                elapsedTime = 0;
                totalElapsedTime = 0;
            }
            if (ProcessFlags.refreshDurationChanged)
            {
                progressBarUpdateCycle.Maximum = systemRefreshDuration * 60;
                ProcessFlags.refreshDurationChanged = false;
            }
            progressBarRefreshCycle.Value = elapsedTime;
            progressBarUpdateCycle.Value = totalElapsedTime;

            if (ProcessFlags.sendEmailRequestOnNewDay) delayTimeToSendEmail++;
            if (delayTimeToSendEmail > delayTimeToSendEmailMax) ProcessFlags.submitToSendEmail = true;
        }
        private void RequestDataTimer_Tick(object sender, object e)
        {
            // Process will be done at period of Constants.SYSTEM_REFRESH_DURAION ( 6 seconds per default ) 
            // -set CALIB mode to synchonizing the sensor clock at begining of R measurement cycle defined in systemRefreshDuration ( minimum 10 minutes )  
            // -Master SPI communication to transfer all update setting data to PIC . It comprises of MasterNbPage page with 16 kits each 
            // -During proceding , the update timer of 1 second shall be stopped . 

            // Initializing
            elapsedTime = 0;
            if (nbDataRefreshed == 0)                                    // Synchonizing the moment to measure internal resistance  of each block
            {
                for (int index = 1; index < totalKit + 1; index++)
                {
                    settingData[index].CONFIG_STRUCT.operationMode = Constants.CALIB_OP;
                }
            }
            if (nbDataRefreshed == 1)
            {
                for (int index = 1; index < totalKit + 1; index++)
                {
                    settingData[index].CONFIG_STRUCT.operationMode = Constants.NORMAL_OP;
                }
            }
            // SPI data refreshing
            _ = SPIProcessAsync();
            // Internet availability checck
            //            _=IntenetServerCheckAsync();
            // Update 3G status
            _ = Mobile3GProcessAsync();
            // Refresh the storage space status 
            _ = SDCheckAsync();
            // Ending
            delayCountToEchoServer++;
            if (delayCountToEchoServer > Constants.DELAY_COUNT_ECHO_SERVER)
            {
                delayCountToEchoServer = 0;
                _ = IntenetServerCheckAsync();
            }

             
/*
            if (!ProcessFlags.serverEchoInProgress)
            {
                delayCountToEchoServer = 0;
                _ = IntenetServerCheckAsync();
            }
            else
            {
                delayCountToEchoServer++;
                if (delayCountToEchoServer > Constants.DELAY_COUNT_ECHO_SERVER)
                {
                    delayCountToEchoServer = 0;
                    ProcessFlags.serverEchoInProgress = false;
                }
            }
*/
            /*
                        if (isInternetNeeded)
                        {
                            isInternetNeeded = false;
                            _ = IntenetServerCheckAsync();
                        }
            */
            nbDataRefreshed++;
            if (nbDataRefreshed >= nbRefreshingThreshold) nbDataRefreshed = 0;
            totalNbRefreshing++;
        }
        private async Task SPIProcessAsync()
        {
            currentIndex = 0;
            ProcessFlags.dataRefreshRequest = true;
            ProcessFlags.updateDataAvailable = false;
            for (currentPage = 0; currentPage < nbPage; currentPage++)
            {
                DataDispatch(ref currentIndex, ref dataPack);                                           // Setting data will be packed or sending via SPI session      
                byte msg = (byte)(currentMsg + (nextMsg << 4));
                pspiMaster.WritePackage(pspiMaster.DataPackage(msg, currentPage, dataPack));            // DataPack including of nbKitperPage records that currently dispatched 
                await System.Threading.Tasks.Task.Delay(2);
            }
            ProcessFlags.dataRefreshRequest = false;
        }
        private async Task Mobile3GProcessAsync()
        {
            if (uart3G.UartPort != null)
            {
                if ((uart3G.UartPort != null) && !uart3G.UARTStartingReceive)
                {
                    uart3G.StartReceive();
                    uart3G.UARTStartingReceive = true;
                }
                if (!uart3G.Module3GInitialized)
                {
                    if (!uart3G.Module3GInitializing) uart3G.Initialise3GModule();
                }
                send3GResult = await uart3G.CheckFor3GStatus();
                send3GResult = await uart3G.SendCommand("AT+CSQ");
                mobileSignal = (byte)(uart3G.rssi * 5 / 31);
                if ((mobileSignal >= 0) && (mobileSignal <= 4))
                {
                    if (!uart3G.TCPInitialized) await uart3G.SendCommand("AT+QIOPEN=1,0,\"TCP\",\"" + TCPServerAddress + "\"," + commSettingData.TCP_Port.ToString() + ",0,0");  // Contex 1 Connect ID 0 Direct push mode 
                    btn_3G.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri(this.BaseUri, "Assets/3G" + (mobileSignal).ToString() + ".png")), Stretch = Stretch.Uniform };
                }
                txt_3G_Signal.Text = uart3G.rssi.ToString() + " dBm";
            }
        }
        private async Task SDCheckAsync()
        {
            sdFreeSpace = await GetFreeSpace();
            freeSpacePerCentage = sdFreeSpace / Constants.SD_CAPACITY;
            txt_SDFreeSpace.Text = freeSpacePerCentage.ToString("N1") + " %";
        }   
        public async Task IntenetServerCheckAsync()
        {
            // Check server connected status
            serverConnected = await IsServerConnectedAsync();
            if (!serverConnected)
            {
                NotifyUser("Server not connected ", NotifyType.ErrorMessage);
                internetAvailable = false;
                internetSSID = "";
                wifiSignal = 0;
            }
            else
            {
                internetAvailable = true;
                wifiSignal = 4;
            }
            if ((wifiSignal >= 0) && (wifiSignal <= 4))
            {
                if (serverConnected)
                    btn_Wifi.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri(this.BaseUri, "Assets/Wifi" + (wifiSignal).ToString() + ".png")), Stretch = Stretch.Uniform };
                else
                    btn_Wifi.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri(this.BaseUri, "Assets/Wifi" + (wifiSignal).ToString() + "R.png")), Stretch = Stretch.Uniform };
            }
        }

        void AllDisplays(Visibility b, int index)
        {
            Border blockInfo = (Border)FindName("blockInfo" + index);
            blockInfo.Visibility = b;
        }
        private async static Task<WiFiConnectionResult> CheckAdapter()
        {
            Task<WiFiConnectionResult> didConnect = null;
            WiFiConnectionResult connectionResult = null;
            WiFiReconnectionKind reconnectionKind = WiFiReconnectionKind.Automatic;
            PasswordCredential credential = new PasswordCredential();
            List<WiFiAvailableNetwork> networkList = new List<WiFiAvailableNetwork>();
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                if (firstAdapter != null)
                {
                    await firstAdapter.ScanAsync();
                    WiFiNetworkReport report = firstAdapter.NetworkReport;
                    foreach (var network in report.AvailableNetworks)
                    {
                        networkList.Add(network);
                    }
                    //                    credential.UserName = "TRIEN";
                    //                    credential.Password = "trien22032203";
                    didConnect = firstAdapter.ConnectAsync(networkList[0], reconnectionKind, credential).AsTask();
                    if (didConnect != null)
                    {
                        connectionResult = await didConnect;
                    }
                }
            }
            return connectionResult;
        }
        public async Task<bool> IsServerConnectedAsync()
        {
            string echoSend = "abc";
            string echoBack = "";
            ProcessFlags.serverEchoInProgress = true;
            try
            {
                //echoBack = echoSend;
                echoBack = await bsm.echoAsync(echoSend);
            }
            catch
            {
                NotifyUser("Not able to echo server ", NotifyType.ErrorMessage);
            }
            ProcessFlags.serverEchoInProgress = false;
            if (echoBack == echoSend) return true;
            return false;
        }

        public static bool IsInternetService(ref byte signal, ref string ssid)
        {
            ConnectionProfile connections = NetworkInformation.GetInternetConnectionProfile();
            bool internet = (connections != null) && (connections.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess);
            signal = 0;
            ssid = "";
            /*
                                    if (!internet)
                                    {
                                        internetRefreshCount++;
                                        if (internetRefreshCount > 1)
                                        {
                                            internetRefreshCount = 0;
                                            var result = Task.Run(() => CheckAdapter()).Result;
                                            if (result != null && result.ConnectionStatus == WiFiConnectionStatus.Success)
                                                internet = true;
                                        }
                                    }
            */
            if (internet == true)
            {
                internetRefreshCount = 0;
                if (connections.IsWlanConnectionProfile)
                {
                    signal = (byte)connections.GetSignalBars();
                    ssid = connections.WlanConnectionProfileDetails.GetConnectedSsid();
                }
            }
            return internet;
        }

        private async Task<bool> SendEmailAsync(String path, DateTime dateTime)
        {
            processMessage = "";
            try
            {
                SmtpMail oMail = new SmtpMail("TryIt");
                SmtpClient oSmtp = new SmtpClient();
                // Set sender email address, please change it to yours
                oMail.From = new MailAddress(commSettingData.SMTPConfig.user);
                // oMail.From = new MailAddress("bmsdc272@gmail.com");
                //                oMail.From = new MailAddress("bms_opy1@saigonetc.com");

                // Add recipient email address, please change it to yours
                //                oMail.To.Add(new MailAddress("nktuan63@gmail.com"));
                //                oMail.Cc.Add(new MailAddress("trungkien@binhson.com"));
                //                oMail.Bcc.Add(new MailAddress("quanghung@binhson.com"));
                oMail.To.Add(new MailAddress(commSettingData.EmailList[0].address));
                oMail.Cc.Add(new MailAddress(commSettingData.EmailList[1].address));
                oMail.Bcc.Add(new MailAddress(commSettingData.EmailList[2].address));

                // Set email subject and email body text
                oMail.Subject = "BMS report " + dateTime.Date.ToString("d", cultureList[cultureIndex]) + " from site " + systemName;
                oMail.TextBody = "Please refer to attachment for log file of the day of " + dateTime.Date.ToString("d", cultureList[cultureIndex]) + " from site " + systemName + ".\n";
                oMail.TextBody += "Sent on " + DateTime.Now.ToString("HH:mm:ss", cultureList[cultureIndex]) + " dated " + DateTime.Now.Date.ToString("d", cultureList[cultureIndex]);
                await oMail.AddAttachmentAsync(path);
                // Your SMTP server address
                SmtpServer oServer = new SmtpServer(commSettingData.SMTPConfig.server);
                //                SmtpServer oServer = new SmtpServer("smtp.gmail.com");
                //                SmtpServer oServer = new SmtpServer("mail.saigonetc.com");

                // User and password for ESMTP authentication
                oServer.User = commSettingData.SMTPConfig.user;
                oServer.Password = commSettingData.SMTPConfig.password;
                //  oServer.User = "bmsdc272@gmail.com";
                //  oServer.Password = "bms#123456789";
                //  oServer.Password = "tuan@2003";
                //  oServer.User = "bms_opy1@saigonetc.com";
                //  oServer.Password = "Giamsataccu19";

                // SMTP server requires SSL connection on 465 port, please add this line
                oServer.Port = commSettingData.SMTPConfig.port;
                //                oServer.Port = 465;
                //                oServer.Port = 110;
                //                oServer.ConnectType = SmtpConnectType.ConnectSSLAuto;
                oServer.ConnectType = SmtpConnectType.ConnectNormal;
                oServer.ConnectType = (SmtpConnectType)commSettingData.SMTPConfig.connect;
                await oSmtp.SendMailAsync(oServer, oMail);
                processMessage = "Email was sent successfully!";
                return true; 
            }
            catch (Exception ep)
            {
                String message = ep.Message;
                processMessage = String.Format("Failed to send email with the following error: {0}", ep.Message);
                return false;
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Populate the scenario list from the SampleConfiguration.cs file
            ScenarioControl.ItemsSource = scenarios;
            if (Window.Current.Bounds.Width < 640)
            {
                ScenarioControl.SelectedIndex = -1;
            }
            else
            {
                ScenarioControl.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Called whenever the user changes selection in the scenarios list.  This method will navigate to the respective
        /// sample scenario page.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScenarioControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear the status block when navigating scenarios.
            NotifyUser(String.Empty, NotifyType.StatusMessage);

            ListBox scenarioListBox = sender as ListBox;
            Scenario s = scenarioListBox.SelectedItem as Scenario;
            if (s != null)
            {
                if (s.ClassType == typeof(Scenario0_Monitor))
                {
                    ScenarioFrame.Navigate(s.ClassType);
                    //                    ScenarioFrame.Navigate(s.ClassType, "TestString");
                }
                else
                    ScenarioFrame.Navigate(s.ClassType);
                //               selectedWindow = s.Id;
                if (Window.Current.Bounds.Width < 640)
                {
                    Splitter.IsPaneOpen = false;
                }
            }
        }

        public List<Scenario> Scenarios
        {
            get { return this.scenarios; }
        }

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatusPanel(strMessage, type);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatusPanel(strMessage, type));
            }
        }
        public void UpdateStatus()
        {
            NotifyType type;
            if (lastTotalBlkPresented >= totalBlkPresented) type = NotifyType.StatusMessage;
            else type = NotifyType.WarningMessage;
            string message = "";
            if (processMessage != "")
            {
                message = processMessage;
                processMessage = "";
            }
            else
            {
                message = " Refresh of " + totalBlkPresented + " blocks successfully at total of " + totalNbRefreshing + " times . ";
                if (mDataPackageList.Count == 0)
                {
                    message += "No record saved yet.";
                }
                else if (mDataPackageList.Count == 1)
                {
                    message += "One record saved .";
                }
                else
                {
                    message += mDataPackageList.Count + " records saved .";
                }
            }
            UpdateStatusPanel(message, type);
        }
        public void UpdateStatusPanel(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.WarningMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.GreenYellow);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }

            // Raise an event if necessary to enable a screen reader to announce the status update.
            var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        async void Footer_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(((HyperlinkButton)sender).Tag.ToString()));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }
        /*        public void GetId(int index, ref int unit, ref int str, ref int blk)
                {
                    int relativeIndex;
                    int nbBlkperStr;
                    for (unit = Constants.LOCAL_UNIT_ID; unit < Constants.MAXUNIT; unit++)
                        if (index < unitData[unit].RECORD_INDEX + unitData[unit].TOTAL_BLOCK + unitData[unit].TOTAL_STRING) break;
                    relativeIndex = index - unitData[unit].RECORD_INDEX;
                    nbBlkperStr = unitData[unit].TOTAL_BLOCK / unitData[unit].TOTAL_STRING;
                    str = relativeIndex / nbBlkperStr;
                    if (str >= unitData[unit].TOTAL_STRING)
                    {
                        str = relativeIndex - unitData[unit].TOTAL_BLOCK;
                    }
                    blk = relativeIndex;
                }
        */
        public void GetId(List<UnitDataType> unitData,int index, ref int unit, ref int str, ref int blk)
        {
            int relativeIndex;
            for (unit = Constants.LOCAL_UNIT_ID; unit < Constants.MAXUNIT; unit++)
                if (index < unitData[unit].RECORD_INDEX + unitData[unit].TOTAL_BLOCK + unitData[unit].TOTAL_STRING) break;
            relativeIndex = index - unitData[unit].RECORD_INDEX;
            int nbKitperStr = unitData[unit].TOTAL_BLOCK / unitData[unit].TOTAL_STRING + 1;
            str = relativeIndex / nbKitperStr;
            blk = relativeIndex- nbKitperStr*str;
        }
        public int GetKitRecord(List<UnitDataType> unitData,int unit, int str, int blk)
        {
            return (unitData[unit].RECORD_INDEX + blk + (unitData[unit].TOTAL_BLOCK/ unitData[unit].TOTAL_STRING + 1)*str);
        }
        public int GetStrRecord(List<UnitDataType> unitData,int unit, int str)
        {
            return (unitData[unit].RECORD_INDEX + (unitData[unit].TOTAL_BLOCK/ unitData[unit].TOTAL_STRING + 1)*(str+1)-1);
        }

        public AlarmDataType TotalAlarm(List<AlarmDataType> alarmList, List<int> activeList)
        {
            AlarmDataType totalAlarm = new AlarmDataType();
            totalAlarm.Clear();
            foreach (int index in activeList)
            {
                totalAlarm.NB_R_Upper += alarmList[index].NB_R_Upper;
                totalAlarm.NB_V_Upper += alarmList[index].NB_V_Upper;
                totalAlarm.NB_V_Lower += alarmList[index].NB_V_Lower;
                totalAlarm.NB_E_Upper += alarmList[index].NB_E_Upper;
                totalAlarm.NB_E_Lower += alarmList[index].NB_E_Lower;
                totalAlarm.NB_T_Upper += alarmList[index].NB_T_Upper;
                totalAlarm.NB_T_Lower += alarmList[index].NB_T_Lower;
            }
            return totalAlarm;
        }
        public async void CloseApp()
        {
            if (STATUSpin.Read() == GpioPinValue.Low)                           // if 3G module alives
            {
                PWRUPpin = gpio.OpenPin(Constants.PWRUP_3G_PIN);
                PWRUPpin.Write(GpioPinValue.High);
                PWRUPpin.SetDriveMode(GpioPinDriveMode.Output);
                await Task.Delay(TimeSpan.FromMilliseconds(700));
                PWRUPpin.Write(GpioPinValue.Low);
            }
            CoreApplication.Exit();
        }
    }
    public enum NotifyType
    {
        StatusMessage,
        WarningMessage,
        ErrorMessage
    };

    public class ScenarioBindingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Scenario s = value as Scenario;
            return (MainPage.Current.Scenarios.IndexOf(s) + 1) + ") " + s.Title;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }


}
