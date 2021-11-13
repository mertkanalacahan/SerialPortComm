using System;
using System.IO.Ports;
using System.Xml;

public class SerialPortSettings
{
    string portName;
    int baudRate;
    Parity parity;
    int dataBits;
    StopBits stopBits;
    Handshake handshake;
    int readTimeout;
    int writeTimeout;

    public SerialPortSettings(string fileName)
    {
        XmlDocument config = new XmlDocument();

        //Load serial port configuration file
        config.Load(fileName);

        //Get serial port settings from xml file
        XmlNodeList portNameNode = config.GetElementsByTagName("portName");
        XmlNodeList baudRateNode = config.GetElementsByTagName("baudRate");
        XmlNodeList parityNode = config.GetElementsByTagName("parity");
        XmlNodeList dataBitsNode = config.GetElementsByTagName("dataBits");
        XmlNodeList stopBitsNode = config.GetElementsByTagName("stopBits");
        XmlNodeList handshakeNode = config.GetElementsByTagName("handshake");
        XmlNodeList readTimeoutNode = config.GetElementsByTagName("readTimeout");
        XmlNodeList writeTimeoutNode = config.GetElementsByTagName("writeTimeout");

        //Set the appropriate properties
        SetPortName(portNameNode[0].InnerText);
        SetPortBaudRate(baudRateNode[0].InnerText);
        SetPortParity(parityNode[0].InnerText);
        SetPortDataBits(dataBitsNode[0].InnerText);
        SetPortStopBits(stopBitsNode[0].InnerText);
        SetPortHandshake(handshakeNode[0].InnerText);
        SetReadTimeout(readTimeoutNode[0].InnerText);
        SetWriteTimeout(writeTimeoutNode[0].InnerText);
    }

    public string GetPortName()
    {
        return portName;
    }

    public int GetBaudRate()
    {
        return baudRate;
    }

    public Parity GetParity()
    {
        return parity;
    }

    public int GetDataBits()
    {
        return dataBits;
    }

    public StopBits GetStopBits()
    {
        return stopBits;
    }

    public Handshake GetHandshake()
    {
        return handshake;
    }

    public int GetReadTimeout()
    {
        return readTimeout;
    }

    public int GetWriteTimeout()
    {
        return writeTimeout;
    }

    private void SetPortName(string portName)
    {
        if (portName == "" || !(portName.ToLower()).StartsWith("com"))
        {
            portName = "COM1";
        }

        this.portName = portName;
    }

    private void SetPortBaudRate(string baudRate)
    {
        if (baudRate == "")
        {
            baudRate = "9600";
        }

        this.baudRate = int.Parse(baudRate);
    }

    private void SetPortParity(string parity)
    {
        if (parity == "")
        {
            parity = "None";
        }

        this.parity = (Parity)Enum.Parse(typeof(Parity), parity, true);
    }

    private void SetPortDataBits(string dataBits)
    {
        if (dataBits == "")
        {
            dataBits = "8";
        }

        this.dataBits = int.Parse(dataBits.ToUpperInvariant());
    }

    private void SetPortStopBits(string stopBits)
    {
        if (stopBits == "")
        {
            stopBits = "One";
        }

        this.stopBits = (StopBits)Enum.Parse(typeof(StopBits), stopBits, true);
    }

    private void SetPortHandshake(string handshake)
    {
        if (handshake == "")
        {
            handshake = "None";
        }

        this.handshake = (Handshake)Enum.Parse(typeof(Handshake), handshake, true);
    }

    private void SetReadTimeout(string readTimeout)
    {
        if (readTimeout == "")
        {
            readTimeout = "500";
        }

        this.readTimeout = int.Parse(readTimeout);
    }
    private void SetWriteTimeout(string writeTimeout)
    {
        if (writeTimeout == "")
        {
            writeTimeout = "500";
        }

        this.writeTimeout = int.Parse(writeTimeout);
    }
}