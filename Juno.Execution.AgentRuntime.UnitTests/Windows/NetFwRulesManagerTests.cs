namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Juno.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class NetFwRulesManagerTests
    {
        [Test]
        public void NetFwRuleManagerAppliesInvalidRuleParameters()
        {
            TestNetFwRulesManager testNetFwRulesManager = new TestNetFwRulesManager();

            testNetFwRulesManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                return true;
            };

            Assert.Throws<ArgumentException>(() => testNetFwRulesManager.DeployRules("Invalid parameters rule", null, null, null, null, null, "IN", "BLOCK"));
            Assert.Throws<ArgumentException>(() => testNetFwRulesManager.DeployRules("Invalid parameters rule", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "OUT", "BLOCK"));
            Assert.Throws<ProviderException>(() => testNetFwRulesManager.DeployRules("Invalid parameters rule", "143", "5000-5020", "192.168.0.4", "143", @"z:\bad.exe", "bogus", "bogus"));
        }

        [Test]
        public void NetFwRulesManagerAppliesTheExpectedInboundBlockFirewallRules()
        {
            const string ruleName = "Juno created inbound block rule";
            const string remotePorts = "17000";
            const string remoteAddresses = "192.168.0.4";
            const string directionality = "in";
            const string action = "block";

            Dictionary<string, object> expectedInvokeMemberParams = new Dictionary<string, object>()
            {
                { "Name", new object[] { ruleName } },
                { "Description", new object[] { "Rule created during Juno experiment by the NetFwRulesProvider." } },
                { "Direction", new object[1] { 1 } },
                { "Enabled", new object[1] { true } },
                { "InterfaceTypes", new object[] { "All" } },
                { "Protocol", new object[1] { 6 } },
                { "RemotePorts", new object[] { remotePorts } },
                { "RemoteAddresses", new object[] { remoteAddresses } },
                { "Action", new object[1] { 0 } },
                { "Rules", null },
                { "Add", new object[1] }
            };

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();
            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                firewallRuleProperties.Add(propName, propValue);

                return firewallRuleProperties;
            };

            testFwRuleManager.DeployRules(
                ruleName, 
                remotePorts, 
                string.Empty, 
                remoteAddresses, 
                string.Empty, 
                string.Empty, 
                directionality, 
                action);

            NetFwRulesManagerTests.ValidateInvokeMethodProperties(
                firewallRuleProperties, 
                expectedInvokeMemberParams, 
                new int[] { 0, 1, 6, 7 });
        }

        [Test]
        public void NetFwRulesManagerAppliesTheExpectedOutboundBlockFirewallRules()
        {
            const string ruleName = "Juno created outbound block rule";
            const string localPorts = "49450-65535";
            const string localAddresses = "";
            const string directionality = "out";
            const string action = "block";

            Dictionary<string, object> expectedInvokeMemberParams = new Dictionary<string, object>()
            {
                { "Name", new object[] { ruleName } },
                { "Description", new object[] { "Rule created during Juno experiment by the NetFwRulesProvider." } },
                { "Direction", new object[1] { 2 } },
                { "Enabled", new object[1] { true } },
                { "InterfaceTypes", new object[] { "All" } },
                { "Protocol", new object[1] { 6 } },
                { "LocalPorts", new object[] { localPorts } },
                { "Action", new object[1] { 0 } },
                { "Rules", null },
                { "Add", new object[1] }
            };

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();
            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                firewallRuleProperties.Add(propName, propValue);

                return firewallRuleProperties;
            };

            testFwRuleManager.DeployRules(
                ruleName, 
                string.Empty, 
                localPorts, 
                string.Empty, 
                localAddresses, 
                string.Empty,
                directionality,
                action);

            NetFwRulesManagerTests.ValidateInvokeMethodProperties(
                firewallRuleProperties, 
                expectedInvokeMemberParams, 
                new int[] { 0, 1, 6, 7 });
        }

        [Test]
        public void NetFwRulesManagerAppliesTheExpectedOutboundApplicationAllowFirewallRules()
        {
            const string ruleName = "Juno created application outbound allow rule";
            const string application = @"%systemDrive%\Program Files\MyApplication.exe";
            const string directionality = "out";
            const string action = "allow";

            Dictionary<string, object> expectedInvokeMemberParams = new Dictionary<string, object>()
            {
                { "DefaultOutboundAction0", new object[2] { 1, 0 } },
                { "DefaultOutboundAction1", new object[2] { 2, 0 } },
                { "DefaultOutboundAction2", new object[2] { 4, 0 } },
                { "Name", new object[] { ruleName } },
                { "Description", new object[] { "Rule created during Juno experiment by the NetFwRulesProvider." } },
                { "Direction", new object[1] { 2 } },
                { "Enabled", new object[1] { true } },
                { "InterfaceTypes", new object[] { "All" } },
                { "Protocol", new object[1] { 6 } },
                { "Applicationname", new object[] { @"%systemDrive%\Program Files\MyApplication.exe" } },
                { "Action", new object[1] { 0 } },
                { "Rules", null },
                { "Add", new object[1] }
            };

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();

            int iterationCallDefaultOutboundAction = 0;

            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                if (string.Equals(propName, "DefaultOutboundAction", StringComparison.OrdinalIgnoreCase))
                {
                    firewallRuleProperties.Add(propName + iterationCallDefaultOutboundAction++, propValue);
                }
                else
                {
                    firewallRuleProperties.Add(propName, propValue);
                }

                return firewallRuleProperties;
            };

            testFwRuleManager.DeployRules(
                ruleName,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                application,
                directionality,
                action);

            NetFwRulesManagerTests.ValidateInvokeMethodProperties(
                firewallRuleProperties, 
                expectedInvokeMemberParams, 
                new int[] { 0, 1, 6, 7 });
        }

        [Test]
        public void NetFwRulesManagerAppliesTheExpectedOutboundApplicationAllowFirewallRulesWithTipChangeIdLocation()
        {
            const string ruleName = "Juno created application outbound allow rule";
            const string application = @"%TIPCHANGEID%\Juno.HostAgent.exe";
            const string directionality = "out";
            const string action = "allow";

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();

            int iterationCallDefaultOutboundAction = 0;

            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                if (string.Equals(propName, "DefaultOutboundAction", StringComparison.OrdinalIgnoreCase))
                {
                    firewallRuleProperties.Add(propName + iterationCallDefaultOutboundAction++, propValue);
                }
                else
                {
                    firewallRuleProperties.Add(propName, propValue);
                }

                return firewallRuleProperties;
            };

            testFwRuleManager.DeployRules(
                ruleName,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                application,
                directionality,
                action);

            Assert.IsTrue((firewallRuleProperties["Applicationname"] as object[])[0].ToString().Contains("Juno.Execution.AgentRuntime.UnitTests\\net5.0\\Juno.HostAgent.exe"));
        }

        [Test]
        public void NetFwRulesManagerAppliesTheExpectedOutboundLocalPortsAllowFirewallRules()
        {
            const string ruleName = "Juno created inbound block rule";
            const string localPorts = "49301-49449";
            const string directionality = "out";
            const string action = "allow";

            Dictionary<string, object> expectedInvokeMemberParams = new Dictionary<string, object>()
             {
                { "DefaultOutboundAction0", new object[2] { 1, 0 } },
                { "DefaultOutboundAction1", new object[2] { 2, 0 } },
                { "DefaultOutboundAction2", new object[2] { 4, 0 } },
                { "Name", new object[] { ruleName } },
                { "Description", new object[] { "Rule created during Juno experiment by the NetFwRulesProvider." } },
                { "Direction", new object[1] { 2 } },
                { "Enabled", new object[1] { true } },
                { "InterfaceTypes", new object[] { "All" } },
                { "Protocol", new object[1] { 6 } },
                { "LocalPorts", new object[] { localPorts } },
                { "Action", new object[1] { 0 } },
                { "Rules", null },
                { "Add", new object[1] }
             };

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();

            int iterationCallDefaultOutboundAction = 0;

            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                if (string.Equals(propName, "DefaultOutboundAction", StringComparison.OrdinalIgnoreCase))
                {
                    firewallRuleProperties.Add(propName + iterationCallDefaultOutboundAction++, propValue);
                }
                else
                {
                    firewallRuleProperties.Add(propName, propValue);
                }

                return firewallRuleProperties;
            };

            testFwRuleManager.DeployRules(
               ruleName,
               string.Empty,
               localPorts,
               string.Empty,
               string.Empty,
               string.Empty,
               directionality,
               action);

            NetFwRulesManagerTests.ValidateInvokeMethodProperties(
               firewallRuleProperties,
               expectedInvokeMemberParams,
               new int[] { 0, 1, 2, 3,  5, 9, 10, 11 });
        }

        [Test]
        public void NetFwRulesManagerRemovedBlockFirewallRule()
        {
            const string ruleName = "Juno created application outbound allow rule";
            const string directionality = "in";
            const string action = "block";

            Dictionary<string, object> expectedInvokeMemberParams = new Dictionary<string, object>()
            {
                { "Rules", null },
                { "Remove", new object[1] }
            };

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();

            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                firewallRuleProperties.Add(propName, propValue);

                return firewallRuleProperties;
            };

            testFwRuleManager.RemoveRules(ruleName, directionality, action);

            NetFwRulesManagerTests.ValidateInvokeMethodProperties(
                firewallRuleProperties,
                expectedInvokeMemberParams,
                new int[] { 0, 1 });
        }

        [Test]
        public void NetFwRulesManagerRemovedAllowFirewallRule()
        {
            const string ruleName = "Juno created application outbound allow rule";
            const string directionality = "out";
            const string action = "allow";

            Dictionary<string, object> expectedInvokeMemberParams = new Dictionary<string, object>()
            {
                { "DefaultOutboundAction0", new object[2] { 1, 0 } },
                { "DefaultOutboundAction1", new object[2] { 2, 0 } },
                { "DefaultOutboundAction2", new object[2] { 4, 0 } },
                { "Rules", null },
                { "Remove", new object[1] }
            };

            Dictionary<string, object> firewallRuleProperties = new Dictionary<string, object>();

            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();

            int iterationCallDefaultOutboundAction = 0;

            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                if (string.Equals(propName, "DefaultOutboundAction", StringComparison.OrdinalIgnoreCase))
                {
                    firewallRuleProperties.Add(propName + iterationCallDefaultOutboundAction++, propValue);
                }
                else
                {
                    firewallRuleProperties.Add(propName, propValue);
                }

                return firewallRuleProperties;
            };

            testFwRuleManager.RemoveRules(ruleName, directionality, action);

            NetFwRulesManagerTests.ValidateInvokeMethodProperties(
                firewallRuleProperties, 
                expectedInvokeMemberParams,
                new int[] { 0, 1, 2, 3, 4 });
        }

        [Test]
        public void NetFwRulesManagerThrowsWhenFailingToApplyFirewallRules()
        {
            TestNetFwRulesManager testFwRuleManager = new TestNetFwRulesManager();

            testFwRuleManager.OnProcessInvokeMember = (propName, objectToSet, propValue, bindings) =>
            {
                throw new TargetInvocationException(new Exception());
            };

            Assert.Throws<TargetInvocationException>(() => testFwRuleManager.DeployRules(
                "rule name", 
                "bogus", 
                string.Empty, 
                "192.168.0.4", 
                string.Empty, 
                string.Empty, 
                "in", 
                "block"));

            Assert.Throws<TargetInvocationException>(() => testFwRuleManager.DeployRules(
                "rule name", 
                "17000", 
                string.Empty, 
                "bogus", 
                string.Empty, 
                string.Empty, 
                "in", 
                "block"));
        }

        private static void ValidateInvokeMethodProperties(
            Dictionary<string, object> actual, 
            Dictionary<string, object> expected,
            int[] elementsToValidate)
        {
            Assert.IsTrue(actual.Count == expected.Count);

            for (int i = 0; i < actual.Count; i++)
            {
                Assert.IsTrue(actual.ElementAt(i).Key == expected.ElementAt(i).Key);
            }

            foreach (int i in elementsToValidate)
            {
                Assert.IsTrue(actual.ElementAt(i).Value as string == expected.ElementAt(i).Value as string);
            }
        }

        private class TestNetFwRulesManager : NetFwRulesManager
        {
            public Func<string, object, object[], BindingFlags, object> OnProcessInvokeMember { get; set; }

            protected override object InvokeMember(string propertyName, object objectToSet, object[] propertyValue, BindingFlags bindingFlag)
            {
                return this.OnProcessInvokeMember?.Invoke(propertyName, objectToSet, propertyValue, bindingFlag);
            }
        }
    }
}
