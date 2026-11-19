using System;
using System.Security.Cryptography;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Session obfuscation for Wi-Fi sync payloads (protocol v2).
    /// Key = SHA1("Alfa156-WiFi-Sync-v2|" + pairing code); payload XOR'd with
    /// SHA1(key || LE32(blockIndex)) keystream. CF 3.5 compatible (SHA1 only).
    /// </summary>
    internal sealed class SyncCrypto : IDisposable
    {
        internal const string ProtocolHeader = "ALFA156SYNC2|";
        internal const string UpgradeMessage =
            "Обновите Alfa 156 на обоих устройствах до 1.12+";

        private const string Salt = "Alfa156-WiFi-Sync-v2";
        private readonly SHA1CryptoServiceProvider sha;
        private readonly byte[] key;
        private readonly byte[] blockInput;
        private byte[] cachedBlock;
        private int cachedBlockIndex = -1;

        internal SyncCrypto(string pairingCode)
        {
            if (pairingCode == null)
                throw new ArgumentNullException("pairingCode");
            sha = new SHA1CryptoServiceProvider();
            key = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + "|" + pairingCode));
            blockInput = new byte[24];
            Buffer.BlockCopy(key, 0, blockInput, 0, 20);
        }

        internal void TransformInPlace(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;
            TransformInPlace(data, 0, data.Length, 0);
        }

        internal void TransformInPlace(byte[] data, int offset, int count, long streamOffset)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            if (offset < 0 || count < 0 || offset + count > data.Length)
                throw new ArgumentOutOfRangeException("count");
            long pos = streamOffset;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                int blockIndex = (int)(pos / 20L);
                int within = (int)(pos % 20L);
                byte[] block = GetBlock(blockIndex);
                data[i] ^= block[within];
                pos++;
            }
        }

        private byte[] GetBlock(int blockIndex)
        {
            if (blockIndex == cachedBlockIndex && cachedBlock != null)
                return cachedBlock;
            blockInput[20] = (byte)blockIndex;
            blockInput[21] = (byte)(blockIndex >> 8);
            blockInput[22] = (byte)(blockIndex >> 16);
            blockInput[23] = (byte)(blockIndex >> 24);
            cachedBlock = sha.ComputeHash(blockInput);
            cachedBlockIndex = blockIndex;
            return cachedBlock;
        }

        public void Dispose()
        {
            sha.Clear();
        }
    }
}
