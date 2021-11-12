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
        XmlNodeList readTimeout = config.GetElementsByTagName("readTimeout");
        XmlNodeList writeTimeout = config.GetElementsByTagName("writeTimeout");

        // Set the appropriate properties
        _serialPort.PortName = SetPortName(portName[0].InnerText);
        _serialPort.BaudRate = SetPortBaudRate(baudRate[0].InnerText);
        _serialPort.Parity = SetPortParity(parity[0].InnerText);
        _serialPort.DataBits = SetPortDataBits(dataBits[0].InnerText);
        _serialPort.StopBits = SetPortStopBits(stopBits[0].InnerText);
        _serialPort.Handshake = SetPortHandshake(handshake[0].InnerText);
        _serialPort.ReadTimeout = SetReadTimeout(readTimeout[0].InnerText);
        _serialPort.WriteTimeout = SetWriteTimeout(writeTimeout[0].InnerText);

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
                List<byte> bytes = CreateByteListFromCommandFile("command1.xml");

                //calculate and add crc
                ushort crc = CalculateCRC(bytes.ToArray());
                bytes.Add((byte)(crc >> 8));
                bytes.Add((byte)(crc));

                //print message on screen

                _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
            }
            else if (stringComparer.Equals("CRC_ER", command))
            {
                //“CRC_ER” girmesi durumunda Tablo 2’de tanımlı olan hazır mesajı hatalı CRC ile 
                //alıcıya gönderecektir.
                //Sunucu tarafı gönderilen ve alınan herbir mesajı aşağıdaki formatlarda ekrana basacaktır.
                List<byte> bytes = CreateByteListFromCommandFile("command1.xml");

                //add wrong crc values
                ushort crc = CalculateCRC(bytes.ToArray());
                bytes.Add((byte)((crc / 2) >> 8));
                bytes.Add((byte)((crc / 4)));

                //print message on screen

                _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
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
                    //CRC check
                    List<byte> bytesMinusCRC = new List<byte>();
                    for (int i = 0; i < incomingBytes.Length - 2; i++)
                        bytesMinusCRC.Add(incomingBytes[i]);

                    ushort crcInMessage = (ushort)((incomingBytes[incomingBytes.Length - 2] << 8) 
                        + incomingBytes[incomingBytes.Length - 1]);

                    if (crcInMessage != CalculateCRC(bytesMinusCRC.ToArray()))
                    {
                        //create wrong crc error message
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA);
                        bytes.Add(0x05);
                        bytes.Add(0xA5);
                        bytes.Add(0x01); //wrong crc
                        ushort crc = CalculateCRC(bytes.ToArray());
                        bytes.Add((byte)(crc >> 8));
                        bytes.Add((byte)(crc));

                        //send message
                        _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
                    }
                    else if (incomingBytes[2] == 0xA5)
                    {
                        Console.WriteLine("gecersiz istek");
                        //yalnızca ekrana bas
                    }
                    else if (incomingBytes[2] == 0xA9)
                    {
                        Console.WriteLine("Komut 2");
                        //yalnızca ekrana bas
                    }
                    else if (incomingBytes[2] == 0xA8)
                    {
                        Console.WriteLine("Komut 1");
                    }
                    else
                    {
                        //create invalid request error message
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA);
                        bytes.Add(0x05);
                        bytes.Add(0xA5);
                        bytes.Add(0x02); //invalid request
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

    public static string SetPortName(string portName)
    {
        if (portName == "" || !(portName.ToLower()).StartsWith("com"))
        {
            portName = "COM1";
        }

        return portName;
    }

    public static int SetPortBaudRate(string baudRate)
    {
        if (baudRate == "")
        {
            baudRate = "9600";
        }

        return int.Parse(baudRate);
    }

    public static Parity SetPortParity(string parity)
    {
        if (parity == "")
        {
            parity = "None";
        }

        return (Parity)Enum.Parse(typeof(Parity), parity, true);
    }

    public static int SetPortDataBits(string dataBits)
    {
        if (dataBits == "")
        {
            dataBits = "8";
        }

        return int.Parse(dataBits.ToUpperInvariant());
    }

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

    public static int SetReadTimeout(string readTimeout)
    {
        if (readTimeout == "")
        {
            readTimeout = "500";
        }

        return int.Parse(readTimeout);
    }
    public static int SetWriteTimeout(string writeTimeout)
    {
        if (writeTimeout == "")
        {
            writeTimeout = "500";
        }

        return int.Parse(writeTimeout);
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

    private static List<byte> CreateByteListFromCommandFile(string filename)
    {
        XmlDocument command = new XmlDocument();
        command.Load(filename);

        XmlNodeList header = command.GetElementsByTagName("header");
        XmlNodeList msgLength = command.GetElementsByTagName("msgLength");
        XmlNodeList commandNo = command.GetElementsByTagName("commandNo");
        XmlNodeList byteData = command.GetElementsByTagName("UInt8");
        XmlNodeList intData = command.GetElementsByTagName("UInt32");

        byte headerByte = byte.Parse(header[0].InnerText);
        byte msgLengthByte = byte.Parse(msgLength[0].InnerText);
        byte commandNoByte = byte.Parse(commandNo[0].InnerText);
        byte byteDataByte = byte.Parse(byteData[0].InnerText);
        uint integer = uint.Parse(intData[0].InnerText);

        List<byte> bytes = new List<byte>();
        bytes.Add(headerByte);
        bytes.Add(msgLengthByte);
        bytes.Add(commandNoByte);
        bytes.Add(byteDataByte);

        byte[] intBytes = BitConverter.GetBytes(integer);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(intBytes);

        foreach (byte elem in intBytes)
            bytes.Add(elem);

        return bytes;
    }
}