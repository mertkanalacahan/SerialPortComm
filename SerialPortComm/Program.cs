using System;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;

enum MessageType : byte
{
    INVALID_REQUEST = 0xA5,
    COMMAND_1 = 0xA8,
    COMMAND_2 = 0xA9
}

enum InvalidRequestReason : byte
{
    INVALID_CRC = 0x01,
    UNIDENTIFIED_MESSAGE = 0x02
}

public class SerialPortCommApp
{
    static bool _continue;
    static SerialPort _serialPort;

    public static void Main()
    {
        string command;
        StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;
        Thread readThread = new Thread(Read);
        SerialPortSettings portSettings = new SerialPortSettings("conf.xml");

        _serialPort = new SerialPort();
        //Encoding needs to be 28591 otherwise bytes larger than 127 are being set to 63
        _serialPort.Encoding = Encoding.GetEncoding(28591);

        _serialPort.PortName = portSettings.GetPortName();
        _serialPort.BaudRate = portSettings.GetBaudRate();
        _serialPort.Parity = portSettings.GetParity();
        _serialPort.DataBits = portSettings.GetDataBits();
        _serialPort.StopBits = portSettings.GetStopBits();
        _serialPort.Handshake = portSettings.GetHandshake();
        _serialPort.ReadTimeout = portSettings.GetReadTimeout();
        _serialPort.WriteTimeout = portSettings.GetWriteTimeout();

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
                Command command1 = new Command("command1.xml");
                command1.AddCorrectCRCBits();
                Command.PrintCommandDetails(command1.bytes.ToArray(), true);

                //Send byte array via serial port
                _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(command1.bytes.ToArray()));
            }
            //If CRC_ER is typed then send Command 1 with wrong CRC
            else if (stringComparer.Equals("CRC_ER", command))
            {
                Command command1 = new Command("command1.xml");
                command1.AddWrongCRCBits();
                Command.PrintCommandDetails(command1.bytes.ToArray(), true);

                //Send byte array via serial port
                _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(command1.bytes.ToArray()));
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
                    if (crcInMessage != Utilities.CalculateCRC(bytesMinusCRC.ToArray()))
                    {
                        //Create wrong crc error message
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA); //Header
                        bytes.Add((byte)MessageType.INVALID_REQUEST); //Message Type (Command No)
                        bytes.Add((byte)InvalidRequestReason.INVALID_CRC); //Reason : Wrong CRC

                        //Insert message length byte (Current bytes + 2 CRC bytes)
                        bytes.Insert(1, (byte)(bytes.Count + 2));

                        //Calculate and Add CRC at the end
                        ushort crc = Utilities.CalculateCRC(bytes.ToArray());
                        bytes.Add((byte)(crc >> 8));
                        bytes.Add((byte)(crc));

                        //Send message
                        _serialPort.WriteLine(Encoding.GetEncoding(28591).GetString(bytes.ToArray()));
                    }
                    //If received message is Invalid Request
                    else if (incomingBytes[2] == (byte)MessageType.INVALID_REQUEST)
                    {
                        //Print "Invalid Request" details on screen
                        Console.WriteLine("----------");
                        Console.Write("Gelen Mesaj: ");
                        Console.Write(Utilities.ByteArrayToString(incomingBytes));
                        Console.WriteLine();
                        Console.Write("Mesaj Tipi: ");
                        Console.Write("Geçersiz İstek");
                        Console.WriteLine();
                        Console.Write("Sebep: ");
                        Console.Write(incomingBytes[3] + " -> " + (InvalidRequestReason)incomingBytes[3]);
                    }
                    //If received message is Command 2
                    else if (incomingBytes[2] == (byte)MessageType.COMMAND_2)
                    {
                        //Print incoming Command 2 details on screen
                        Command.PrintCommandDetails(incomingBytes, false);
                    }
                    //If received message is Command 1
                    else if (incomingBytes[2] == (byte)MessageType.COMMAND_1)
                    {
                        //Create command 2 response
                        List<byte> bytes = new List<byte>();
                        bytes.Add(0xCA); //Header
                        bytes.Add((byte)MessageType.COMMAND_2); //Message Type (Command No)
                        bytes.Add((byte)~incomingBytes[3]); //Complement of UInt8

                        //Convert next 4 bytes to UInt32 integer and double it
                        uint integer = Utilities.ConvertBytesToInteger(incomingBytes);
                        integer *= 2;

                        //Turn it back into a byte array and add those bytes into bytes list
                        byte[] integerBytes = new byte[4];
                        integerBytes = BitConverter.GetBytes(integer);

                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(integerBytes);

                        foreach (byte elem in integerBytes)
                            bytes.Add(elem);

                        //Insert message length byte (Current bytes + 2 CRC bytes)
                        bytes.Insert(1, (byte)(bytes.Count + 2));

                        //Calculate and Add 2 CRC bytes at the end
                        ushort crc = Utilities.CalculateCRC(bytes.ToArray());
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
                        bytes.Add((byte)MessageType.INVALID_REQUEST); //Message Type (Command No)
                        bytes.Add((byte)InvalidRequestReason.UNIDENTIFIED_MESSAGE); //Reason: Unidentified Message

                        //Insert message length byte (Current bytes + 2 CRC bytes)
                        bytes.Insert(1, (byte)(bytes.Count + 2));

                        //Calculate CRC and add it to the end
                        ushort crc = Utilities.CalculateCRC(bytes.ToArray());
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
}