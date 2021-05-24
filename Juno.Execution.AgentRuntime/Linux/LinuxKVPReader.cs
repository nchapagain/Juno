using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Azure.CRC.Platform;

namespace Juno.Execution.AgentRuntime.Linux
{
    /// <summary>
    /// A utility for communication between the Linux VM and the Host. (Requires Hyper-V, likely will only function on Azure.)
    /// 
    /// Documentation Reference:
    /// https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2012-R2-and-2012/dn798287(v=ws.11)
    /// https://github.com/ejsiron/hvkvp (MIT License allows unrestricted use)
    /// </summary>
    public static class LinuxKVPReader
    {
        /// <summary>
        /// This value must match HV_KVP_EXCHANGE_MAK_KEY_SIZE in hyperv.h
        /// Right now this is hardcoded, however ideally we will be able to locate hyperv.h and read the value from it, because if this value is not correct, that will create unusable results.
        /// It's unclear in what situation this would not be 0x200, my guess is this would only happen if someone decided to change it themselves, yet the documentation suggests this value should always be used instead of a hardcoded value.
        /// </summary>
        public const int MaxPairKeySize = 0x200;

        /// <summary>
        /// See "MaxPairKeySize"'s documentation.
        /// </summary>
        public const int MaxPairValueSize = 0x800;

        /// <summary>
        /// Pool 0:
        /// Read-Only
        /// This is a host-to-guest channel which seems to get populated by Azure.
        /// 
        /// </summary>
        public const string Pool0 = "/var/lib/hyperv/.kvp_pool_0";

        /// <summary>
        /// Pool 1:
        /// Write-Only
        /// This is how commands can get sent to the host.
        /// </summary>
        public const string Pool1 = "/var/lib/hyperv/.kvp_pool_1";

        /// <summary>
        /// Pool 3:
        /// Read-Only
        /// Equivalent to "Virtual Machine\Guest\Parameter"
        /// Hyper-V is what 
        /// Example of Contained Data:
        /// VirtualMachineName=f19dd4f5-b29c-4e4e-baa7-8735f5d9f918
        /// HostName=MWH041030207028
        /// </summary>
        public const string Pool3 = "/var/lib/hyperv/.kvp_pool_3";

        /// <summary>
        /// Reads a key value pair pool into a Dictionary.
        /// </summary>
        /// <param name="poolFilePath">The path to the pool file to read from</param>
        /// <returns>readValues</returns>
        /// <exception cref="FileNotFoundException">Thrown if the pool is not found on the filesystem.</exception>
        /// <exception cref="InvalidDataException">Thrown if the data read is not in the expected format.</exception>
        public static Dictionary<string, string> ReadKeyValuePairs(string poolFilePath)
        {
            if (!File.Exists(poolFilePath))
            {
                throw new FileNotFoundException(poolFilePath);
            }

            bool isUnix = (PlatformUtil.CurrentPlatform == PlatformID.Unix); // Windows runs this method during tests, but needs some slightly different behavior. This is not optimal, but it's the only realistic option.
            Dictionary<string, string> resultMap = new Dictionary<string, string>();
            using (FileStream fs = new FileStream(poolFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // I would like to use fs.Lock to lock the file so it can't be written to while we're accessing it, however 
                // https://github.com/dotnet/runtime/issues/29173 shows this isn't possible at the moment.

                byte[] keyBuffer = new byte[LinuxKVPReader.MaxPairKeySize];
                byte[] valBuffer = new byte[LinuxKVPReader.MaxPairValueSize];

                // Read key value pairs until there is nothing left to read.
                string readKey = null;
                int lastReadLength;
                byte[] useBuffer = keyBuffer;
                while ((lastReadLength = fs.Read(useBuffer, 0, useBuffer.Length)) > 0)
                {
                    bool readingValue = (readKey != null);
                    if (lastReadLength != useBuffer.Length)
                    {
                        if (isUnix)
                        {
                            fs.Unlock(0, fs.Length);
                        }

                        throw new InvalidDataException(string.Format(
                            "Read {0} did not match expected size! (Pool: {1}, Address: 0x{2:X}, Read: {3}, Expected: {4})",
                            (readingValue ? "value" : "key"),
                            poolFilePath,
                            fs.Position - lastReadLength,
                            lastReadLength, useBuffer.Length));
                    }

                    // Determine actual string length.
                    int stringByteLength;
                    for (stringByteLength = 0; stringByteLength < useBuffer.Length; stringByteLength++)
                    {
                        if (useBuffer[stringByteLength] == 0x00)
                        {
                            break;
                        }
                    }

                    // Verify the data follows the spec.
                    // If this error throws, it most likely means the key value sizes were incorrect.
                    for (int i = stringByteLength + 1; i < useBuffer.Length; i++)
                    {
                        if (useBuffer[i] != 0x00)
                        {
                            if (isUnix)
                            {
                                fs.Unlock(0, fs.Length);
                            }

                            throw new InvalidDataException(string.Format(
                                "{0} had non-null value past the string termination. (Pool: {1}, Address: 0x{2:X})",
                                (readingValue ? "Value" : "Key"),
                                poolFilePath,
                                fs.Position - useBuffer.Length + i));
                        }
                    }

                    if (stringByteLength == 0)
                    {
                        continue; // There is no data to read, therefore this will be skipped.
                    }

                    byte[] stringBytes = new byte[stringByteLength];
                    Array.Copy(useBuffer, 0, stringBytes, 0, stringByteLength);
                    string tempString = Encoding.ASCII.GetString(stringBytes); // There is no clear documentation on the charset / encoding being ASCII, but given the provided example C++ code in the MS documentation seems to just use char[], and the data looks like ASCII, I'm just going with thats
                    if (readingValue)
                    {
                        resultMap[readKey] = tempString;
                        useBuffer = keyBuffer;
                        readKey = null;
                    }
                    else
                    {
                        readKey = tempString;
                        useBuffer = valBuffer;
                    }
                }

                if (isUnix)
                {
                    fs.Unlock(0, fs.Length);
                }

                if (readKey != null)
                {
                    throw new InvalidDataException(string.Format(
                        "The last KeyValuePair only had a key? (Pool: {0}, Key: \"{1}\")",
                        poolFilePath,
                        readKey));
                }
            }

            return resultMap;
        }
    }
}