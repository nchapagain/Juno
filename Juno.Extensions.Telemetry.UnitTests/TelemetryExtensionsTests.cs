namespace Juno.Extensions.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry.Properties;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.ApplicationInsights;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TelemetryExtensionsTests
    {
        private Fixture mockFixture;
        private EventContext eventContext;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.eventContext = new EventContext(Guid.NewGuid());
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedKeyValuePairPropertiesToTheEventContext()
        {
            string expectedKey = "AnyProperty";
            object expectedValue = 123456;

            this.eventContext.AddContext(expectedKey, expectedValue);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey(expectedKey));
            Assert.AreEqual(expectedValue, this.eventContext.Properties[expectedKey]);
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedComponentInformationToTheEventContext()
        {
            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            this.eventContext.AddContext(component);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("component"));
            Assert.IsTrue(object.ReferenceEquals(component, this.eventContext.Properties["component"]));
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedExperimentInformationToTheEventContext()
        {
            Experiment experiment = this.mockFixture.Create<Experiment>();
            object expectedContext1 = new
            {
                name = experiment.Name,
                description = experiment.Description,
                contentVersion = experiment.ContentVersion,
                schema = experiment.Schema,
                metadata = experiment.Metadata
            };

            object expectedContext2 = experiment.Parameters;
            object expectedContext3 = experiment.Workflow.Select(component => new
            {
                type = component.ComponentType,
                name = component.Name,
                group = component.Group
            });

            this.eventContext.AddContext(experiment);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("experiment"));
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("experimentParameters"));
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("experimentWorkflow"));

            SerializationAssert.JsonEquals(expectedContext1.ToJson(), this.eventContext.Properties["experiment"].ToJson());
            SerializationAssert.JsonEquals(expectedContext2.ToJson(), this.eventContext.Properties["experimentParameters"].ToJson());
            SerializationAssert.JsonEquals(expectedContext3.ToJson(), this.eventContext.Properties["experimentWorkflow"].ToJson());
        }

        [Test]
        public void AddContextExtensionEnsuresTheExperimentObjectPropertiesAreSubdividedSoAsToAvoidExceedingTelmetrySizeConstraints()
        {
            string bigCrazyExperimentJson = TestResources.BigCrazyExperiment;
            Experiment bigCrazyExperiment = bigCrazyExperimentJson.FromJson<Experiment>();
            this.eventContext.Properties.Clear();
            this.eventContext.AddContext(bigCrazyExperiment);

            foreach (var entry in this.eventContext.Properties)
            {
                // This test is very specific to the use of Application Insights as a telemetry target. Application Insights
                // has a maximum single context property size of 8192 characters. We've hit this size constraint numerous times
                // in numerous bug fix passes. So, we are trying to ensure that we do not hit this issue in the future.
                int appInsightsMaxSinglePropertyChars = 8192;
                Assert.DoesNotThrow(() => JsonContextSerialization.Serialize(entry.Value, appInsightsMaxSinglePropertyChars));
            }
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedExperimentInstanceInformationToTheEventContext()
        {
            ExperimentInstance experimentInstance = this.mockFixture.Create<ExperimentInstance>();
            Experiment experiment = experimentInstance.Definition;

            object expectedContext1 = new
            {
                id = experimentInstance.Id,
                name = experiment.Name,
                description = experiment.Description,
                contentVersion = experiment.ContentVersion,
                schema = experiment.Schema,
                metadata = experiment.Metadata,
                created = experimentInstance.Created,
                lastModified = experimentInstance.LastModified,
                _eTag = experimentInstance.GetETag()
            };

            object expectedContext2 = experiment.Parameters;
            object expectedContext3 = experiment.Workflow.Select(component => new
            {
                type = component.ComponentType,
                name = component.Name,
                group = component.Group
            });

            this.eventContext.AddContext(experimentInstance);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("experimentn"));
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("experimentnParameters"));
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("experimentnWorkflow"));

            SerializationAssert.JsonEquals(expectedContext1.ToJson(), this.eventContext.Properties["experimentn"].ToJson());
            SerializationAssert.JsonEquals(expectedContext2.ToJson(), this.eventContext.Properties["experimentnParameters"].ToJson());
            SerializationAssert.JsonEquals(expectedContext3.ToJson(), this.eventContext.Properties["experimentnWorkflow"].ToJson());
        }

        [Test]
        public void AddContextExtensionEnsuresTheExperimentInstanceObjectPropertiesAreSubdividedSoAsToAvoidExceedingTelmetrySizeConstraints()
        {
            string bigCrazyExperimentJson = TestResources.BigCrazyExperimentInstance;
            ExperimentInstance bigCrazyExperiment = bigCrazyExperimentJson.FromJson<ExperimentInstance>();
            this.eventContext.Properties.Clear();
            this.eventContext.AddContext(bigCrazyExperiment);

            foreach (var entry in this.eventContext.Properties)
            {
                // This test is very specific to the use of Application Insights as a telemetry target. Application Insights
                // has a maximum single context property size of 8192 characters. We've hit this size constraint numerous times
                // in numerous bug fix passes. So, we are trying to ensure that we do not hit this issue in the future.
                int appInsightsMaxSinglePropertyChars = 8192;
                Assert.DoesNotThrow(() => JsonContextSerialization.Serialize(entry.Value, appInsightsMaxSinglePropertyChars));
            }
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedExperimentMetadataInformationToTheEventContext()
        {
            ExperimentMetadata metadata = this.mockFixture.Create<ExperimentMetadata>();
            this.eventContext.AddContext(metadata);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("context"));
            Assert.IsTrue(object.ReferenceEquals(metadata, this.eventContext.Properties["context"]));
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedExperimentMetadataInstanceInformationToTheEventContext()
        {
            ExperimentMetadataInstance metadata = this.mockFixture.Create<ExperimentMetadataInstance>();
            metadata.Extensions.Add("entityPool", JToken.FromObject(new List<EnvironmentEntity>
            {
                EnvironmentEntity.Cluster("ClusterA", "Group A"),
                EnvironmentEntity.Node("Node01", "ClusterA", "Group A"),
                EnvironmentEntity.VirtualMachine("VM01", "Node01", "Group A")
            }));

            this.eventContext.AddContext(metadata);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("contextn"));
            SerializationAssert.JsonEquals(
                new
                {
                    id = metadata.Id,
                    experimentId = metadata.Definition.ExperimentId,
                    metadata = metadata.Definition.Metadata?.Select(item => new
                    {
                        item.Key
                    }),
                    created = metadata.Created,
                    lastModified = metadata.LastModified,
                    _eTag = metadata.GetETag()
                }.ToJson(),
                this.eventContext.Properties["contextn"].ToJson());
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedExperimentStepInstanceInformationToTheEventContext()
        {
            ExperimentStepInstance step = this.mockFixture.Create<ExperimentStepInstance>();
            this.eventContext.AddContext(step);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("stepn"));
            SerializationAssert.JsonEquals(
                new
                {
                    id = step.Id,
                    name = step.Definition.Name,
                    agentId = step.AgentId,
                    parentStepId = step.ParentStepId,
                    experimentId = step.ExperimentId,
                    experimentGroup = step.ExperimentGroup,
                    provider = step.Definition.ComponentType,
                    stepType = step.StepType.ToString(),
                    status = step.Status.ToString(),
                    sequence = step.Sequence,
                    attempts = step.Attempts,
                    eTag = step.GetETag()
                }.ToJson(),
                this.eventContext.Properties["stepn"].ToJson());
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedExperimentStepInstancesInformationToTheEventContext()
        {
            ExperimentStepInstance step1 = this.mockFixture.Create<ExperimentStepInstance>();
            ExperimentStepInstance step2 = this.mockFixture.Create<ExperimentStepInstance>();
            IEnumerable<ExperimentStepInstance> steps = new List<ExperimentStepInstance> { step1, step2 };

            object expectedContext = steps.Select(item => new
            {
                id = item.Id,
                name = item.Definition.Name,
                experimentId = item.ExperimentId,
                experimentGroup = item.ExperimentGroup,
                provider = item.Definition.ComponentType,
                stepType = item.StepType.ToString(),
                status = item.Status.ToString(),
                sequence = item.Sequence,
                attempts = item.Attempts,
                eTag = item.GetETag(),
                partition = item.GetPartition()
            });

            this.eventContext.AddContext(steps);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("stepsn"));
            SerializationAssert.JsonEquals(expectedContext.ToJson(), this.eventContext.Properties["stepsn"].ToJson());
        }

        [Test]
        public void AddContextExtensionHandlesUndefinedAgentIdentificationInformationObjects()
        {
            Assert.DoesNotThrow(() => this.eventContext.AddContext(null as AgentIdentification));
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedAgentIdentificationInformationToTheEventContextForAgentsThatRunOnNodes()
        {
            string expectedAgentCluster = "ClusterA";
            string expectedAgentNodeId = "NodeB";
            string expectedAgentContext = Guid.NewGuid().ToString();

            AgentIdentification agentId = new AgentIdentification(expectedAgentCluster, expectedAgentNodeId, context: expectedAgentContext);
            this.eventContext.AddContext(agentId);

            Assert.AreEqual(this.eventContext.Properties["agentId"], agentId.ToString());
            Assert.AreEqual(this.eventContext.Properties["agentCluster"], expectedAgentCluster);
            Assert.AreEqual(this.eventContext.Properties["agentNodeId"], expectedAgentNodeId);
            Assert.AreEqual(this.eventContext.Properties["agentContextId"], expectedAgentContext);
            Assert.IsFalse(this.eventContext.Properties.ContainsKey("agentVmName"));
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedAgentIdentificationInformationToTheEventContextForAgentsThatRunOnVMs()
        {
            string expectedAgentCluster = "ClusterA";
            string expectedAgentNodeId = "NodeB";
            string expectedAgentContext = Guid.NewGuid().ToString();
            string expectedAgentVmName = "VM01";

            AgentIdentification agentId = new AgentIdentification(expectedAgentCluster, expectedAgentNodeId, expectedAgentVmName, expectedAgentContext);
            this.eventContext.AddContext(agentId);

            Assert.AreEqual(this.eventContext.Properties["agentId"], agentId.ToString());
            Assert.AreEqual(this.eventContext.Properties["agentCluster"], expectedAgentCluster);
            Assert.AreEqual(this.eventContext.Properties["agentNodeId"], expectedAgentNodeId);
            Assert.AreEqual(this.eventContext.Properties["agentContextId"], expectedAgentContext);
            Assert.AreEqual(this.eventContext.Properties["agentVmName"], expectedAgentVmName);
        }
    }
}
