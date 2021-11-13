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
        Thread readThread = new Thread(Read);
        XmlDocument config = new XmlDocument();

        //Load serial port configuration file
        config.Load("conf.xml");

        _serialPort = new SerialPort();
        //Encoding needs to be 28591 otherwise bytes larger than 127 are being set to 63
        _serialPort.Encoding = Encoding.GetEncoding(28591);

        //Get serial port settings from xml file
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

        //Open serial port and start read thread
        _serialPort.Open();
        _continue = true;
        readThread.Start();

        while (_continue)
        {
            //Read command from console
            command = Console.ReadLine();

            //If user types "quit" then exit loop
            if (stringComparer.Equals("quit", command))
            {
                _continue = false;
            }
            //If CRC_OK is typed then send Command 1 with correct CRC
            else if (stringComparer.Equals("CRC_OK", command))
            {
                //Read command details from xml file and create a byte list out of it
                List<byte> bytes = CreateByteListFromCommandFile("command1.xml");

                //calculate and add crc
                ushort crc = CalculateCRC(bytes.ToArray());
                bytes.Add((byte)(crc >> 8));
                bytes.Add((byte)(crc));

                //print message on screen
                PrintCommandDetails(bytes.ToArray(), true);

                //Send byte array via serial port
                _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
            }
            //If CRC_ER is typed then send Command 1 with wrong CRC
            else if (stringComparer.Equals("CRC_ER", command))
            {
                //Read command details from xml file and create a byte list out of it
                List<byte> bytes = CreateByteListFromCommandFile("command1.xml");

                //add wrong crc values
                ushort crc = CalculateCRC(bytes.ToArray());
                bytes.Add((byte)((crc / 2) >> 8));
                bytes.Add((byte)((crc / 4)));

                //print message on screen
                PrintCommandDetails(bytes.ToArray(), true);

                //Send byte array via serial port
                _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
            }
            else
            {
                //If input is anything else then send it directly
                _serialPort.WriteLine(command);
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
                //Read incoming message and turn it into byte array
                string message = _serialPort.ReadLine();
                byte[] incomingBytes = Encoding.GetEncoding(28591).GetBytes(message);

                //Disregard messages shorter than 3 bytes since at least 2 bytes need to be CRC
                if (incomingBytes.Length > 2)
                {
                    //CRC check: Make a list of all bytes except CRC bytes at the end
                    List<byte> bytesMinusCRC = new List<byte>();
                    for (int i = 0; i < incomingBytes.Length - 2; i++)
                        bytesMinusCRC.Add(incomingBytes[i]);

                    //Get CRC bytes in the message as UInt16
                    ushort crcInMessage = (ushort)((incomingBytes[incomingBytes.Length - 2] << 8)
                        + incomingBytes[incomingBytes.Length - 1]);

                    //If CRC in message isn't equal to calculated CRC
                    if (crcInMessage != CalculateCRC(bytesMinusCRC.ToArray()))
                    {
                        //Create wrong crc error message
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA); //Header
                        bytes.Add(0x05); //Message Length
                        bytes.Add(0xA5); //Message Type (Command No)
                        bytes.Add(0x01); //Reason : Wrong CRC
                        //Calculate and Add CRC at the end
                        ushort crc = CalculateCRC(bytes.ToArray());
                        bytes.Add((byte)(crc >> 8));
                        bytes.Add((byte)(crc));

                        //Send message
                        _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
                    }
                    //If received message is Invalid Request
                    else if (incomingBytes[2] == 0xA5)
                    {
                        //Print "Invalid Request" details on screen
                        Console.WriteLine("----------");
                        Console.Write("Gelen Mesaj: ");
                        Console.Write(ByteArrayToString(incomingBytes));
                        Console.WriteLine();
                        Console.Write("Mesaj Tipi: ");
                        Console.Write("Geçersiz İstek");
                        Console.WriteLine();
                        Console.Write("Sebep: ");
                        Console.Write(incomingBytes[3]);
                    }
                    //If received message is Command 2
                    else if (incomingBytes[2] == 0xA9)
                    {
                        //Print incoming Command 2 details on screen
                        PrintCommandDetails(incomingBytes, false);
                    }
                    //If received message is Command 1
                    else if (incomingBytes[2] == 0xA8)
                    {
                        //Create command 2 response
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA); //Header
                        bytes.Add(0x09); //Message Length
                        bytes.Add(0xA9); //Message Type (Command No)
                        bytes.Add((byte)~incomingBytes[3]); //Complement of UInt8

                        //Convert next 4 bytes to UInt32 integer and double it
                        uint integer = ConvertBytesToInteger(incomingBytes);
                        integer *= 2;

                        //Turn it back into a byte array and add those bytes into bytes list
                        byte[] integerBytes = new byte[4];
                        integerBytes = BitConverter.GetBytes(integer);

                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(integerBytes);

                        foreach (byte elem in integerBytes)
                            bytes.Add(elem);

                        //Calculate and Add 2 CRC bytes at the end
                        ushort crc = CalculateCRC(bytes.ToArray());
                        bytes.Add((byte)(crc >> 8));
                        bytes.Add((byte)(crc));

                        //Send message
                        _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
                    }
                    else
                    {
                        //Create invalid request error message
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA); //Header
                        bytes.Add(0x05); //Message Length
                        bytes.Add(0xA5); //Message Type (Command No)
                        bytes.Add(0x02); //Reason: Invalid request
                        //Calculate CRC and add it to the end
                        ushort crc = CalculateCRC(bytes.ToArray());
                        bytes.Add((byte)(crc >> 8));
                        bytes.Add((byte)(crc));

                        //Send message
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

        //Convert integer to byte array
        byte[] intBytes = BitConverter.GetBytes(integer);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(intBytes);

        //Add converted bytes to list
        foreach (byte elem in intBytes)
            bytes.Add(elem);

        return bytes;
    }
    public static string ByteArrayToString(byte[] ba)
    {
        return BitConverter.ToString(ba).Replace("-", " ");
    }

    private static void PrintCommandDetails(byte[] bytes, bool isBeingSent)
    {
        Console.WriteLine("----------");

        if (isBeingSent)
            Console.Write("Gönderilen Mesaj: ");

        else
            Console.Write("Gelen Mesaj: ");

        Console.WriteLine(ByteArrayToString(bytes));

        if (!isBeingSent)
        {
            Console.Write("Mesaj Tipi: ");
            Console.Write(bytes[2]);
            Console.WriteLine();
        }

        string byteString = Convert.ToString(bytes[3], 2).PadLeft(8, '0');
        int index = 0;

        Console.WriteLine("A Durumu: ");
        Console.Write("Tek Hata: ");
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();
        Console.Write("Mesaj Hatası: ");
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();
        Console.Write("A Sinyal Durumu: ");
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();
        Console.Write("B Sinyal Durumu: ");
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();
        Console.Write("C Sinyal Durumu: ");
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();
        Console.Write("Çift Hata: ");
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();
        Console.Write("6:7 : ");
        Console.Write(byteString.Substring(index++, 1));
        Console.Write(byteString.Substring(index++, 1));
        Console.WriteLine();

        Console.WriteLine("B Değeri: ");
        Console.WriteLine(ConvertBytesToInteger(bytes));
    }

    private static uint ConvertBytesToInteger(byte[] bytes)
    {
        byte[] integerBytes = new byte[4];
        int startIndex = 4;
        int endIndex = 8;

        for (int i = startIndex; i < endIndex; i++)
        {
            integerBytes[i - startIndex] = bytes[i];
        }

        if (BitConverter.IsLittleEndian)
            Array.Reverse(integerBytes);

        return BitConverter.ToUInt32(integerBytes, 0);
    }
}