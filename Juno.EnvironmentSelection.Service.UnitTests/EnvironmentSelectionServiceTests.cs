namespace Juno.EnvironmentSelection.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.EnvironmentSelection.ClusterSelectionFilters;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentSelectionServiceTests
    {
        public static readonly IDictionary<string, EnvironmentCandidate> SetOne = new Dictionary<string, EnvironmentCandidate>()
        {
            { "node1", new EnvironmentCandidate(null, "cluster", null, null, null, "node1", null, null) },
            { "node2", new EnvironmentCandidate(null, "cluster", null, null, null, "node2", null, null) }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SetTwo = new Dictionary<string, EnvironmentCandidate>()
        {
            { "node2", new EnvironmentCandidate(null, null, "region1", null, null, "node2", null, null) },
            { "node1", new EnvironmentCandidate(null, null, "region3", null, null, "node1", null, null) },
            { "node3", new EnvironmentCandidate(null, null, "region4", null, null, "node3", null, null) }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SetThree = new Dictionary<string, EnvironmentCandidate>()
        {
            { "node1", new EnvironmentCandidate(null, null, null, "machinesDontSwim", null, "node1", null, "cpuid10") }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SetFour = new Dictionary<string, EnvironmentCandidate>()
        {
            { "node4", new EnvironmentCandidate(null, null, null, "machinesDontSwim", null, "node4", null, "cpuid10") }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SetFive = new Dictionary<string, EnvironmentCandidate>()
        {
            { "node2", new EnvironmentCandidate(null, "cluster1", "region1", null, null, "node2", null, null) },
            { "node1", new EnvironmentCandidate(null, null, "region3", null, null, "node1", null, null) },
            { "node3", new EnvironmentCandidate(null, null, "region4", null, null, "node3", null, null) },
            { "node4", new EnvironmentCandidate(null, "cluster2", "region1", null, null, "node4", null, null) }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SubscriptionSetOne = new Dictionary<string, EnvironmentCandidate>()
        {
            { "subscription1", new EnvironmentCandidate("subscription1") }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SubscriptionSetTwo = new Dictionary<string, EnvironmentCandidate>()
        {
            { "subscription1", new EnvironmentCandidate("subscription1", "cluster", null, null, null, null, null, "cpuid10") },
            { "subscription2", new EnvironmentCandidate("subscription2", null, null, "machinesDontSwim", null, "node1", null, "cpuid10") }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> SubscriptionSetThree = new Dictionary<string, EnvironmentCandidate>()
        {
            { "subscription3", new EnvironmentCandidate("subscription3", "cluster", null, null, null, null, null, "cpuid10") },
            { "subscription4", new EnvironmentCandidate("subscription4", null, null, "machinesDontSwim", null, "node1", null, "cpuid10") }
        };

        public static readonly IDictionary<string, EnvironmentCandidate> ClusterSet = new Dictionary<string, EnvironmentCandidate>()
        {
            { "clusterId1", new EnvironmentCandidate(null, "cluster", vmSku: new List<string>() { "vm1", "vm2" }) },
            { "clusterId2", new EnvironmentCandidate(null, "cluster2", vmSku: new List<string>() { "vm1", "vm2" }) }
        };

        private EnvironmentSelectionService service;
        private IServiceCollection services;
        private IConfiguration configuration;
        private ILogger logger;
        private Mock<IAccountable> mockAccountable;

        [SetUp]
        public void Setup()
        {
            this.configuration = new Mock<IConfiguration>().Object;
            this.services = new ServiceCollection();
            this.logger = NullLogger.Instance;
            this.mockAccountable = new Mock<IAccountable>();
            this.services.AddSingleton<IEnumerable<IAccountable>>(new List<IAccountable>() { this.mockAccountable.Object });
            this.service = new EnvironmentSelectionService(this.services, this.configuration, this.logger);
        }

        [Test]
        public void IntersectionReturnsExpectedValue()
        {
            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            {
                { "node1", new EnvironmentCandidate(null, "cluster", "region3", null, null, "node1", null, null) },
                { "node2", new EnvironmentCandidate(null, "cluster", "region1", null, null, "node2", null, null) }
            };
            IDictionary<string, EnvironmentCandidate> actualResult = EnvironmentSelectionServiceTests.SetOne.Intersection(EnvironmentSelectionServiceTests.SetTwo);
            Assert.IsNotNull(actualResult);
            Assert.IsTrue(expectedResult.All(e => actualResult.Contains(e)));
        }

        [Test]
        public void IntersectionValidatesParameters()
        {
            IDictionary<string, EnvironmentCandidate> nullDictionary = null;
            Assert.Throws<ArgumentException>(() => nullDictionary.Intersection(EnvironmentSelectionServiceTests.SetOne));
            Assert.Throws<ArgumentException>(() => EnvironmentSelectionServiceTests.SetOne.Intersection(null));
        }

        [Test]
        public void GetNodesAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.service.GetNodesAsync(null, CancellationToken.None));
        }

        [Test]
        public void EnvironmentSelectionServiceConstructorValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => new EnvironmentSelectionService(null, this.configuration, this.logger));
            Assert.Throws<ArgumentException>(() => new EnvironmentSelectionService(this.services, null, this.logger));
            Assert.Throws<ArgumentException>(() => new EnvironmentSelectionService(this.services, this.configuration, null));
        }

        [Test]
        public void GetNodesAsyncReturnsTheExpectedResult()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(type: typeof(NodeSelectionFiltersOne).FullName),
                new EnvironmentFilter(type: typeof(NodeSelectionFiltersTwo).FullName)
            };

            EnvironmentQuery query = new EnvironmentQuery("query", 6, filters);

            IEnumerable<EnvironmentCandidate> expectedResult = new List<EnvironmentCandidate>()
            {
                new EnvironmentCandidate(null, "cluster", "region3", null, null, "node1", null, null),
                new EnvironmentCandidate(null, "cluster", "region1", null, null, "node2", null, null)
            };

            IEnumerable<EnvironmentCandidate> actualResult = this.service.GetNodesAsync(query, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.IsTrue(expectedResult.All(e => actualResult.Contains(e)));
        }

        [Test]
        public void GetSubscriptionAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.service.GetSubscriptionAsync(null, CancellationToken.None));
        }

        [Test]
        public void GetSubscriptionAsyncReturnsExpectedResult()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(type: typeof(SubscriptionFiltersOne).FullName),
                new EnvironmentFilter(type: typeof(SubscriptionFiltersTwo).FullName)
            };

            EnvironmentQuery query = new EnvironmentQuery("query", 6, filters);

            EnvironmentCandidate expectedResult = new EnvironmentCandidate("subscription1", "cluster", null, "machinesDontSwim", null, "node1", null, "cpuid10");
            EnvironmentCandidate actualResult = this.service.GetSubscriptionAsync(query, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void GetSubscriptionReturnsNullWhenNoSubscriptionsAreFound()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(typeof(SubscriptionFiltersOne).FullName),
                new EnvironmentFilter(typeof(SubscriptionFiltersThree).FullName)
            };

            EnvironmentQuery query = new EnvironmentQuery("query", 6, filters);

            EnvironmentCandidate actualResult = this.service.GetSubscriptionAsync(query, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNull(actualResult);
        }

        [Test]
        public void GetEnvironmentCandidateAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.service.GetEnvironmentCandidatesAsync(null, CancellationToken.None));
        }

        [Test]
        public void GetEnvironmentCandidateAsyncReturnsExpectedResultWhenEnvrionmentIsPresent()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            { 
                new EnvironmentFilter(typeof(NodeSelectionFiltersOne).FullName),
                new EnvironmentFilter(typeof(NodeSelectionFiltersTwo).FullName),
                new EnvironmentFilter(typeof(SubscriptionFiltersOne).FullName)
            };
            EnvironmentQuery query = new EnvironmentQuery("henlo", 6, filters);

            IEnumerable<EnvironmentCandidate> expectedResult = new List<EnvironmentCandidate>()
            {
                new EnvironmentCandidate("subscription1", "cluster", "region3", null, null, "node1", null, null),
                new EnvironmentCandidate("subscription1", "cluster", "region1", null, null, "node2", null, null)
            };
            IEnumerable<EnvironmentCandidate> actualResult = this.service.GetEnvironmentCandidatesAsync(query, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.IsTrue(expectedResult.All(e => actualResult.Contains(e)));
        }

        [Test]
        public void GetEnvironmentCandidateAsyncReturnsExpectedResultWhenNodeCountIsOne()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(typeof(NodeSelectionFiltersOne).FullName),
                new EnvironmentFilter(typeof(NodeSelectionFiltersTwo).FullName),
                new EnvironmentFilter(typeof(SubscriptionFiltersOne).FullName)
            };
            EnvironmentQuery query = new EnvironmentQuery("query", 1, filters, NodeAffinity.SameRack);

            IEnumerable<EnvironmentCandidate> expectedResult = new List<EnvironmentCandidate>()
            {
                new EnvironmentCandidate("subscription1", "cluster", "region3", null, null, "node1", null, null),
                new EnvironmentCandidate("subscription1", "cluster", "region1", null, null, "node2", null, null)
            };
            IEnumerable<EnvironmentCandidate> actualResult = this.service.GetEnvironmentCandidatesAsync(query, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.IsTrue(expectedResult.Any(e => actualResult.Contains(e)));
        }

        [Test]
        public void GetEnvironmentCandidateHandlesNoSubscriptionBeingFoundProperly()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(typeof(SubscriptionFiltersOne).FullName),
                new EnvironmentFilter(typeof(SubscriptionFiltersTwo).FullName),
                new EnvironmentFilter(typeof(SubscriptionFiltersThree).FullName)
            };
            EnvironmentQuery query = new EnvironmentQuery("henlo", 6, filters);

            Assert.ThrowsAsync<EnvironmentSelectionException>(() => this.service.GetEnvironmentCandidatesAsync(query, CancellationToken.None));
        }

        [Test]
        public void GetEnvrionmentCandidateHandlesNoNodesBeingFoundProperly()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(typeof(SubscriptionFiltersOne).FullName),
                new EnvironmentFilter(typeof(SubscriptionFiltersTwo).FullName),
                new EnvironmentFilter(typeof(NodeSelectionFiltersFour).FullName),
                new EnvironmentFilter(typeof(NodeSelectionFiltersOne).FullName)
            };

            EnvironmentQuery query = new EnvironmentQuery("henlo", 6, filters);
            Assert.ThrowsAsync<EnvironmentSelectionException>(() => this.service.GetEnvironmentCandidatesAsync(query, CancellationToken.None));
        }

        [Test]
        public void GetEnvironmentCandidateReturnsExpectedNodesWhenParityIsDifferentCluster()
        {
            IList<EnvironmentFilter> filters = new List<EnvironmentFilter>()
            {
                new EnvironmentFilter(typeof(SubscriptionFiltersOne).FullName)
            };

            EnvironmentQuery query = new EnvironmentQuery("query", 2, filters, NodeAffinity.DifferentCluster);
            IEnumerable<EnvironmentCandidate> candidates = this.service.GetEnvironmentCandidatesAsync(query, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(candidates);
            Assert.IsNotEmpty(candidates);
            Assert.IsTrue(candidates.Select(c => c.Region).Distinct().Count() == 1);
        }

        [Test]
        public void IntersectionUsesCorrectKeyDuringJoin()
        {
            IDictionary<string, EnvironmentCandidate> result = EnvironmentSelectionServiceTests.SubscriptionSetOne.Intersection(EnvironmentSelectionServiceTests.SubscriptionSetTwo);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 1);
            Assert.AreEqual("subscription1", result.First().Key);
        }

        [Test]
        public void ReserveNodesAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.service.ReserveEnvironmentCandidatesAsync(null, TimeSpan.Zero, CancellationToken.None));
        }

        [Test]
        public async Task ReserveNodesAsyncReturnsEmptyIfCouldNotReserveNode()
        {
            this.mockAccountable.Setup(c => c.ReserveCandidateAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            var reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate(node: "fake_nodeId", cluster: "fake_cluster") });

            IEnumerable<EnvironmentCandidate> result = await this.service.ReserveEnvironmentCandidatesAsync(reservedNodes.Nodes, TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [Test]
        public async Task ReserveNodesAsyncReturnsPartialListIfANodeIdWasNotFoundInCache()
        {
            this.mockAccountable.SetupSequence(c => c.ReserveCandidateAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false))
                .Returns(Task.FromResult(true));
            var reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>() 
            { 
                new EnvironmentCandidate(node: "fake_nodeId1", cluster: "fake_cluster"), 
                new EnvironmentCandidate(node: "fake_nodeId2", cluster: "fake_cluster") 
            });

            IEnumerable<EnvironmentCandidate> result = await this.service.ReserveEnvironmentCandidatesAsync(reservedNodes.Nodes, TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false);

            this.mockAccountable.Verify(c => c.ReserveCandidateAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
        }

        [Test]
        public void ReserveEnvironmentCandidatesAsyncReservesCorrectNode()
        {
            string nodeId = Guid.NewGuid().ToString();
            IEnumerable<EnvironmentCandidate> reservedNodes = new List<EnvironmentCandidate> { new EnvironmentCandidate(node: nodeId) };
            this.mockAccountable.Setup(a => a.ReserveCandidateAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Callback<EnvironmentCandidate, TimeSpan, CancellationToken>((candidate, duration, token) =>
                {
                    Assert.AreEqual(nodeId, candidate.NodeId);
                }).Returns(Task.FromResult(true));

            this.service.ReserveEnvironmentCandidatesAsync(reservedNodes, TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();

            this.mockAccountable.Verify(a => a.ReserveCandidateAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Test]
        public void DeleteEnvironmentCandidatesAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.service.DeleteReservationsAsync(null, CancellationToken.None));
        }

        [Test]
        public void DeleteEnvironmentCandidatesAsyncPostsTheCorrectEnvironmentCandidates()
        {
            string nodeId = Guid.NewGuid().ToString();
            string clusterId = Guid.NewGuid().ToString();
            this.mockAccountable.Setup(c => c.DeleteReservationAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<CancellationToken>()))
                .Callback<EnvironmentCandidate, CancellationToken>((candidate, token) => 
                {
                    Assert.AreEqual(nodeId, candidate.NodeId);
                    Assert.AreEqual(clusterId, candidate.ClusterId);
                })
                .Returns(Task.FromResult(true));
            var reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate(node: nodeId, cluster: clusterId) });

            IEnumerable<EnvironmentCandidate> deletedNodes = this.service.DeleteReservationsAsync(reservedNodes.Nodes, CancellationToken.None).GetAwaiter().GetResult();

            this.mockAccountable.Verify(c => c.DeleteReservationAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<CancellationToken>()), Times.Once());
            Assert.IsNotNull(deletedNodes);
            Assert.AreEqual(reservedNodes.Nodes.Count(), deletedNodes.Count());
        }

        [Test]
        public void DeleteEnvironmentCandidatesAsyncReturnsPartialListIfSomeDeletionsFailed()
        {
            this.mockAccountable.SetupSequence(c => c.DeleteReservationAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false))
                .Returns(Task.FromResult(true));
            var reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>()
            {
                new EnvironmentCandidate(node: "fake_nodeId1", cluster: "fake_cluster"),
                new EnvironmentCandidate(node: "fake_nodeId2", cluster: "fake_cluster")
            });

            IEnumerable<EnvironmentCandidate> deletedNodes = this.service.DeleteReservationsAsync(reservedNodes.Nodes, CancellationToken.None).GetAwaiter().GetResult();

            this.mockAccountable.Verify(c => c.DeleteReservationAsync(It.IsAny<EnvironmentCandidate>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.IsNotNull(deletedNodes);
            Assert.AreEqual(1, deletedNodes.Count());
        }

        private class NodeSelectionFiltersOne : EnvironmentSelectionProvider
        {
            public NodeSelectionFiltersOne(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SetOne);
            }
        }

        private class NodeSelectionFiltersTwo : EnvironmentSelectionProvider
        {
            public NodeSelectionFiltersTwo(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SetTwo);
            }
        }

        private class NodeSelectionFiltersThree : EnvironmentSelectionProvider
        {
            public NodeSelectionFiltersThree(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SetThree);
            }
        }

        private class NodeSelectionFiltersFour : EnvironmentSelectionProvider
        {
            public NodeSelectionFiltersFour(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SetFour);
            }
        }

        private class NodeSelectionFiltersFive : EnvironmentSelectionProvider
        {
            public NodeSelectionFiltersFive(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SetFive);
            }
        }

        private class SubscriptionFiltersOne : EnvironmentSelectionProvider
        {
            public SubscriptionFiltersOne(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SubscriptionSetOne);
            }
        }

        private class SubscriptionFiltersTwo : EnvironmentSelectionProvider
        {
            public SubscriptionFiltersTwo(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SubscriptionSetTwo);
            }
        }

        private class SubscriptionFiltersThree : EnvironmentSelectionProvider
        {
            public SubscriptionFiltersThree(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.SubscriptionSetThree);
            }
        }

        private class ClusterSelectionFiltersOne : EnvironmentSelectionProvider
        {
            public ClusterSelectionFiltersOne(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return Task.FromResult(EnvironmentSelectionServiceTests.ClusterSet);
            }
        }
    }
}