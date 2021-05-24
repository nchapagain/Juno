namespace Juno.Providers
{
    using System.IO;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class DependencyPathsTests
    {
        [Test]
        public void DependencyPathsRootPathMatchesExpected()
        {
            string expectedPath = Path.Combine(Path.GetTempPath(), "Juno");
            Assert.AreEqual(expectedPath, DependencyPaths.RootPath);
        }

        [Test]
        public void DependencyPathsNuGetPackageMatchesExpected()
        {
            string expectedPath = Path.Combine(Path.GetTempPath(), @"Juno\NuGet\Packages");
            Assert.AreEqual(expectedPath, DependencyPaths.NuGetPackages);
        }

        [Test]
        public void DependencyPathsSupportsNuGetPathPlaceholderReferences()
        {
            string path = @"{NuGetPackagePath}\any\path\within";
            string expectedPath = Path.Combine(
                Path.Combine(Path.GetTempPath(), @"Juno\NuGet\Packages"),
                @"any\path\within");

            Assert.AreEqual(expectedPath, DependencyPaths.ReplacePathReferences(path));
        }
    }
}
