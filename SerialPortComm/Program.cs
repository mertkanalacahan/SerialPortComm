using System;
using System.IO.Ports;
using System.Threading;
using System.Xml;

public class SerialPortCommApp
{
    static bool _continue;
    static SerialPort _serialPort;

    public static void Main()
    {
        string name;
        string message;
        StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;
        XmlDocument config = new XmlDocument();
        Thread readThread = new Thread(Read);

        config.Load("conf.xml");

        // Create a new SerialPort object with default settings.
        _serialPort = new SerialPort();

        XmlNodeList portName = config.GetElementsByTagName("portName");
        XmlNodeList baudRate = config.GetElementsByTagName("baudRate");
        XmlNodeList parity = config.GetElementsByTagName("parity");
        XmlNodeList dataBits = config.GetElementsByTagName("dataBits");
        XmlNodeList stopBits = config.GetElementsByTagName("stopBits");
        XmlNodeList handshake = config.GetElementsByTagName("handshake");

        // Set the appropriate properties.
        _serialPort.PortName = SetPortName(portName[0].InnerText);
        _serialPort.BaudRate = SetPortBaudRate(baudRate[0].InnerText);
        _serialPort.Parity = SetPortParity(parity[0].InnerText);
        _serialPort.DataBits = SetPortDataBits(dataBits[0].InnerText);
        _serialPort.StopBits = SetPortStopBits(stopBits[0].InnerText);
        _serialPort.Handshake = SetPortHandshake(handshake[0].InnerText);

        // Set the read/write timeouts
        _serialPort.ReadTimeout = 500;
        _serialPort.WriteTimeout = 500;

        _serialPort.Open();
        _continue = true;
        readThread.Start();

        Console.Write("Name: ");
        name = Console.ReadLine();

        Console.WriteLine("Type QUIT to exit");

        while (_continue)
        {
            message = Console.ReadLine();

            if (stringComparer.Equals("quit", message))
            {
                _continue = false;
            }
            else
            {
                _serialPort.WriteLine(
                    String.Format("<{0}>: {1}", name, message));
            }
        }

        readThread.Join();
        _serialPort.Close();
    }

    public static void Read()
    {
        while (_continue)
        {
            try
            {
                string message = _serialPort.ReadLine();
                Console.WriteLine(message);
            }
            catch (TimeoutException) { }
        }
    }

    // Display Port values and prompt user to enter a port.
    public static string SetPortName(string portName)
    {
        if (portName == "" || !(portName.ToLower()).StartsWith("com"))
        {
            portName = "COM1";
        }

        return portName;
    }
    // Display BaudRate values and prompt user to enter a value.
    public static int SetPortBaudRate(string baudRate)
    {
        if (baudRate == "")
        {
            baudRate = "9600";
        }

        return int.Parse(baudRate);
    }

    // Display PortParity values and prompt user to enter a value.
    public static Parity SetPortParity(string parity)
    {
        if (parity == "")
        {
            parity = "None";
        }

        return (Parity)Enum.Parse(typeof(Parity), parity, true);
    }
    // Display DataBits values and prompt user to enter a value.
    public static int SetPortDataBits(string dataBits)
    {
        if (dataBits == "")
        {
            dataBits = "8";
        }

        return int.Parse(dataBits.ToUpperInvariant());
    }

    // Display StopBits values and prompt user to enter a value.
    public static StopBits SetPortStopBits(string stopBits)
    {
        if (stopBits == "")
        {
            stopBits = "One";
        }

        return (StopBits)Enum.Parse(typeof(StopBits), stopBits, true);
    }
    public static Handshake SetPortHandshake(string handshake)
    {
        if (handshake == "")
        {
            handshake = "None";
        }

        return (Handshake)Enum.Parse(typeof(Handshake), handshake, true);
    }
}