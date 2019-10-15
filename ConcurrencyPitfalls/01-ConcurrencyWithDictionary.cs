namespace ConcurrencyPitfalls
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class ConcurrencyWithDictionary
    {
        [Test]
        public void ConcurrentReads()
        {
            // simple dictionaries are thread safe if they are only read in multiple threads
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            Parallel.For(
                0,
                50,
                i => Assert.That(dictionary.Select(p => p.Value), Is.EquivalentTo(new[] { "One", "Two", "Three", "Four", "Five" })));
        }

        [Test]
        public void ConcurrentReadsAndWrites()
        {
            // But clearly they aren't thread safe when read and write operations are performed in different threads
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            Parallel.For(
                0,
                50,
                i =>
                {
                    if (!dictionary.ContainsKey(6))
                    {
                        dictionary.Add(6, "Six");
                    }
                    Assert.That(dictionary.Select(p => p.Value), Is.EquivalentTo(new[] { "One", "Two", "Three", "Four", "Five", "Six" }));
                    dictionary.Remove(6);
                });
        }

        [Test]
        public void ConcurrentReadsAndWrites_WithWriteLocks()
        {
            // And the issue is not just about writing in concurrence
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            Parallel.For(
                0,
                50,
                i =>
                {
                    lock (dictionary)
                    {
                        if (!dictionary.ContainsKey(6))
                        {
                            dictionary.Add(6, "Six");
                        }
                    }
                    Assert.That(dictionary.Select(p => p.Value), Is.EquivalentTo(new[] { "One", "Two", "Three", "Four", "Five", "Six" }));
                    lock (dictionary)
                    {
                        dictionary.Remove(6);
                    }
                });
        }

        [Test]
        public void ConcurrentReadsAndWrites_WithReadAndWriteLocks()
        {
            // If write operations are performed in concurrence, then read operations needs to be locked too
            // Which may be not so performant...
            var dictionary = new Dictionary<long, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Four" },
                { 5, "Five" },
            };

            Parallel.For(
                0,
                50,
                i =>
                {
                    lock (dictionary)
                    {
                        if (!dictionary.ContainsKey(6))
                        {
                            dictionary.Add(6, "Six");
                        }
                        Assert.That(dictionary.Select(p => p.Value), Is.EquivalentTo(new[] { "One", "Two", "Three", "Four", "Five", "Six" }));
                    }
                    lock (dictionary)
                    {
                        dictionary.Remove(6);
                    }
                });
        }
    }
}