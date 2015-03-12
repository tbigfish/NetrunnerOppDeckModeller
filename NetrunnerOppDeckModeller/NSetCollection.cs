using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class NSetCollection
    {
        private List<Set> _data = new List<Set>();

        private int _n = 2;

        public NSetCollection(int n)
        {
            _n = n;

            if((n > 3) || (n<2))
            {
                throw new ApplicationException("Invalid n-Set size (to be implemented)");
            }
        }

        public int Count()
        {
            return _data.Count();
        }

        public void AddStatistic(Card identity, Card a, Card b, bool isOffInfluence, short bCount)
        {
            if (_n == 2)
            {
                _data.Add(new Set(identity, a, b, bCount));
            }
            else if (_n == 3)
            {
                _data.Add(new Set(identity, a, b, identity, bCount));
            }
            else
            {
                throw new ApplicationException("Set with n != (2 || 3)");
            }
        }

        public List<Set> GetAllMatches(Card value, bool? influenceFilter)
        {
            return _data.Where(x => x.Contains(value)).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="identity"></param>
        /// <param name="influenceFilter">True = return only cards that used influence, False = return only cards that didn't use influence, null = return all</param>
        /// <returns></returns>
        public List<Set> GetAllMatches(Card value, Card identity, bool? influenceFilter)
        {
            if((identity == null) || (_n == 2))
            {
                //We can't filter by Identity here, so don't bother
                return GetAllMatches(value, influenceFilter);
            }
            else
            {
                if (_n != 3)
                {
                    throw new ApplicationException("Attempt to GetAllMatches(Card,Card) on a 2-Set");
                }

                if (!influenceFilter.HasValue)
                {
                    return _data.Where(x => (x.GetValue(2) == identity) && x.Contains(value)).ToList();
                }
                else
                {
                    return _data.Where(x => (x.GetValue(2) == identity) && (x.Contains(value, influenceFilter.Value))).ToList();
                }
            }
        }

        public int GetCount(Card identity)
        {
            return this._data.Count(x => x.Contains(identity));
        }

        public BindingSortableList<NSetSum> GetData()
        {
            BindingSortableList<NSetSum> retVal = new BindingSortableList<NSetSum>();
            
            //TODO - This implementation needs fixing!
            //foreach (var group in this._data.GroupBy(x => x.GetHashCode()))
            //{
            //    retVal.Add(new NSetSum(group.First().GetValue(0), group.First().GetValue(1), group.Count()));
            //}

            return retVal;
        }

        public void Clear()
        {
            _data = new List<Set>();
        }
    }
}
