using System;

namespace WelderRS232
{
    public static class WelderCommands
    {
        public const int COMMAND_SIZE = 30;

        public static byte[] BuildReadConfigCommand(bool bezSzyfrowania = false)
        {
            byte[] cmd = new byte[COMMAND_SIZE];
            cmd[0] = (byte)(bezSzyfrowania ? 't' : 'T');
            cmd[1] = (byte)'Z';
            cmd[2] = (byte)'V';
            cmd[3] = (byte)'6';
            cmd[4] = (byte)4;
            cmd[5] = (byte)'S';

            // Fill with X like in the specification
            for (int i = 6; i < 26; i++)
            {
                cmd[i] = (byte)'X';
            }

            // Calculate CRC on bytes 2-24 (COMMAND_SIZE - 6)
            ushort crc = WelderCrypto.CRC16(cmd, 0, COMMAND_SIZE - 4);
            cmd[COMMAND_SIZE - 4] = (byte)(crc >> 8);
            cmd[COMMAND_SIZE - 3] = (byte)(crc & 0xFF);

            // Szyfrowanie RC4 od bajtu 2 do 27 (26 bajtów)

            if (!bezSzyfrowania)
            {
                WelderCrypto.RC4Encrypt(cmd, 2, COMMAND_SIZE - 4);
            }

            cmd[COMMAND_SIZE - 2] = 0x0D;  // CR
            cmd[COMMAND_SIZE - 1] = 0x0A;  // LF
            return cmd;
        }

        public static byte[] BuildIdentifyCommand(bool bezSzyfrowania = false)
        {
            byte[] cmd = new byte[COMMAND_SIZE];
            cmd[0] = (byte)(bezSzyfrowania ? 't' : 'T');
            cmd[1] = (byte)'Z';
            cmd[2] = (byte)'A';

            // Fill with numbers 1-5 like in C++ version
            for (int i = 3; i < 26; i++)
            {
                cmd[i] = (byte)('1' + ((i - 3) % 5));
            }

            // Calculate CRC on bytes 2-24 (COMMAND_SIZE - 6)
            ushort crc = WelderCrypto.CRC16(cmd, 0, COMMAND_SIZE - 4);
            cmd[COMMAND_SIZE - 4] = (byte)(crc >> 8);
            cmd[COMMAND_SIZE - 3] = (byte)(crc & 0xFF);

            if (!bezSzyfrowania)
            {
                WelderCrypto.RC4Encrypt(cmd, 2, COMMAND_SIZE - 4);
            }

            cmd[COMMAND_SIZE - 2] = 0x0D;
            cmd[COMMAND_SIZE - 1] = 0x0A;
            return cmd;
        }

        public static byte[] BuildTypeQueryCommand(bool bezSzyfrowania = false)
        {
            byte[] cmd = new byte[COMMAND_SIZE];
            cmd[0] = (byte)(bezSzyfrowania ? 't' : 'T');
            cmd[1] = (byte)'Z';
            cmd[2] = (byte)'B';

            // Fill with X like in C++ version
            for (int i = 3; i < 26; i++)
            {
                cmd[i] = (byte)'X';
            }

            // Calculate CRC on bytes 2-24 (COMMAND_SIZE - 6)
            ushort crc = WelderCrypto.CRC16(cmd, 0, COMMAND_SIZE - 4);
            cmd[COMMAND_SIZE - 4] = (byte)(crc >> 8);
            cmd[COMMAND_SIZE - 3] = (byte)(crc & 0xFF);
            if (!bezSzyfrowania)
            {
                WelderCrypto.RC4Encrypt(cmd, 2, COMMAND_SIZE - 4);
            }

            cmd[COMMAND_SIZE - 2] = 0x0D;
            cmd[COMMAND_SIZE - 1] = 0x0A;
            return cmd;
        }

        public static byte[] BuildReadWeldCountCommand(bool bezSzyfrowania = false)
        {
            byte[] cmd = new byte[COMMAND_SIZE];
            cmd[0] = (byte)(bezSzyfrowania ? 't' : 'T');
            cmd[1] = (byte)'Z';
            cmd[2] = (byte)'F';

            // Fill with X like in the specification
            for (int i = 3; i < 26; i++)
            {
                cmd[i] = (byte)'X';
            }

            // Calculate CRC on bytes 2-24 (COMMAND_SIZE - 6)
            ushort crc = WelderCrypto.CRC16(cmd, 0, COMMAND_SIZE - 4);
            cmd[COMMAND_SIZE - 4] = (byte)(crc >> 8);
            cmd[COMMAND_SIZE - 3] = (byte)(crc & 0xFF);

            if (!bezSzyfrowania)
            {
                WelderCrypto.RC4Encrypt(cmd, 2, COMMAND_SIZE - 4);
            }

            cmd[COMMAND_SIZE - 2] = 0x0D;  // CR
            cmd[COMMAND_SIZE - 1] = 0x0A;  // LF
            return cmd;
        }

        public static byte[] BuildReadWeldParametersCommand(bool bezSzyfrowania = false)
        {
            byte[] cmd = new byte[COMMAND_SIZE];
            cmd[0] = (byte)(bezSzyfrowania ? 't' : 'T');
            cmd[1] = (byte)'Z';
            cmd[2] = (byte)'V';
            cmd[3] = (byte)'7';
            // Wypełnij X od pozycji 4 do 25
            for (int i = 4; i < 26; i++)
            {
                cmd[i] = (byte)'X';
            }
            // CRC na bajtach 0-25 (26 bajtów)
            ushort crc = WelderCrypto.CRC16(cmd, 0, COMMAND_SIZE - 4);
            cmd[COMMAND_SIZE - 4] = (byte)(crc >> 8);
            cmd[COMMAND_SIZE - 3] = (byte)(crc & 0xFF);
            if (!bezSzyfrowania)
            {
                WelderCrypto.RC4Encrypt(cmd, 2, COMMAND_SIZE - 4);
            }
            cmd[COMMAND_SIZE - 2] = 0x0D; // CR
            cmd[COMMAND_SIZE - 1] = 0x0A; // LF
            return cmd;
        }
    }
}