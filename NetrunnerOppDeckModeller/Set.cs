using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class Set
    {
        internal Card[] _data;
        internal int[] _counts;
        internal bool[] _isOffInfluence;

        /// <summary>
        /// Used for standard 2-Sets
        /// </summary>
        /// <param name="deckIdentity">Identity of the owning deck, used only for Off-influence tests</param>
        /// <param name="a">Card A of the 2-set</param>
        /// <param name="b">Card B of the 2-set</param>
        /// <param name="countB">The number of identical copies of B observed as part of this 2-set</param>
        public Set(Card deckIdentity, Card a, Card b, int countB)
            : this(deckIdentity, new List<Card>() { a, b }, new List<int>() { 1, countB })
        { }

        /// <summary>
        /// Used for standard 3-Sets, where the third element is the deck identity
        /// </summary>
        /// <param name="deckIdentity">Identity of the owning deck, used only for Off-influence tests</param>
        /// <param name="a">Card A of the 3-set</param>
        /// <param name="b">Card B of the 3-set</param>
        /// <param name="c">Identity of the owning deck</param>
        /// <param name="countB">The number of identical copies of B observed as part of this 2-set</param>
        public Set(Card deckIdentity, Card a, Card b, Card c, int countB)
            : this(deckIdentity, new List<Card>() { a, b, c }, new List<int>() { 1, countB, 1 })
        { 
            if(deckIdentity != c)
            {
                throw new ApplicationException("Standard 3-sets must have DeckIdentity == C");
            }
        }

        public Set(Card deckIdentity, List<Card> items, List<int> counts)
        {
            _data = new Card[items.Count()];
            _counts = new int[items.Count()];
            _isOffInfluence = new bool[items.Count()];

            int index = 0;

            foreach (Card card in items)
            {
                _data[index] = card;
                _counts[index] = counts[index]; //This could cause an exception if the lists are different lengths, but that's desirable (as there's clearly a bug elsewhere)
                _isOffInfluence[index] = card.CostsInfluence(deckIdentity.Faction);
                index++;
            }
        }

        public int GetN()
        {
            return _data.Count();
        }

        public bool Contains(int value)
        {
            return _data.Any(x => x.ID == value);
        }

        public bool Contains(Card value)
        {
            return Contains(value.ID);
        }

        public bool Contains(Card value, bool requiresOffInfluence)
        {
            for(int i = 0; i < _data.Length; i++)
            {
                if((_data[i] == value) && (_isOffInfluence[i] == requiresOffInfluence))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsOffInfuence(int index)
        {
            return this._isOffInfluence[index]; //Will cause an exception if handed an invalid index
        }

        public int GetCount(int index)
        {
            return this._counts[index]; //Will cause an exception if handed an invalid index
        }

        public Card GetValue(int index)
        {
            return this._data[index]; //Will cause an exception if handed an invalid index
        }

        public List<Card> GetOtherSets(int firstSet)
        {
            return this._data.Where(x => x.ID != firstSet).ToList(); //Will cause an exception if handed an invalid ID
        }
    }
}
