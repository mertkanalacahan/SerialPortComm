using System;

public static class Utilities
{
    public static ushort CalculateCRC(byte[] data)
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

    public static string ByteArrayToString(byte[] ba)
    {
        return BitConverter.ToString(ba).Replace("-", " ");
    }

    public static uint ConvertBytesToInteger(byte[] bytes)
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

