namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TipRackTests
    {
        [Test]
        public void FromEnvironmentEntity_ShouldConvertValidEntity()
        {
            var environmentEntity = new EnvironmentEntity(EntityType.Rack, "fooId", "A", new Dictionary<string, IConvertible>()
            {
                { nameof(TipRack.RackLocation), "location" },
                { nameof(TipRack.MachinePoolName), "mp" },
                { nameof(TipRack.ClusterName), "cluster" },
                { nameof(TipRack.CpuId), "cpu" },
                { nameof(TipRack.Region), "region" },
                { nameof(TipRack.NodeList), "n1;n2" },
                { nameof(TipRack.SupportedVmSkus), "d2;d4" },
                { nameof(TipRack.PreferredVmSku), "d2" },
                { nameof(TipRack.RemainingTipSessions), 2 }
            });
            var tipRack = TipRack.FromEnvironmentEntity(environmentEntity);
            Assert.AreEqual("location", tipRack.RackLocation);
            Assert.AreEqual("mp", tipRack.MachinePoolName);
            Assert.AreEqual("cluster", tipRack.ClusterName);
            Assert.AreEqual(2, tipRack.NodeList.Count);
            Assert.AreEqual("d2", tipRack.PreferredVmSku);
            Assert.AreEqual(2, tipRack.SupportedVmSkus.Count);
            Assert.AreEqual("n1", tipRack.NodeList.First());
            Assert.AreEqual("location", tipRack.RackLocation);
        }

        [Test]
        public void FromEnvironmentEntity_ShouldFailInvalidEntity_MissingMachinePool()
        {
            var environmentEntity = new EnvironmentEntity(EntityType.Rack, "fooId", "A", new Dictionary<string, IConvertible>()
            {
                { nameof(TipRack.RackLocation), "location" },
                { nameof(TipRack.ClusterName), "cluster" },
                { nameof(TipRack.CpuId), "cpu" },
                { nameof(TipRack.Region), "region" },
                { nameof(TipRack.NodeList), "n1;n2" },
                { nameof(TipRack.SupportedVmSkus), "d2;d4" },
                { nameof(TipRack.RemainingTipSessions), 2 }
            });
            Assert.That(
                () => TipRack.FromEnvironmentEntity(environmentEntity),
                Throws.TypeOf<KeyNotFoundException>().With.Message.Contains($"'{nameof(TipRack.MachinePoolName)}' does not exist"));
        }

        [Test]
        public void ToEnvironmentEntity_ShouldConvertValidEntity()
        {
            var tipRack = new TipRack()
            {
                NodeList = new List<string>() { "n1", "n2" },
                SupportedVmSkus = new List<string>() { "d2", "d3" },
                RackLocation = "location",
                ClusterName = "cluster",
                RemainingTipSessions = 2,
                Region = "region",
                CpuId = "cpuid",
                MachinePoolName = "mp"
            };
            var entity = TipRack.ToEnvironmentEntity(tipRack);
            Assert.AreEqual("n1;n2", entity.Metadata.GetValue<string>(nameof(TipRack.NodeList)));
            Assert.AreEqual("d2;d3", entity.Metadata.GetValue<string>(nameof(TipRack.SupportedVmSkus)));
            Assert.AreEqual("region", entity.Metadata.GetValue<string>(nameof(TipRack.Region)));
            Assert.AreEqual("2", entity.Metadata.GetValue<string>(nameof(TipRack.RemainingTipSessions)));
            Assert.AreEqual("cluster", entity.Metadata.GetValue<string>(nameof(TipRack.ClusterName)));
        }
    }
}