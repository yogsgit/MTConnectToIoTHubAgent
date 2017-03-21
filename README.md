# MTConnectToIoTHubAgent
MTConnect data collector

This project is a part of the blog [Processing MTConnect data using Azure IoT hub, Stream Analytics, Azure Functions and Power BI live reports](https://codedesignetc.com/2017/03/21/processing-mtconnect-data-using-azure-iot-hub-stream-analytics-azure-functions-power-bi-streaming-datasets/)

The console application can:
- collect data from multiple CNC lathe machines in real-time and send it to an Azure IoT hub.
- collect data from agent.mtconnect.org, simulate multiple machines and sent data in real-time and send it to an Azure IoT hub.

MTConnect is a protocol designed for the exchange of data between shop floor equipment and software applications used for monitoring and data analysis. This gives rise to a possibility of having an internet of things architecture for shop floor machine data processing and further analysis. In this post, we will discuss and implement a possible architecture for ingesting data from multiple CNC lathe machines in an Azure IoT hub, process it with Azure Stream Analytics and use Azure Table Storage, Power BI streaming datasets and Azure Service Bus Queue as data sinks. Further, we will process queue messages using an Azure Function App.

