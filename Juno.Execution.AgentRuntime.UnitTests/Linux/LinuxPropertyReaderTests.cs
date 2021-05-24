using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Juno.Execution.AgentRuntime.Windows;
using Microsoft.Win32;
using Moq;
using NUnit.Framework;

namespace Juno.Execution.AgentRuntime.Linux
{
    [TestFixture]
    [Category("Unit")]
    public class LinuxPropertyReaderTests
    {
        [Test]
        public void ReadsKVPValueSuccessfully()
        {
            string tempFile = Path.GetTempFileName();
            using (BinaryWriter writer = new BinaryWriter(new FileStream(tempFile, FileMode.Open, FileAccess.Write)))
            {
                writer.Write(Encoding.ASCII.GetBytes("exampleKey"));
                while (writer.BaseStream.Position < LinuxKVPReader.MaxPairKeySize)
                {
                    writer.Write((byte)0x00);
                }

                writer.Write(Encoding.ASCII.GetBytes("exampleValue"));
                while (writer.BaseStream.Position < LinuxKVPReader.MaxPairKeySize + LinuxKVPReader.MaxPairValueSize)
                {
                    writer.Write((byte)0x00);
                }
            }

            Dictionary<string, string> keyValuePairs = LinuxKVPReader.ReadKeyValuePairs(tempFile);
            File.Delete(tempFile);
            Assert.AreEqual(1, keyValuePairs.Count);
            Assert.IsTrue(keyValuePairs.ContainsKey("exampleKey"));
            Assert.AreEqual("exampleValue", keyValuePairs["exampleKey"]);
        }
    }
}
