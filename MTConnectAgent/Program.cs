using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;
using MTConnectSharp;
using System.Threading;
using System.IO;
using System.Text;

namespace MTConnectAgent
{
    class Program
	{
        static void Main(string[] args)
		{
            DateTime m = DateTime.Now;
            DateTime n = DateTime.UtcNow;
            string errorLogPath = ConfigurationManager.AppSettings["ErrorLogPath"].ToString();
            if (!errorLogPath.EndsWith("/"))
                errorLogPath = errorLogPath + "/";
            string filename = errorLogPath + DateTime.Now.ToString().Replace(' ', '_').Replace('/', '_').Replace(':', '_') + ".txt";
            try
            {
                Init();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                
                if (!Directory.Exists(errorLogPath))
                    Directory.CreateDirectory(errorLogPath);
                StringBuilder error = new StringBuilder("******************************************************************************");
                error.Append(e.Message);
                error.Append("\r\n\r\n");
                error.Append(e.StackTrace);
                error.Append("******************************************************************************");

                File.WriteAllText(filename, error.ToString());
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Error details hav been written to the log file: {0}", filename);

                Console.ReadLine();
            }
        }

        static void Init()
        {
            string iotHubConnectionString = ConfigurationManager.AppSettings["IoTHubConnectionString"].ToString();
            string baseURL = ConfigurationManager.AppSettings["BaseURL"].ToString();
            int sampleInterval = Convert.ToInt32(ConfigurationManager.AppSettings["SampleInterval"]);
            int recordCount = Convert.ToInt32(ConfigurationManager.AppSettings["RecordCount"]);
            int noOfDevices = Convert.ToInt32(ConfigurationManager.AppSettings["NoOfDevices"]);

            Console.WriteLine("Getting the list of devices from the agent...");
            Console.WriteLine();
            List<string> machines = MTConnectClient.GetDeviceList(baseURL);

            Dictionary<string, Task> collectorTasks = new Dictionary<string, Task>();
            for (int i = 0; i < noOfDevices; i++)
            {
                var machineName = machines[0] + "-" + (i + 1).ToString();
                Task task = Task.Factory.StartNew(() =>    // Begin task
                {
                    new DataCollector(machineName, iotHubConnectionString, baseURL, sampleInterval, recordCount, machines[0]).Init();
                });
                collectorTasks.Add(machineName, task);
            }
            //Console.WriteLine("Press enter to stop streaming.");
            Console.ReadLine();
        }
    }
}
