namespace ConcurrencyPitfalls
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class ConcurrencyWithAtomicLazyConcurrentDictionary
    {
        // Since we are not the first to discover all that issues
        // Some have already suggest some solutions
        // And the AtomicLazy is a good one
        // https://github.com/dotnet/corefx/issues/32337#issuecomment-498962755
        public class AtomicLazy<T>
        {
            private readonly Func<T> _factory;

            private T _value;

            private bool _initialized;

            private object _lock;

            public AtomicLazy(Func<T> factory)
            {
                _factory = factory;
            }

            public AtomicLazy(T value)
            {
                _value = value;
                _initialized = true;
            }

            public T Value => LazyInitializer.EnsureInitialized(ref _value, ref _initialized, ref _lock, _factory);
        }

        [Test]
        public void ConcurrentGetOrAdd_AtomicLazyPattternThatWorks()
        {
            // No need any more to remove Lazy instances from the dictionary
            // Since the AtomicLazy don't cache the exceptions
            var concurrentDictionary = new ConcurrentDictionary<long, AtomicLazy<string>>();
            var lazyCount = 0;
            var exceptionCount = 0;

            Parallel.For(
                0,
                50,
                i =>
                {
                    var added = false;
                    var lazyName = concurrentDictionary.GetOrAdd(
                        6,
                        n =>
                        new AtomicLazy<string>(() =>
                        {
                            added = true;
                            lazyCount++;
                            Thread.Sleep(10);
                            if (lazyCount == 1)
                            {
                                throw new Exception("Issue while accessing a resource (network, file system, ...)");
                            }
                            return "Six" + "-from-" + i;
                        }));

                    string name;
                    try
                    {
                        name = lazyName.Value;
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref exceptionCount);
                        return;
                    }

                    Assert.That(name, Does.StartWith("Six"));
                    if (added)
                    {
                        Assert.That(name, Does.EndWith("-from-" + i));
                    }
                });

            Assert.That(lazyCount, Is.EqualTo(2));
            Assert.That(exceptionCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentGetOrAdd_AtomicLazyPattternThatWorks_Simplified()
        {
            // Without all the counters and boolean to validate the behaviour
            // The code is acceptable
            var concurrentDictionary = new ConcurrentDictionary<long, AtomicLazy<string>>();

            string Factory(long id)
            {
                switch (id)
                {
                    case 1:
                        return "One";
                    case 6:
                        return "Six";
                    default:
                        throw new Exception("Not found");
                }
            };

            Parallel.For(
                0,
                50,
                i =>
                {
                    var lazyName = concurrentDictionary.GetOrAdd(
                        6,
                        n => new AtomicLazy<string>(() => Factory(n)));

                    string name = lazyName.Value;
                    Assert.That(name, Does.StartWith("Six"));
                });
        }
    }
}
