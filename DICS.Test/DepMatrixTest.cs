using global::NUnit.Framework;
using global::System.Collections.Generic;

namespace DICS.Test
{
    
    public class DepMatrixTest
    {

        [Test]
        public void Test_DepMatrix_Transpose()
        {
            var links = new Dictionary<Key, ISet<Key>>
            {
                { Key.Of<int>("A") , new HashSet<Key> { Key.Of<int>("B"), Key.Of<int>("C") } },
                { Key.Of<int>("B"), new HashSet<Key> { Key.Of<int>("C") } },
                { Key.Of<int>("C"), new HashSet<Key> { Key.Of<int>("A"), Key.Of<int>("D") } }
            };
            
            var m = new DepMatrix<int>(links, new Dictionary<Key, int>());

            var transposed = m.Transpose();

            var restored = transposed.Transpose();
            Assert.That(restored.Links.Keys, Is.EquivalentTo(m.Links.Keys));
            foreach (var key in restored.Links.Keys)
            {
                Assert.That(restored.Links[key], Is.EquivalentTo(m.Links[key]));
            }
        }
    }
}