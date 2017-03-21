using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using MTConnectSharp;
using System;
using System.Linq;
using System.Text;

namespace MTConnectAgent
{
    /// <summary>
    /// This class get the data blob URLs from IoT hub, captures the data from MTConnect agent and uploads it to blobs.
    /// </summary>
    class DataCollector
    {
        private string machineName;
        private string iotHubConnectionString;
        private string baseURL;
        private string sessionId;
        private int sampleInterval;
        private int recordCount;
        private string actualMachine;
        private StringBuilder sbMsg;
        private DeviceClient deviceClient;
        private MTConnectClient client;
        private int rowCount = 1;

        public DataCollector(string machineName, string ioTHubConnectionString, string baseURL, int sampleInterval, int recordCount, string actualMachine)
        {
            this.machineName = machineName;
            this.iotHubConnectionString = ioTHubConnectionString;
            this.baseURL = baseURL;
            this.sampleInterval = sampleInterval;
            this.recordCount = recordCount;
            this.actualMachine = actualMachine;

            // Unique session id which will be the prefix for blob names.
            sessionId = machineName + "_" + DateTime.Now.ToString().Replace(' ', '_').Replace('/', '_').Replace(':', '_');

            sbMsg = new StringBuilder();
            sbMsg.AppendLine("sessionid,recordid,machineid,name,value,sequence,recordtimestamp,sessionrowcount");
        }

        public void Init()
        {
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            var device = registryManager.GetDeviceAsync(machineName).Result;
            if (device == null)
            {
                device = registryManager.AddDeviceAsync(new Microsoft.Azure.Devices.Device(machineName)).Result;
            }

            string deviceConnStr = string.Format("{0};DeviceId={1};SharedAccessKey={2}",
                iotHubConnectionString.Split(new char[] { ';' }).Where(m => m.Contains("HostName")).FirstOrDefault(),
                device.Id, device.Authentication.SymmetricKey.PrimaryKey);

            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnStr, TransportType.Http1);

            Console.WriteLine();
            Console.WriteLine("Starting data collection for machine: " + machineName);

            // Initialize an instance of MTConnectClient
            client = new MTConnectClient(baseURL + actualMachine, recordCount);
            // Register for events
            client.ProbeCompleted += client_ProbeCompleted;
            client.DataItemChanged += client_DataItemChanged;
            client.DataItemsChanged += client_DataItemsChanged;
            client.UpdateInterval = sampleInterval;
            client.Probe();
        }

        #region MTConnect Client Event Handlers

        void client_DataItemsChanged(object sender, EventArgs e)
        {
            var msg = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(sbMsg.ToString()));
            msg.MessageId = Guid.NewGuid().ToString();
            deviceClient.SendEventAsync(msg);
            sbMsg.Clear();
            sbMsg.AppendLine("sessionid,recordid,machineid,name,value,sequence,recordtimestamp,sessionrowcount");
        }

        void client_DataItemChanged(object sender, DataItemChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.DataItem.Name))
                sbMsg.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}", sessionId, Guid.NewGuid().ToString(), machineName,
                    e.DataItem.Name, e.DataItem.CurrentSample.Value, e.DataItem.CurrentSample.Sequence, e.DataItem.CurrentSample.TimeStamp, rowCount));
        }

        void client_ProbeCompleted(object sender, EventArgs e)
        {
            var client = sender as MTConnectClient;
            client.StartStreaming();
        }

        #endregion
    }
}
