namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Identity.Client;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class GetAccessTokenTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestGetAccessToken cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Mock<IPublicClientApplication> mockPublicClientApplication;
        private AuthenticationResult expectedAuthenticationResult;
        private Mock<IAccount> mockAccount;
        private string expectedAccessToken;
        private string expectedUserName;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockPublicClientApplication = new Mock<IPublicClientApplication>();
            this.mockAccount = new Mock<IAccount>();
            this.expectedAuthenticationResult = new AuthenticationResult(
                this.expectedAccessToken, 
                true, 
                "aUId", 
                DateTimeOffset.MaxValue, 
                DateTimeOffset.MaxValue, 
                "aTenantId", 
                this.mockAccount.Object, 
                "anIdToken", 
                new List<string>(), 
                Guid.NewGuid());

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.cmdlet = new TestGetAccessToken();
            this.cmdlet.ServiceUri = null;
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.cmdlet.AsJson = false;
            this.expectedAccessToken = "anAccessToken";
            this.expectedUserName = "aUserName";

            List<IAccount> accountsList = new List<IAccount>();
            accountsList.Add(this.mockAccount.Object);

            this.mockAccount.Setup(m => m.Username)
                .Returns(this.expectedUserName);

            this.mockPublicClientApplication.Setup(m => m.GetAccountsAsync())
                .ReturnsAsync(accountsList);

            this.cmdlet.ClientApplication = this.mockPublicClientApplication.Object;
        }

        [TearDown]
        public void TearDown()
        {
            this.cmdlet.Dispose();
        }

        [Test]
        public void CmdletGetAccessTokenAsyncThrowsExpectedException()
        {
            bool msalExceptionThrown = false;
            bool msalUiRequiredExceptionThrown = false;
            MsalException expectedMsalException = new MsalException("errorCode2", "exceptionMessage2");
            MsalUiRequiredException expectedMsalUiRequiredException = new MsalUiRequiredException("errorCode", "exceptionMessage");

            this.cmdlet.TokenResult = this.expectedAuthenticationResult;
            this.cmdlet.OnAcquireTokenSilent = (application, actualScopes, accounts) =>
            {
                this.AssertScopeAndAMEValues(actualScopes);
                Assert.AreEqual(application, this.mockPublicClientApplication.Object);
                Assert.AreEqual(accounts.FirstOrDefault(), this.mockAccount.Object);

                throw expectedMsalUiRequiredException;
            };

            this.cmdlet.OnAcquireTokenInteractive = (application, actualScopes) =>
            {
                msalExceptionThrown = true;
                this.AssertScopeAndAMEValues(actualScopes);
                Assert.AreEqual(application, this.mockPublicClientApplication.Object);

                throw expectedMsalException;
            };

            try
            {
                Assert.IsNull(this.cmdlet.ServiceUri);
                this.cmdlet.ProcessInternal();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e is NullReferenceException);
                Assert.AreNotEqual(expectedMsalUiRequiredException.GetType(), e.GetType());
                Assert.AreNotEqual(expectedMsalUiRequiredException.Message, e.Message);
                Assert.AreNotEqual(expectedMsalUiRequiredException, e);
                msalUiRequiredExceptionThrown = true;
                Assert.AreEqual(this.cmdlet.ServiceUri, "https://junodev01experiments.azurewebsites.net");
                Assert.IsNull(GetAccessToken.Username);
                Assert.AreEqual(GetAccessToken.ServiceEndpoint, this.cmdlet.ServiceUri.AbsoluteUri);
                Assert.IsNull(GetAccessToken.AccessToken);
            }

            Assert.IsTrue(msalExceptionThrown);
            Assert.IsTrue(msalUiRequiredExceptionThrown);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Zero(this.currentRetries);
        }

        ////[Test]
        ////public void CmdletWritesExpectedObject()
        ////{
        ////    this.cmdlet.AsJson = true;

        ////    this.cmdlet.OnAcquireTokenSilent = (application, actualScopes, accounts) =>
        ////    {
        ////        this.AssertScopeAndAMEValues(actualScopes);
        ////        Assert.AreEqual(application, this.mockPublicClientApplication.Object);
        ////        Assert.AreEqual(accounts.FirstOrDefault(), this.mockAccount.Object);
        ////    };

        ////    this.cmdlet.OnAcquireTokenInteractive = (application, actualScopes) =>
        ////    {
        ////        this.AssertScopeAndAMEValues(actualScopes);
        ////        Assert.AreEqual(application, this.mockPublicClientApplication.Object);
        ////        Assert.AreEqual(this.cmdlet.ServiceUri, "https://junodev01experiments.azurewebsites.net");
        ////        Assert.IsNull(GetAccessToken.Username);
        ////        Assert.IsNull(GetAccessToken.ServiceEndpoint);
        ////        Assert.IsNull(GetAccessToken.AccessToken);
        ////    };

        ////    try
        ////    {
        ////        Assert.IsNull(this.cmdlet.ServiceUri);
        ////        this.cmdlet.ProcessInternal();
        ////    }
        ////    catch (Exception)
        ////    {
        ////        Assert.Fail();
        ////    }

        ////    Assert.IsNotNull(this.cmdlet.Results);
        ////    Assert.AreEqual(null, this.cmdlet.ServiceUri);
        ////    Assert.IsTrue(this.cmdlet.AsJson);
        ////    Assert.IsTrue(this.cmdlet.IsJsonObject);
        ////}

        private void AssertScopeAndAMEValues(IEnumerable<string> actualScopes, bool expectedAMEValue = false)
        {
            Assert.IsTrue(actualScopes.FirstOrDefault().StartsWith("api://"));
            Assert.IsTrue(actualScopes.FirstOrDefault().EndsWith("/user_impersonation"));
            Assert.IsTrue(actualScopes.LastOrDefault().Equals("User.Read"));
            Assert.AreEqual(expectedAMEValue, this.cmdlet.IsAMEValue);
        }

        private class TestGetAccessToken : GetAccessToken
        {
            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public IPublicClientApplication ClientApplication { get; set; }

            public AuthenticationResult TokenResult { get; set; }

            public Action<IPublicClientApplication, IEnumerable<string>, IEnumerable<IAccount>> OnAcquireTokenSilent { get; set; }

            public Action<IPublicClientApplication, IEnumerable<string>> OnAcquireTokenInteractive { get; set; }

            public bool IsAMEValue
            {
                get
                {
                    return this.IsAME;
                }
            }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            protected override IPublicClientApplication GetPublicClientApplication()
            {
                return this.ClientApplication;
            }

            protected override Task<AuthenticationResult> AcquireTokenSilentExecutionResultAsync(IPublicClientApplication application, IEnumerable<string> scopes, IEnumerable<IAccount> accounts)
            {
                this.OnAcquireTokenSilent?.Invoke(application, scopes, accounts);
                return Task.FromResult(this.TokenResult);
            }

            protected override Task<AuthenticationResult> AcquireTokenInteractiveExecutionResultAsync(IPublicClientApplication application, IEnumerable<string> scopes)
            {
                this.OnAcquireTokenInteractive?.Invoke(application, scopes);
                return Task.FromResult(this.TokenResult);
            }

            protected override void WriteResults(object results)
            {
                this.Results = results;
                this.IsJsonObject = false;
            }

            protected override void WriteResultsAsJson(object results)
            {
                this.Results = results;
                this.IsJsonObject = true;
            }
        }
    }
}
