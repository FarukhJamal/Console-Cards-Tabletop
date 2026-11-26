// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DatagramEncryptorNative.cs" company="Exit Games GmbH">
//   Protocol & Photon Client Lib - Copyright (C) 2010 Exit Games GmbH
// </copyright>
// <summary>
//   Datagram Encryption implementation.
// </summary>
// <author>developer@photonengine.com</author>
// --------------------------------------------------------------------------------------------------------------------

#if UNITY_4_7 || UNITY_5 || UNITY_5_3_OR_NEWER
#define SUPPORTED_UNITY
#endif

#if ((UNITY_IOS || UNITY_SWITCH || UNITY_SWITCH2) && !UNITY_EDITOR) || __IOS__
#define STATIC_IMPORT
#endif


namespace ExitGames.Client.Photon.Encryption
{
    using global::Photon.Client;                // uncomment for Photon .Net v5 compatibility. comment-out for Photon .Net v4 compatibility
    using global::Photon.Client.Encryption;     // uncomment for Photon .Net v5 compatibility. comment-out for Photon .Net v4 compatibility
    using System;
    using System.Runtime.InteropServices;
    using System.Diagnostics;

    /// <summary>EncryptorNative was renamed to DatagramEncryptorNative in the Datagram Encryption Addon. This class inherits DatagramEncryptorNative for compatibility.</summary>
    [Preserve]
    public class EncryptorNative : DatagramEncryptorNative
    {
    }

    /// <summary>
    /// Implementation of encryption for "Datagram Encryption".
    /// </summary>
    [Preserve]
    public class DatagramEncryptorNative : IPhotonEncryptor
    {
#if STATIC_IMPORT
        private const string LibName = "__Internal";
#else
        private const string LibName = "PhotonDatagramEncryptorPlugin";
#endif
        public enum ChainingMode : byte
        {
            GCM = 1,
        }
        #if !NETFX_CORE
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallbackDelegate(IntPtr userData, int level, [MarshalAs(UnmanagedType.LPStr)] string msg);
        #endif
        // The classes which use DllImport, should always use public (!) extern method. This allows a PrelinkAll() check if the dll is loaded.
#if NATIVE_ENCRYPTOR_API1
        #error NATIVE_ENCRYPTOR_API1 is no longer supported
#else
        [DllImport(LibName)]
        public static extern IntPtr egdatagramEncryptorConstruct(byte[] pEncryptSecret, byte[] pHMACSecret, ChainingMode chainingMode, int mtu);
        [DllImport(LibName)]
        public static extern void egdatagramEncryptorDestruct(IntPtr pDatagramEncryptor);
        [DllImport(LibName)]
        public static extern void egdatagramEncryptorEncrypt(IntPtr pDatagramEncryptor, byte[] pIn, int inSize, byte[] pHeader, byte[] pOut, int outOffset, ref int outSize);
        [DllImport(LibName)]
        public static extern void egdatagramEncryptorDecrypt(IntPtr pDatagramEncryptor, byte[] pIn, int inSize, int inOffset, byte[] pHeader, byte[] pOut, out int outSize);
        [DllImport(LibName)]
        public static extern int egdatagramEncryptorCalculateEncryptedSize(IntPtr pDatagramEncryptor, int unencryptedSize);
        [DllImport(LibName)]
        public static extern int egdatagramEncryptorGetFragmentLength(IntPtr pDatagramEncryptor);

        #if !NETFX_CORE
        [DllImport(LibName)]
        public static extern void egdatagramEncryptorSetLoggingCallback(IntPtr pUserData, LogCallbackDelegate callback);
        #endif
        [DllImport(LibName)]
        public static extern bool egdatagramEncryptorSetLoggingLevel(int level);
        [DllImport(LibName)]
        public static extern int egdatagramEncryptorGetPluginVersion();
#endif

        private enum egDebugLevel
        {
            OFF = 0,
            ERRORS = 1,
            WARNINGS = 2,
            INFO = 3,
            ALL = 4,
        }

        private int loggingLevel;

        public int LogLevel
        {
            get
            {
                return loggingLevel;
            }

            set
            {
                loggingLevel = value;
            }
        }

        protected IntPtr encryptor;

        private static object EncryptorLock = new object();

        ~DatagramEncryptorNative()
        {
            this.Dispose(false);
        }

        #if !NETFX_CORE && (UNITY || SUPPORTED_UNITY)
        [AOT.MonoPInvokeCallback(typeof(LogCallbackDelegate))]
        #endif
        private static void OnNativeLog(IntPtr userData, int debugLevel, string message)
        {
            #if UNITY || SUPPORTED_UNITY
            switch ((egDebugLevel)debugLevel)
            {
                case egDebugLevel.ERRORS:
                    UnityEngine.Debug.LogError("DatagramEncryptorNative: " + message);
                    break;
                case egDebugLevel.WARNINGS:
                    UnityEngine.Debug.LogWarning("DatagramEncryptorNative: " + message);
                    break;
                case egDebugLevel.INFO:
                case egDebugLevel.ALL:
                case egDebugLevel.OFF:
                    UnityEngine.Debug.Log("DatagramEncryptorNative: " + message);
                    break;
            }
            #else
            Debug.WriteLine("DatagramEncryptorNative: " + message);
            #endif
        }

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="encryptionSecret"></param>
        /// <param name="hmacSecret"></param>
        /// <param name="ivBytes"></param>
        public void Init(byte[] encryptionSecret, byte[] hmacSecret, byte[] ivBytes = null, bool chainingModeGCM = true, int mtu = 1200)
        {
            #if !NETFX_CORE && (!ENABLE_MONO || !UNITY_XBOXONE)
            egdatagramEncryptorSetLoggingCallback(IntPtr.Zero, OnNativeLog);
            #endif
            egdatagramEncryptorSetLoggingLevel(this.LogLevel);

            lock (EncryptorLock)
            {
                this.encryptor = egdatagramEncryptorConstruct(encryptionSecret, hmacSecret, ChainingMode.GCM, mtu);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        // ReSharper disable once UnusedParameter.Local
        private void Dispose(bool dispose)
        {
            if (this.encryptor != IntPtr.Zero)
            {
                egdatagramEncryptorDestruct(this.encryptor);
                this.encryptor = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Encrypts data and puts the encryption result prefixed with the unencrypted header into the output buffer
        /// </summary>
        /// <param name="data"></param>
        /// <param name="len"></param>
        /// <param name="header"></param>
        /// <param name="output"></param>
        /// <param name="outOffset"></param>
        /// <param name="outSize"></param>
        /// <returns></returns>
        public void Encrypt2(byte[] data, int len, byte[] header, byte[] output, int outOffset, ref int outSize)
        {
            lock (EncryptorLock)
            {
                egdatagramEncryptorEncrypt(this.encryptor, data, len, header, output, outOffset, ref outSize);
            }
        }

        /// <summary>
        /// Puts the header into 'header', decrypts the rest of data and returns the decrypted data
        /// </summary>
        /// <param name="data">encrypted data</param>
        /// <param name="offset">offset in the input buffer</param>
        /// <param name="len">len of data to decrypt</param>
        /// <param name="header"></param>
        /// <param name="outLen">len of decrypted data</param>
        /// <returns>the decrypted data</returns>
        public byte[] Decrypt2(byte[] data, int offset, int len, byte[] header, out int outLen)
        {
            outLen = data.Length;
            lock (EncryptorLock)
            {
                egdatagramEncryptorDecrypt(encryptor, data, len, offset, header, data, out outLen);
            }
            return data;
        }

        /// <summary>
        /// Calculates the size that data of the specified unencrypted size will have after encryption
        /// </summary>
        /// <param name="unencrypted size">the unencrypted size (including the size of the unencrypted header)</param>
        /// <returns>the calculated encrypted size (size of encrypted data + size of unencrypted header)</returns>
        public int CalculateEncryptedSize(int unencryptedSize)
        {
            return egdatagramEncryptorCalculateEncryptedSize(encryptor, unencryptedSize);
        }

        /// <summary>
        /// Returns the unencrypted length that the payload of each fragment of a fragmented encrypted Enet command will have
        /// </summary>
        /// <returns>the unencrypted fragment length</returns>
        public int CalculateFragmentLength()
        {
            return egdatagramEncryptorGetFragmentLength(encryptor);
        }

        /// <summary>
        /// Returns the version number of the native plugin in the format 'major << 24 | serverMinor << 16 | clientMinor << 8 | patchRelease'
        /// </summary>
        /// <returns>the version number of the native plugin</returns>
        public int GetEncryptorPluginVersion()
        {
            return egdatagramEncryptorGetPluginVersion();
        }
    }
}
