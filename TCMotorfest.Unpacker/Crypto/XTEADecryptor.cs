using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace TCMotorfest.Unpacker.Crypto
{
    public class XTEAParameter
    {
        public uint Rounds { get; set; }
        public uint DecryptSize { get; set; }
        public string Key { get; set; }

        public XTEAParameter(uint Rounds, uint DecryptSize, string Key)
        {
            this.Rounds = Rounds;
            this.DecryptSize = DecryptSize;
            this.Key = Key;
        }
    }

    public class XTEADecryptor
    {
        static public bool Decrypt(uint method, XTEAParameter parameter, Span<byte> input, Span<byte> output, uint length)
        {
            if (method != 0x41455458)
                return false;

            uint[] key = MemoryMarshal.Cast<byte, uint>(Encoding.ASCII.GetBytes(new string(parameter.Key))).ToArray();

            if (length > parameter.DecryptSize)
                length = parameter.DecryptSize;

            length &= 0xFFFFFFF8;

            for (var i = 0; i < length; i += 8)
            {
                uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(i + 0));
                uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(i + 4));

                // Decrypt TEA constant - 9e3779b9
                uint v29 = true ? 0x16408446u : 0x55B54BCBu;
                uint v30 = AddXor(v29, 0xAD823B32, 0x1F098FF8);

                uint delta = AddRotateRight((uint)-(int)v30 - 0x6D3C158B, 0xF5, 0x6D3C158A);
                uint sum = parameter.Rounds * delta;
                for (uint j = 0; j < parameter.Rounds; j++)
                {
                    v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + key[sum >> 11 & 3];
                    sum -= delta;
                    v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + key[sum & 3];
                }

                BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(i + 0), v0);
                BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(i + 4), v1);
            }

            return true;
        }

        static public void Decrypt_R16_L20000(Span<byte> data, uint length)
        {
            bool retAddr = true;

            // Decrypt rounds (16)
            uint v5 = MinusXor(0x4BE81B9C, 0xE806F890, 0xAEE21D69);
            uint v6 = retAddr ? BitOperations.RotateRight(v5, 15) : v5;
            uint rounds = v6 ^ 0x6B460A16;

            // Decrypt Length (0x20000)
            uint v8 = AddRotateLeft(0xC4B975E4, 0xC0, 0x692711AB);
            uint v9 = AddRotateLeft(v8 - 0x692711AB, 0x49, 0x692711AB);
            uint v10 = retAddr ? MinusXor(v9, 0x3FAA02D7, 0xFEA51C89) : v9;
            uint maxLength = BitOperations.RotateRight(PlusNot(v10 - 0x53AFD803, 0x53AFD803), 16);

            if (maxLength != 0 && length > maxLength)
                length = maxLength;

            uint[] key = new uint[4];

            byte[] tempBuffer = new byte[0x10];

            // Decrypt Keys
            // All buffers leads to the same key
            DecryptKeyPart(tempBuffer, 0x993D6F33, new byte[] { /* ... */ });
            key[0] = BinaryPrimitives.ReadUInt32LittleEndian(tempBuffer.AsSpan(0));
            tempBuffer.AsSpan().Clear();

            DecryptKeyPart(tempBuffer, 0x6B20ED38, new byte[] { /* ... */ });
            key[1] = BinaryPrimitives.ReadUInt32LittleEndian(tempBuffer.AsSpan(4));
            tempBuffer.AsSpan().Clear();

            DecryptKeyPart(tempBuffer, 0x615D4755, new byte[] { /* ... */ });
            key[2] = BinaryPrimitives.ReadUInt32LittleEndian(tempBuffer.AsSpan(8));
            tempBuffer.AsSpan().Clear();

            DecryptKeyPart(tempBuffer, 0xB5F353AA, new byte[] { /* ... */ });
            key[3] = BinaryPrimitives.ReadUInt32LittleEndian(tempBuffer.AsSpan(12));
            tempBuffer.AsSpan().Clear();

            for (var i = 0; i < length; i += 8)
            {
                uint v0 = BinaryPrimitives.ReadUInt32BigEndian(data[0..]);
                uint v1 = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);

                // Decrypt TEA constant - 9e3779b9
                uint v29 = retAddr ? 0x16408446u : 0x55B54BCBu;
                uint v30 = AddXor(v29, 0xAD823B32, 0x1F098FF8);

                uint delta = AddRotateRight((uint)-(int)v30 - 0x6D3C158B, 0xF5, 0x6D3C158A);
                uint sum = rounds * delta;
                for (uint j = 0; j < rounds; j++)
                {
                    v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + key[sum >> 11 & 3];
                    sum -= delta;
                    v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + key[sum & 3];
                }

                BinaryPrimitives.WriteUInt32LittleEndian(data[0..], v0);
                BinaryPrimitives.WriteUInt32LittleEndian(data[4..], v1);
            }
        }

        static void DecryptKeyPart(byte[] output, uint packedValue, byte[] k1)
        {
            for (var i = 0; i < 16; i++)
            {
                output[i] = (byte)(packedValue ^ k1[i]);
                packedValue = BitOperations.RotateLeft(packedValue, 1);
            }
        }


        static uint Minus(uint a1, uint a2)
        {
            return a2 - a1;
        }

        static uint AddXor(uint a1, uint a2, uint value)
        {
            return a1 + (a2 ^ value);
        }

        static uint XorAdd(uint a1, uint a2, uint value)
        {
            return a1 ^ a2 + value;
        }

        static uint XorMinus(uint a1, uint a2, uint value)
        {
            return a1 ^ a2 - value;
        }

        static uint MinusXor(uint left, uint right, uint value)
        {
            return left - (right ^ value);
        }

        static uint NotPlus(uint a1, uint value)
        {
            return ~(a1 + value);
        }

        static uint AddRotateLeft(uint left, uint offset, uint val)
        {
            uint res = left + val;
            if ((offset & 0x1F) != 0)
                return BitOperations.RotateLeft(res, (byte)(offset & 0x1F));
            return res;
        }

        static uint MinusRotateLeft(uint left, uint offset, uint val)
        {
            uint res = left - val;
            if ((offset & 0x1F) != 0)
                return BitOperations.RotateLeft(res, (byte)offset & 0x1F);
            return res;
        }


        static uint AddRotateRight(uint a1, uint offset, uint value)
        {
            uint res = a1 + value;
            if ((offset & 0x1F) != 0)
                return BitOperations.RotateRight(res, (byte)offset & 0x1F);
            return res;
        }

        static uint PlusNot(uint left, uint value)
        {
            return ~(left + value);
        }
    }
}
