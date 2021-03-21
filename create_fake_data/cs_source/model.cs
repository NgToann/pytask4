using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BMSNameSpace
{
    public class DeviceModel
    {
        public string uuid { get; set; }
        public string macAddress { get; set; }
    }
    public class MQTTCredential
    {
        public string Endpoint { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
        public string ClientId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class DeviceInfo
    {
        //public string CloudVersion { get; set; }
        //public string LocalVersion { get; set; }
        //public string SystemId { get; set; }
        public string date { get; set; }
        public SystemInfo System { get; set; }
        public List<UnitInfo> Units { get; set; }
        public List<StringInfo> Strings { get; set; }
        public List<BlockInfo> Blocks { get; set; }
        //public long TimeStamp { get; set; }
    }

    public class SystemPayload
    {
        public string Date { get; set; }
        public SystemInfo System { get; set; }
    }
    public class SystemInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Culture { get; set; }
        public int TimeZone { get; set; }
        public int Block { get; set; }
        public int String { get; set; }
        public int Unit { get; set; }
        public int R1 { get; set; }
        public int R2 { get; set; }
        public int VScale { get; set; }
        public AddressInfo Address { get; set; }
        public ConfigInfo Config { get; set; }
        public AlarmInfo Alarm { get; set; }
    }

    public class AddressInfo
    {
        public int Address16 { get; set; }
        public int Address24 { get; set; }
        public int Address32 { get; set; }
        public int Address40 { get; set; }
    }

    public class UnitInfo
    {
        public string Name { get; set; }
        public int TotalBlock { get; set; }
        public int TotalString { get; set; }
        public int Address { get; set; }
        public int BlkCapacity { get; set; }
        public int RefreshDuration { get; set; }
        public int AdjustIndex { get; set; }
        public ConfigInfo Config { get; set; }
    }

    public class StringInfo
    {
        public string Id { get; set; }
        public string UnitId { get; set; }
        public string Name { get; set; }
        public int Address { get; set; }
        public int BlkCapacity { get; set; }
        public bool Enable { get; set; }
        public int OperationMode { get; set; }
        public int RefreshDuration { get; set; }
        public int VoltageLevel { get; set; }
        public int DeviceType { get; set; }
        public int CableType { get; set; }
        public int Capacity { get; set; }
    }

    public class BlockInfo
    {
        public int BlockId { get; set; }
        public int StringId { get; set; }
        public int UnitId { get; set; }
        public int R1 { get; set; }
        public int R2 { get; set; }
        public int VScale { get; set; }
        public AddressInfo Address { get; set; }
        public ConfigInfo Config { get; set; }
    }

    public class ConfigInfo
    {
        public bool Enable { get; set; }
        public int OperationMode { get; set; }
        public int RefreshDuration { get; set; }
        public int VoltageLevel { get; set; }
        public int DeviceType { get; set; }
        public int CableType { get; set; }
        public int Capacity { get; set; }

    }
    public class AlarmInfo
    {
        public int RUpper { get; set; }
        public int VUpper { get; set; }
        public int VLower { get; set; }
        public int EUpper { get; set; }
        public int ELower { get; set; }
        public int TUpper { get; set; }
        public int TLower { get; set; }

    }
    public class Topic
    {
        public string Info { get; set; }
        public string Status { get; set; }
    }

    public class DeviceStatus
    {
        public SystemStatus System { get; set; }
        public List<BlockStatus> Blocks { get; set; }
        public long TimeStamp { get; set; }
    }
    public class SystemStatus
    {
        public string Id { get; set; }
        public int R1 { get; set; }
        public int R2 { get; set; }
        public int VScale { get; set; }
    }
    public class BlockStatus
    {
        public string UnitId { get; set; }
        public string StringId { get; set; }
        public string MacAddress { get; set; }
        public bool Enable { get; set; }
        public int R1 { get; set; }
        public int R2 { get; set; }
        public float V0 { get; set; }
        public float V1 { get; set; }
        public float V2 { get; set; }
        public float E { get; set; }
        public float R { get; set; }
        public float T { get; set; }
        public float SOC { get; set; }
        public int TotalDischargeSecond { get; set; }
        public int TotalDischargeCycle { get; set; }
        public float ExpectedLifeTimeRest { get; set; }
        public int VScale { get; set; }
        public int Address16 { get; set; }
        public int Address24 { get; set; }
        public int Address32 { get; set; }
        public int Address40 { get; set; }
        public int OperationMode { get; set; }
        public int RefreshDuration { get; set; }
        public int VoltageLevel { get; set; }
        public int DeviceType { get; set; }
        public int CableType { get; set; }
        public int Capacity { get; set; }

    }
    public class JsonHardcodeModel
    {
        public string StringId { get; set; }
        public List<UnitHardcodeModel> UnitIds { get; set; }
    }
    public class UnitHardcodeModel
    {
        public int Address { get; set; }
        public string Id { get; set; }
    }
}

