using System;
using System.Text;

namespace WelderRS232
{
    public static class WelderCrypto
    {
        private const string RC4_KEY = "adA$2#34&1ASdq123";

        public static void RC4Encrypt(byte[] data, int offset, int length)
        {
            // KSA - Key Scheduling Algorithm
            byte[] S = new byte[256];
            for (int i = 0; i < 256; i++) S[i] = (byte)i;

            int j = 0;
            byte[] key = Encoding.ASCII.GetBytes(RC4_KEY);
            int keyLength = key.Length;
            for (int i = 0; i < 256; i++)
            {
                j = (j + S[i] + key[i % keyLength]) % 256;
                // swap S[i], S[j]
                byte tmp = S[i];
                S[i] = S[j];
                S[j] = tmp;
            }

            // PRGA - Pseudo-Random Generation Algorithm
            int i_ = 0, j_ = 0;
            byte[] keyStream = new byte[length];
            for (int k = 0; k < length; k++)
            {
                i_ = (i_ + 1) % 256;
                j_ = (j_ + S[i_]) % 256;
                // swap S[i_], S[j_]
                byte tmp = S[i_];
                S[i_] = S[j_];
                S[j_] = tmp;
                keyStream[k] = S[(S[i_] + S[j_]) % 256];
            }

            // XOR data with key stream
            for (int k = 0; k < length; k++)
            {
                data[offset + k] ^= keyStream[k];
            }
        }

        public static ushort CRC16(byte[] buf, int offset, int cnt)
        {
            uint temp = 0xFFFF;
            for (int i = offset; i < offset + cnt; i++)
            {
                temp = temp ^ buf[i];
                for (int j = 1; j <= 8; j++)
                {
                    uint flag = temp & 0x0001;
                    temp = temp >> 1;
                    if (flag != 0) temp = temp ^ 0xA001;
                }
            }
            return (ushort)temp;
        }

        // Overload for backward compatibility
        public static ushort CRC16(byte[] buf, int cnt)
        {
            return CRC16(buf, 0, cnt);
        }
    }
}