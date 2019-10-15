namespace ConcurrencyPitfalls
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class ConcurrencyWithConcurrentDictionary
    {
        [Test]
        public void ConcurrentReadsAndWrites()
        {
            // ConcurrentDictionary is thread safe (that means it cwon't raise exceptions if read and write at the same times)
            // however that doesn't prevent you to think about what can happens between atomic operations exposed by the ConcurrentDictionary
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            var concurrentDictionary = new ConcurrentDictionary<long, string>(dictionary);

            Parallel.For(
                0,
                50,
                i =>
                {
                    if (!concurrentDictionary.ContainsKey(6))
                    {
                        concurrentDictionary.TryAdd(6, "Six");
                    }
                    Assert.That(concurrentDictionary.Select(p => p.Value), Is.EquivalentTo(new[] { "One", "Two", "Three", "Four", "Five", "Six" }));
                    concurrentDictionary.TryRemove(6, out _);
                });
        }

        [Test]
        public void ConcurrentGetOrAdd()
        {
            // So you need to write code that take advantage of the atomic operations provided by the ConcurrentDictionary
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            var concurrentDictionary = new ConcurrentDictionary<long, string>(dictionary);

            Parallel.For(
                0,
                50,
                i =>
                {
                    var name = concurrentDictionary.GetOrAdd(6, n => "Six");
                    Assert.That(name, Is.EqualTo("Six"));
                    concurrentDictionary.TryRemove(6, out _);
                });
        }

        [Test]
        public void ConcurrentGetOrAdd_Slow()
        {
            // But pay attentions to the flows of the GetOrAdd method of the ConcurrentDictionary
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            var concurrentDictionary = new ConcurrentDictionary<long, string>(dictionary);

            Parallel.For(
                0,
                50,
                i =>
                {
                    var added = false;
                    var name = concurrentDictionary.GetOrAdd(
                        6,
                        n =>
                        {
                            added = true;
                            Thread.Sleep(10);
                            return "Six" + "-from-" + i;
                        });
                    Assert.That(name, Does.StartWith("Six"));
                    if (added)
                    {
                        Assert.That(name, Does.EndWith("-from-" + i));
                    }
                });
        }
    }
}
