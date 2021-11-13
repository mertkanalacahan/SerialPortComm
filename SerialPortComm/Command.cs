using System;
using System.Collections.Generic;
using System.Xml;

public class Command
{
    List<byte> bytes;
    public Command(string fileName)
    {
        //Read command details from xml file and create a byte list out of it
        bytes = CreateByteListFromCommandFile(fileName);
    }

    private List<byte> CreateByteListFromCommandFile(string filename)
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

    public static void PrintCommandDetails(byte[] bytes, bool isBeingSent)
    {
        Console.WriteLine("----------");

        if (isBeingSent)
            Console.Write("Gönderilen Mesaj: ");

        else
            Console.Write("Gelen Mesaj: ");

        Console.WriteLine(Utilities.ByteArrayToString(bytes));

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
        Console.WriteLine(Utilities.ConvertBytesToInteger(bytes));
    }

    public void AddCorrectCRCBits()
    {
        throw new NotImplementedException();
    }

    public void AddWrongCRCBits()
    {
        throw new NotImplementedException();
    }
}