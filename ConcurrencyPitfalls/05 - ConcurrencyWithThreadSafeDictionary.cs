namespace ConcurrencyPitfalls
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Pfz.Collections;

    [TestFixture]
    public class ConcurrencyWithThreadSafeDictionary
    {
        [Test]
        public void ConcurrentGetOrAdd_ThreadSafeDictionary()
        {
            // Some have even go further and have totally rewrite an alternative to ConcurrentDictionary
            // That takes all into account, to have something performant hen reading (no locks)
            // And without the flows of the ConcurrentDictionary
            // The code is really complex (see all what have been put in the Pfz folder),
            // but as you can see in that example the ThreadSafeDictionary seems to effectively do what we need,
            // and without having to wrap our factory into a Lazy wrapper
            // sources:
            // https://www.codeproject.com/Articles/548406/Dictionary-plus-Locking-versus-ConcurrentDictionar
            var threadSafeDictionary = new ThreadSafeDictionary<long, string>();

            var factoryCount = 0;
            var exceptionCount = 0;

            Parallel.For(
                0,
                50,
                i =>
                {
                    var added = false;

                    string name;
                    try
                    {
                        name = threadSafeDictionary.GetOrCreateValue(
                            6,
                            n =>
                            {
                                added = true;
                                Interlocked.Increment(ref factoryCount);
                                Thread.Sleep(10);
                                if (factoryCount == 1)
                                {
                                    throw new Exception("Issue while accessing a resource (network, file system, ...)");
                                }
                                return "Six" + "-from-" + i;
                            });
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
                    else
                    {
                        Assert.That(name, Does.Not.EndWith("-from-" + i));
                    }
                });

            Assert.That(factoryCount, Is.EqualTo(2));
            Assert.That(exceptionCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentGetOrAdd_ThreadSafeDictionary_Simplified()
        {
            // Without all the counters and boolean to validate the behaviour
            // The code is really simple
            var threadSafeDictionary = new ThreadSafeDictionary<long, string>();

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
                    var name = threadSafeDictionary.GetOrCreateValue(
                        6,
                        Factory);

                    Assert.That(name, Does.StartWith("Six"));
                });
        }
    }
}
