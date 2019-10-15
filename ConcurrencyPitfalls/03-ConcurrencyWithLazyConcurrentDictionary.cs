namespace ConcurrencyPitfalls
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class ConcurrencyWithLazyConcurrentDictionary
    {
        [Test]
        public void ConcurrentGetOrAdd_SlowLazy()
        {
            // Using the Lazy<T> type into a ConcurrentDictionary seems a great solution
            // that we can found on many blogs
            var concurrentDictionary = new ConcurrentDictionary<long, Lazy<string>>();

            Parallel.For(
                0,
                50,
                i =>
                {
                    var added = false;
                    var lazyName = concurrentDictionary.GetOrAdd(
                        6,
                        n =>
                        new Lazy<string>(() =>
                        {
                            added = true;
                            Thread.Sleep(10);
                            return "Six" + "-from-" + i;
                        }));
                    var name = lazyName.Value;
                    Assert.That(name, Does.StartWith("Six"));
                    if (added)
                    {
                        Assert.That(name, Does.EndWith("-from-" + i));
                    }
                });
        }

        [Test]
        public void ConcurrentGetOrAdd_LazyWithException()
        {
            // But pay attention when converting non lazy dictionaries
            var concurrentDictionary = new ConcurrentDictionary<long, string>();

            Assert.That(
                () => concurrentDictionary.GetOrAdd(
                    6,
                    n => throw new Exception("Issue while accessing a resource (network, file system, ...)")),
                Throws.TypeOf<Exception>(),
                "A First call that raise an exception");

            var name = concurrentDictionary.GetOrAdd(
                6,
                n => "Six");
            Assert.That(
                name,
                Is.EqualTo("Six"),
                "Second call do a new factory execution");

            // Since exception rasied by the wrapped factory won't produce the same behaviour
            var concurrentDictionaryLazy = new ConcurrentDictionary<long, Lazy<string>>();

            var lazyName = concurrentDictionaryLazy.GetOrAdd(
                6,
                n =>
                new Lazy<string>(() =>
                {
                    throw new Exception("Issue while accessing a resource (network, file system, ...)");
                }));

            Assert.That(
                () => lazyName.Value,
                Throws.TypeOf<Exception>(),
                "A First call that raise an exception");

            var lazyName2 = concurrentDictionaryLazy.GetOrAdd(
                6,
                n =>
                new Lazy<string>(() => "Six"));
            Assert.That(
                lazyName2.Value,
                Is.EqualTo("Six"),
                "Second call do a new factory execution ?");
        }

        [Test]
        public void ConcurrentGetOrAdd_LazyWithExceptionOnFirstCallOnly()
        {
            // Also pay attention to the fact that Lazy<T> cache exceptions
            var concurrentDictionary = new ConcurrentDictionary<long, Lazy<string>>();
            var count = 0;

            var lazyName = concurrentDictionary.GetOrAdd(
                6,
                n =>
                new Lazy<string>(() =>
                {
                    count++;
                    if (count == 1)
                    {
                        throw new Exception("Issue while accessing a resource (network, file system, ...)");
                    }
                    return "Six";
                }));

            var lazyName2 = concurrentDictionary.GetOrAdd(
                6,
                n =>
                new Lazy<string>(() => "We know it won't be called"));

            Assert.That(() => lazyName.Value, Throws.TypeOf<Exception>());
            Assert.That(lazyName2.Value, Is.EqualTo("Six"));
        }

        [Test]
        public void ConcurrentGetOrAdd_LazyWithExceptionOnFirstCallOnly_PublicationOnly()
        {
            // PublicationOnly mode can seem to be a solution since it doesn't cache expcetions
            var concurrentDictionary = new ConcurrentDictionary<long, Lazy<string>>();
            var count = 0;

            var lazyName = concurrentDictionary.GetOrAdd(
                6,
                n =>
                new Lazy<string>(() =>
                {
                    count++;
                    if (count == 1)
                    {
                        throw new Exception("Issue while accessing a resource (network, file system, ...)");
                    }
                    return "Six";
                }, LazyThreadSafetyMode.PublicationOnly));

            var lazyName2 = concurrentDictionary.GetOrAdd(
                6,
                n =>
                new Lazy<string>(() => "We know it won't be called"));

            Assert.That(() => lazyName.Value, Throws.TypeOf<Exception>());
            Assert.That(lazyName2.Value, Is.EqualTo("Six"));
        }

        [Test]
        public void ConcurrentGetOrAdd_SlowLazyWithExceptionOnFirstCallOnly_PublicationOnly()
        {
            // But PublicationOnly had the same issue than ConcurrentDictionary
            // Factory executions are not thread safe
            // So It doesn't provide any additional value over the ConcurrentDictionary
            var concurrentDictionary = new ConcurrentDictionary<long, Lazy<string>>();
            var lazyCount = 0;

            Parallel.For(
                0,
                50,
                i =>
                {
                    var added = false;
                    var lazyName = concurrentDictionary.GetOrAdd(
                        6,
                        n =>
                        new Lazy<string>(() =>
                        {
                            added = true;
                            Interlocked.Increment(ref lazyCount);
                            Thread.Sleep(10);
                            return "Six" + "-from-" + i;
                        },
                        LazyThreadSafetyMode.PublicationOnly));
                    var name = lazyName.Value;
                    Assert.That(name, Does.StartWith("Six"));
                    if (added)
                    {
                        Assert.That(name, Does.EndWith("-from-" + i));
                    }
                });

            Assert.That(lazyCount, Is.EqualTo(1));
        }

        [Test]
        public void Lazy_None_Is_Not_Thread_Safe()
        { 
            // By the way, the Nobne mode is really not thread safe at all !!!
            // Even a basic null coalesce operator (??) is more thread safe than that !
            var lazyName = new Lazy<string>(() => "Six", LazyThreadSafetyMode.None);

            Parallel.For(
                0,
                50,
                i => Assert.That(lazyName.Value, Is.EqualTo("Six")));
        }

        [Test]
        public void ConcurrentGetOrAdd_LazyPattternThatWorks()
        {
            // So here is an example that demonstrate how we should use
            // ConcurrentDictionary and Lazy, with the introduction of a TryRemove
            // on the dictionary so it do what we want :
            //  * having a factory that is called in a thread safe way
            //  * not called in parallel
            //  * and which is retried by subsequent access to the dictionary if it has failed.
            var concurrentDictionary = new ConcurrentDictionary<long, Lazy<string>>();
            var lazyCount = 0;

            Parallel.For(
                0,
                50,
                i =>
                {
                    var added = false;
                    var lazyName = concurrentDictionary.GetOrAdd(
                        6,
                        n =>
                        new Lazy<string>(() =>
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
                    } catch(Exception)
                    {
                        if (added)
                        {
                            concurrentDictionary.TryRemove(6, out _);
                        }
                        return;
                    }
                    
                    Assert.That(name, Does.StartWith("Six"));
                    if (added)
                    {
                        Assert.That(name, Does.EndWith("-from-" + i));
                    }
                });

            Assert.That(lazyCount, Is.EqualTo(2));
        }
    }
}
