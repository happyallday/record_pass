using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;

namespace PasswordManager.Services
{
    public class EncryptionService
    {
        [DllImport("crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CryptProtectData(
            ref DATA_BLOB pDataIn,
            string szDataDescr,
            ref DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DATA_BLOB pDataOut
        );

        [DllImport("crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CryptUnprotectData(
            ref DATA_BLOB pDataIn,
            StringBuilder szDataDescr,
            ref DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DATA_BLOB pDataOut
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        private const string KEY_FILE = "master.key";

        private byte[]? _encryptionKey;

        public byte[] GenerateKeyFromMasterPassword(string masterPassword)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(masterPassword, Encoding.UTF8.GetBytes("PasswordManagerSalt"), 10000, HashAlgorithmName.SHA256);
            return deriveBytes.GetBytes(32);
        }

        public void SaveEncryptionKey(byte[] key, string appDataPath)
        {
            var keyPath = Path.Combine(appDataPath, KEY_FILE);
            var protectedKey = ProtectData(key);
            File.WriteAllBytes(keyPath, protectedKey);
        }

        public bool TryLoadEncryptionKey(string masterPassword, string appDataPath, out byte[] key)
        {
            key = Array.Empty<byte>();
            var keyPath = Path.Combine(appDataPath, KEY_FILE);

            if (!File.Exists(keyPath))
                return false;

            try
            {
                var protectedKey = File.ReadAllBytes(keyPath);
                var unprotectedKey = UnprotectData(protectedKey);
                var testKey = GenerateKeyFromMasterPassword(masterPassword);

                key = unprotectedKey;
                return unprotectedKey.SequenceEqual(testKey);
            }
            catch
            {
                return false;
            }
        }

        public string EncryptPassword(string password, string masterPassword)
        {
            if (_encryptionKey == null)
                _encryptionKey = GenerateKeyFromMasterPassword(masterPassword);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            
            msEncrypt.Write(aes.IV, 0, aes.IV.Length);

            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(password);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public string DecryptPassword(string encryptedPassword, string masterPassword)
        {
            if (_encryptionKey == null)
                _encryptionKey = GenerateKeyFromMasterPassword(masterPassword);

            var fullCipher = Convert.FromBase64String(encryptedPassword);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;

            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            return srDecrypt.ReadToEnd();
        }

        private byte[] ProtectData(byte[] data)
        {
            var blobIn = new DATA_BLOB
            {
                cbData = data.Length,
                pbData = Marshal.AllocHGlobal(data.Length)
            };
            Marshal.Copy(data, 0, blobIn.pbData, data.Length);

            var blobOut = new DATA_BLOB();

            if (!CryptProtectData(ref blobIn, null, ref blobOut, IntPtr.Zero, IntPtr.Zero, 0, ref blobOut))
            {
                Marshal.FreeHGlobal(blobIn.pbData);
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var result = new byte[blobOut.cbData];
            Marshal.Copy(blobOut.pbData, result, 0, blobOut.cbData);

            Marshal.FreeHGlobal(blobIn.pbData);
            Marshal.FreeHGlobal(blobOut.pbData);

            return result;
        }

        private byte[] UnprotectData(byte[] protectedData)
        {
            var blobIn = new DATA_BLOB
            {
                cbData = protectedData.Length,
                pbData = Marshal.AllocHGlobal(protectedData.Length)
            };
            Marshal.Copy(protectedData, 0, blobIn.pbData, protectedData.Length);

            var blobOut = new DATA_BLOB();

            if (!CryptUnprotectData(ref blobIn, null, ref blobOut, IntPtr.Zero, IntPtr.Zero, 0, ref blobOut))
            {
                Marshal.FreeHGlobal(blobIn.pbData);
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var result = new byte[blobOut.cbData];
            Marshal.Copy(blobOut.pbData, result, 0, blobOut.cbData);

            Marshal.FreeHGlobal(blobIn.pbData);
            Marshal.FreeHGlobal(blobOut.pbData);

            return result;
        }
    }
}