using System;
using System.Collections.Generic;
using System.Linq;
using RestSharp;
using System.Xml.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;
using System.Net.Http;

namespace MTConnectSharp
{
	/// <summary>
	/// Connects to a single agent and streams data from it.
	/// </summary>
	[ComVisible(true)]
	[ClassInterface(ClassInterfaceType.None)]
	[ComSourceInterfaces(typeof(IClientEvents))]
    public class MTConnectClient : IMTConnectClient
    {
		/// <summary>
		/// The probe response has been recieved and parsed
		/// </summary>
		public event EventHandler ProbeCompleted;

		/// <summary>
		/// All data items in a current or sample response have been parsed
		/// </summary>
		public event EventHandler DataItemsChanged;

		/// <summary>
		/// The value of a data item changed
		/// </summary>
		public event EventHandler<DataItemChangedEventArgs> DataItemChanged;

		/// <summary>
		/// The base uri of the agent
		/// </summary>
		public string AgentUri { get; set; }

		/// <summary>
		/// Time in milliseconds between sample queries when simulating a streaming connection
		/// </summary>
		public Int32 UpdateInterval { get; set; }

		/// <summary>
		/// Devices on the connected agent
		/// </summary>
		public Device[] Devices
		{
			get
			{
				return devices.ToArray<Device>();
			}
		}
		private List<Device> devices;
        private bool isFirstRun;

        /// <summary>
        /// Dictionary Reference to all data items by id for better performance when streaming
        /// </summary>
        private Dictionary<String, DataItem> dataItemsRef = new Dictionary<string,DataItem>(); 
		
		/// <summary>
		/// RestSharp RestClient
		/// </summary>
		private RestClient restClient;
		
		/// <summary>
		/// Last sequence number read from current or sample
		/// </summary>
		private Int64 lastSequence;

        /// <summary>
        /// Next sequence number to read from current or sample
        /// </summary>
        private Int64 nextSequence;

        /// <summary>
        /// Response instance id
        /// </summary>
        private Int64 instanceId;

        private Boolean probeCompleted = false;

        private bool stopStreaming = false;

        private int recordCount;

        /// <summary>
        /// Initializes a new Client 
        /// </summary>
        public MTConnectClient()
		{
			//UpdateInterval = (20 * 1000);
            isFirstRun = true;
		}

        /// <summary>
        /// Initializes a new Client and connects to the agent
        /// </summary>
        /// <param name="agentUri">The base uri of the agent</param>
        /// <param name="targetTags">The requird XML tags</param>
        public MTConnectClient(String agentUri, int RecordCount) : this()
		{
            restClient = new RestClient();
            restClient.BaseUrl = new Uri(agentUri);

			AgentUri = agentUri;
            this.recordCount = RecordCount;
		}

		public void StartStreaming()
		{
            while (true) 
            {
                if (!stopStreaming)
                {
                    GetData();
                    Thread.Sleep(UpdateInterval);
                }
            }
        }

		/// <summary>
		/// Gets current response and updates DataItems
		/// </summary>
		public void GetCurrentState()
		{
			if (!probeCompleted)
			{
				throw new InvalidOperationException("Cannot get DataItem values. Agent has not been probed yet.");
			}
            Console.WriteLine("Probe data has been processed for the machine at Uri: {0}", AgentUri);
            Console.WriteLine();

            var request = new RestRequest("current", Method.GET);
            var response = restClient.Execute(request);
            parseStream(response);
            Console.WriteLine("Current data has been processed for the machine at Uri: {0}", AgentUri);
            Console.WriteLine();
        }

		/// <summary>
		/// Gets probe response from the agent and populates the devices collection
		/// </summary>
		public void Probe()
		{
            var request = new RestRequest("probe", Method.GET);
            var response = restClient.Execute(request);
            parseProbeResponse(response);
		}

		/// <summary>
		/// Parses IRestResponse from a probe command into a Device collection
		/// </summary>
		/// <param name="response">An IRestResponse from a probe command</param>
		private void parseProbeResponse(IRestResponse response)
		{
			devices = new List<Device>();			
			XDocument xDoc = XDocument.Load(new StringReader(response.Content));
			foreach (var d in xDoc.Descendants().First(d => d.Name.LocalName == "Devices").Elements())
			{
				devices.Add(new Device(d));
			}
			FillDataItemRefList();

			probeCompleted = true;
			ProbeCompletedHandler();
            
        }
        
		/// <summary>
		/// Loads DataItemRefList with all data items from all devices
		/// </summary>
		private void FillDataItemRefList()
		{
			foreach (Device device in devices)
			{
				List<DataItem> dataItems = new List<DataItem>();
				dataItems.AddRange(device.DataItems);
				dataItems.AddRange(GetDataItems(device.Components));
				foreach (var dataItem in dataItems)
				{
					dataItemsRef.Add(dataItem.id, dataItem);
				}
			}
		}

		/// <summary>
		/// Recursive function to get DataItems list from a Component collection
		/// </summary>
		/// <param name="Components">Collection of Components</param>
		/// <returns>Collection of DataItems from passed Component collection</returns>
		private List<DataItem> GetDataItems(Component[] Components)
		{
			var dataItems = new List<DataItem>();

			foreach (var component in Components)
			{
				dataItems.AddRange(component.DataItems);
				if (component.Components.Length > 0)
				{
					dataItems.AddRange(GetDataItems(component.Components));
				}
			}
			return dataItems;
		}

        private void GetData()
        {
            var request = new RestRequest(isFirstRun ? "current" : "sample", Method.GET);

            if (!isFirstRun)
            {
                request.AddParameter("from", nextSequence);
                request.AddParameter("count", recordCount);
            }
            else 
            {
                isFirstRun = false;
            }
            
            var response = restClient.Execute(request);
            parseStream(response);

        }

		/// <summary>
		/// Parses response from a current or sample request, updates changed data items and fires events
		/// </summary>
		/// <param name="response">IRestResponse from the MTConnect request</param>
		private void parseStream(IRestResponse response)
		{
            String xmlContent = response.Content;
			using (StringReader sr = new StringReader(xmlContent))
			{
                XDocument xDoc = null;
                try
                {
                    xDoc = XDocument.Load(sr);
                }
                catch
                {
                    return;
                }
                try
                {
                    var curInstanceId = Convert.ToInt64(xDoc.Descendants().First(e => e.Name.LocalName == "Header").Attribute("instanceId").Value);
                    if (instanceId != 0 && curInstanceId != instanceId)
                    {
                        isFirstRun = true;
                        instanceId = 0;
                        GetData();
                        return;
                    }
                    else if (instanceId == 0)
                        instanceId = curInstanceId;
                    lastSequence = Convert.ToInt64(xDoc.Descendants().First(e => e.Name.LocalName == "Header").Attribute("lastSequence").Value);
                    if (xDoc.Descendants().First(e => e.Name.LocalName == "Header").Attribute("nextSequence").Value != null)
                        nextSequence = Convert.ToInt64(xDoc.Descendants().First(e => e.Name.LocalName == "Header").Attribute("nextSequence").Value);
                }
                catch
                {
                    lastSequence = nextSequence;
                }
                if (xDoc.Descendants().Any(e => e.Attributes().Any(a => a.Name.LocalName == "dataItemId")))
				{
					IEnumerable<XElement> xmlDataItems = xDoc.Descendants()
						.Where(e => e.Attributes().Any(a => a.Name.LocalName == "dataItemId"));
                    
					var dataItems = (from e in xmlDataItems
									 select new
									 {
										 id = e.Attribute("dataItemId").Value,
                                         timestamp = e.Attribute("timestamp").Value,
                                         value = e.Value,
                                         sequence = e.Attribute("sequence").Value
                                     }).ToList();

					foreach (var item in dataItems.Where(m => !string.IsNullOrEmpty(m.id) 
                        && !string.IsNullOrEmpty(m.value) ).OrderBy(i => i.timestamp))
					{
						var dataItem = dataItemsRef[item.id];
                        var ts = item.timestamp.Replace('T', ' ').Remove(item.timestamp.Length - 1);
						dataItem.AddSample(new DataItemSample(item.value.ToString(), DateTime.Parse(ts), item.sequence));
                        
						DataItemChangedHandler(dataItemsRef[item.id]);
					}
					DataItemsChangedHandler();
				}
			}
		}

		private void ProbeCompletedHandler()
		{
			var args = new EventArgs();
			if (ProbeCompleted != null)
			{
				ProbeCompleted(this, args);
			}
		}

		private void DataItemChangedHandler(DataItem dataItem)
		{
            var args = new DataItemChangedEventArgs(dataItem);
            DataItemChanged(this, args);
		}

		private void DataItemsChangedHandler()
		{
			var args = new EventArgs();
			if (DataItemsChanged != null)
			{
				DataItemsChanged(this, args);
			}
		}

        public void StopStreaming()
        {
            stopStreaming = true;
        }

        /// <summary>
        /// Get list of devices the agent is currently streaming the data from.
        /// </summary>
        /// <param name="baseURL">URL for agent</param>
        /// <returns>List of names of devices</returns>
        public static List<string> GetDeviceList(string baseURL)
        {
            List<string> machines = new List<string>();
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(baseURL);

            HttpResponseMessage response = client.GetAsync("probe").Result;
            if (response.IsSuccessStatusCode)
            {
                var probe = response.Content.ReadAsStringAsync().Result;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(probe);
                XmlNodeList devices = doc.GetElementsByTagName("Device");

                foreach (XmlNode n in devices)
                {
                    machines.Add(n.Attributes["name"].Value.ToString());
                }
            }
            return machines;
        }
    }
}
