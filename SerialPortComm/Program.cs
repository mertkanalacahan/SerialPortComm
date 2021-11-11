using System;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Xml;
using System.Collections.Generic;

public class SerialPortCommApp
{
    static bool _continue;
    static SerialPort _serialPort;

    public static void Main()
    {
        string command;
        StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;
        XmlDocument config = new XmlDocument();
        Thread readThread = new Thread(Read);

        config.Load("conf.xml");

        // Create a new SerialPort object with default settings.
        _serialPort = new SerialPort();
        _serialPort.Encoding = Encoding.GetEncoding(28591);

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

        while (_continue)
        {
            command = Console.ReadLine();

            if (stringComparer.Equals("quit", command))
            {
                _continue = false;
            }
            else if (stringComparer.Equals("CRC_OK", command))
            {
                //“CRC_OK” girmesi durumunda Tablo 2’de tanımlı olan hazır mesajı uygun CRC ile 
                //alıcıya gönderecektir.
                //Sunucu tarafı gönderilen ve alınan herbir mesajı aşağıdaki formatlarda ekrana basacaktır.
            }
            else if (stringComparer.Equals("CRC_ER", command))
            {
                //“CRC_ER” girmesi durumunda Tablo 2’de tanımlı olan hazır mesajı hatalı CRC ile 
                //alıcıya gönderecektir.
                //Sunucu tarafı gönderilen ve alınan herbir mesajı aşağıdaki formatlarda ekrana basacaktır.
            }
            else
            {
                _serialPort.WriteLine(String.Format("{0}", command));
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
                byte[] incomingBytes = Encoding.GetEncoding(28591).GetBytes(message);

                if (incomingBytes.Length > 0)
                {
                    if (incomingBytes[2] == 0xA5)
                    {
                        Console.WriteLine("gecersiz istek");
                    }
                    else
                    {
                        //create error message
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA);
                        bytes.Add(0x05);
                        bytes.Add(0xA5);
                        bytes.Add(0x02);
                        ushort crc = CalculateCRC(bytes.ToArray());
                        bytes.Add((byte)(crc >> 8));
                        bytes.Add((byte)(crc));

                        //send message
                        _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
                    }
                }
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
    private static ushort CalculateCRC(byte[] data)
    {
        ushort wCRC = 0;
        for (int i = 0; i < data.Length; i++)
        {
            wCRC ^= (ushort)(data[i] << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((wCRC & 0x8000) != 0)
                    wCRC = (ushort)((wCRC << 1) ^ 0x1021);
                else
                    wCRC <<= 1;
            }
        }
        return wCRC;
    }
}