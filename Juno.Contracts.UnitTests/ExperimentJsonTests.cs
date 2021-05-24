namespace Juno.Contracts
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentJsonTests
    {
        /*
          Note:
          This particular set of unit tests serves the purpose of ensuring that the Experiment
          object model successfully serializes various JSON representations of the schema that are
          used as illustrative examples in source control. This helps to ensure that the JSON documents
          we have in source control are correct with respect to the object model.

          The JSON documents that are used here exist in the Juno/Documentation/Examples directory in source
          and are used both for test validation and as documented examples.
         */
        private static DirectoryInfo experimentsDirectory;
        private static Assembly testAssembly = Assembly.GetAssembly(typeof(ExperimentJsonTests));

        [OneTimeSetUp]
        public void SetupFixture()
        {
            ExperimentJsonTests.experimentsDirectory = new DirectoryInfo(
                Path.Combine(Path.GetDirectoryName(ExperimentJsonTests.testAssembly.Location), @"Resources\Experiments"));
        }

        [Test]
        [TestCase("A_Experiment.json")]
        [TestCase("A_Experiment2.json")]
        [TestCase("A_Experiment3.json")]
        public void A_ExperimentSerializationVerification(string experimentJsonFile)
        {
            // Ensure that the A Experiment JSON document serializes/deserializes
            // correctly.

            string filePath = Path.Combine(ExperimentJsonTests.experimentsDirectory.FullName, experimentJsonFile);
            string experimentJson = File.ReadAllText(filePath);

            Experiment experiment = experimentJson.FromJson<Experiment>();
            ExperimentJsonTests.AssertExpectedPropertiesDefined(experiment);

            Assert.AreEqual(experiment.GroupNames().Count(), 1);

            // This ensures not only that we are not losing information in the serialization
            // process but that the order of the JSON sections and properties within matches
            // expected.
            string recomposedJson = experiment.ToJson();
            SerializationAssert.JsonEquals(experimentJson, recomposedJson);
        }

        [Test]
        [TestCase("AB_Experiment.json")]
        [TestCase("AB_Experiment2.json")]
        [TestCase("AB_Experiment3.json")]
        public void AB_ExperimentSerializationVerification(string experimentJsonFile)
        {
            // Ensure that the AB Experiment JSON document serializes/deserializes
            // correctly.

            string filePath = Path.Combine(ExperimentJsonTests.experimentsDirectory.FullName, experimentJsonFile);
            string experimentJson = File.ReadAllText(filePath);

            Experiment experiment = experimentJson.FromJson<Experiment>();

            // Core experiment properties
            ExperimentJsonTests.AssertExpectedPropertiesDefined(experiment);
            Assert.AreEqual(experiment.GroupNames().Count(), 2);

            // This ensures not only that we are not losing information in the serialization
            // process but that the order of the JSON sections and properties within matches
            // expected.
            string recomposedJson = experiment.ToJson();
            SerializationAssert.JsonEquals(experimentJson, recomposedJson);
        }

        [Test]
        [TestCase("ABC_Experiment.json")]
        public void ABC_ExperimentSerializationVerification(string experimentJsonFile)
        {
            // Ensure that the ABC Experiment JSON document serializes/deserializes
            // correctly.

            string filePath = Path.Combine(ExperimentJsonTests.experimentsDirectory.FullName, experimentJsonFile);
            string experimentJson = File.ReadAllText(filePath);

            Experiment experiment = experimentJson.FromJson<Experiment>();

            // Core experiment properties
            ExperimentJsonTests.AssertExpectedPropertiesDefined(experiment);
            Assert.AreEqual(experiment.GroupNames().Count(), 3);

            // This ensures not only that we are not losing information in the serialization
            // process but that the order of the JSON sections and properties within matches
            // expected.
            string recomposedJson = experiment.ToJson();
            SerializationAssert.JsonEquals(experimentJson, recomposedJson);
        }

        private static void AssertExpectedPropertiesDefined(Experiment experiment)
        {
            Assert.IsNotNull(experiment);
            Assert.IsNotNull(experiment.ContentVersion);
            Assert.IsNotNull(experiment.Name);
            Assert.IsNotNull(experiment.Description);
            Assert.IsNotNull(experiment.Metadata);
            Assert.IsNotNull(experiment.Parameters);
            Assert.IsNotNull(experiment.Workflow);

            CollectionAssert.IsNotEmpty(experiment.Metadata);
            CollectionAssert.IsNotEmpty(experiment.Parameters);
            CollectionAssert.IsNotEmpty(experiment.Workflow);
        }
    }
}
