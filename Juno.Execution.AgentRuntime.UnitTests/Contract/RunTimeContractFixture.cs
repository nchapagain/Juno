namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using AutoFixture;

    public class RunTimeContractFixture : Fixture
    {
        public RunTimeContractFixture SetUpRuntimeContracts()
        {
            this.Register<SsdDrive>(() => RunTimeContractFixture.CreateSsdDrive());
            this.Register<SsdDrives>(() => RunTimeContractFixture.CreateSsdDrives());
            this.Register<NvmeHealth>(() => RunTimeContractFixture.CreateNvmeHealth());
            this.Register<SsdDrive>(() => RunTimeContractFixture.CreateSsdDrive());
            this.Register<SsdHealth>(() => RunTimeContractFixture.CreateSsdHealth());
            this.Register<NvmeInfo>(() => RunTimeContractFixture.CreateNvmeInfo());
            this.Register<SataInfo>(() => RunTimeContractFixture.CreateSataInfo());
            this.Register<SataSmartAttribute>(() => RunTimeContractFixture.CreateSataSmartAttribute());
            this.Register<SataSmartAttributes>(() => RunTimeContractFixture.CreateSataSmartAttributes());
            return this;
        }

        private static SsdHealth CreateSsdHealth()
        {
            return new SsdHealth(Guid.NewGuid().ToString());
        }

        private static SsdDrive CreateSsdDrive()
        {
            return new SsdDrive(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString());
        }

        private static SsdDrives CreateSsdDrives()
        {
            return new SsdDrives(new List<SsdDrive>()
            {
                RunTimeContractFixture.CreateSsdDrive()
            });
        }

        private static NvmeHealth CreateNvmeHealth()
        {
            return new NvmeHealth(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
        }

        private static NvmeInfo CreateNvmeInfo()
        {
            return new NvmeInfo(
                RunTimeContractFixture.CreateSsdDrive(),
                RunTimeContractFixture.CreateSsdHealth(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                RunTimeContractFixture.CreateNvmeHealth());
        }

        private static SataSmartAttribute CreateSataSmartAttribute()
        {
            return new SataSmartAttribute(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString());
        }

        private static SataSmartAttributes CreateSataSmartAttributes()
        {
            return new SataSmartAttributes(new List<SataSmartAttribute>() { RunTimeContractFixture.CreateSataSmartAttribute() });
        }

        private static SataInfo CreateSataInfo()
        {
            return new SataInfo(
                RunTimeContractFixture.CreateSsdDrive(),
                RunTimeContractFixture.CreateSsdHealth(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                RunTimeContractFixture.CreateSataSmartAttributes());
        }
    }
}
