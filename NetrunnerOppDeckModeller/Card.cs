using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class Card
    {
        public enum FactionEnum 
        { 
            Neutral = 0, 
            Anarch = 1, 
            Criminal = 2, 
            Shaper = 3, 
            HaasBioroid = 4, 
            Jinteki = 5,
            NBN = 6,
            Weyland = 7
        }

        public enum CardTypeEnum
        {
            INVALID = -1,
            Identity = 0,
            Asset = 1,
            Event,
            Hardware,
            Agenda,
            ICE,
            Operation,
            Program,
            Resource,
            Upgrade
        }

        //Formatters
        public string TypeString
        {
            get
            {
                return CardType.ToString();
            }
        }

        public string FactionString
        {
            get
            {
                return Faction.ToString();
            }
        }

        public string SideString
        {
            get
            {
                if (IsCorp)
                {
                    return "Corp";
                }
                else
                {
                    return "Runner";
                }
            }
        }

        public int OccuranceCount { get; set; }

        public int DeckInclusionCount { get; set; }

        public float Multiplicity
        {
            get
            {
                return ((float)OccuranceCount) / ((float)DeckInclusionCount);
            }
        }

        public float PercentDeckInclusion { get { return ((float)DeckInclusionCount / (float)Decklist.DECKLISTLIST.Count()) * 100; } }

        public static Dictionary<int, Card> CARDLIST = new Dictionary<int, Card>();
        private static bool CARD_DATA_LOADED = false;

        public int ID { get; set; }

        public string Name { get; set; }

        public CardTypeEnum CardType { get; set; }

        public FactionEnum Faction { get; set; }

        public int AgendaPoints { get; set; }

        public int MaxNumPerDeck { get; set; }

        public static int LoadedCardCount { get { return Card.CARDLIST.Count(); } }

        public static void LoadCardData(string datapath)
        {
            if (!Card.CARD_DATA_LOADED)
            {
                if (!System.IO.File.Exists(datapath))
                {
                    throw new ApplicationException("Invalid path - " + datapath);
                }

                Microsoft.VisualBasic.FileIO.TextFieldParser reader = new Microsoft.VisualBasic.FileIO.TextFieldParser(datapath);
                reader.HasFieldsEnclosedInQuotes = true;
                reader.SetDelimiters(",");

                string currentline = reader.ReadLine();

                string[] fields;

                while (!reader.EndOfData)
                {
                    fields = reader.ReadFields();

                    int id = Int32.Parse(fields[0]);

                    Card.CARDLIST.Add(id, new Card() 
                    { 
                        ID = id, 
                        Name = fields[1], 
                        CardType = (Card.CardTypeEnum)Enum.Parse(typeof(Card.CardTypeEnum), fields[2]), 
                        Faction = (Card.FactionEnum)Enum.Parse(typeof(Card.FactionEnum), fields[3]), 
                        Influence = Int32.Parse(fields[4]), 
                        AgendaPoints = Int32.Parse(fields[5])
                    });

                    Card.CARDLIST[id].MaxNumPerDeck = (Card.CARDLIST[id].CardType == CardTypeEnum.Identity ? 1 : 3);
                }

                reader.Close();

                //Unfortunately some amount of hardcoded stuff, as there are certain cards which can only be 1/deck, and that information isn't provided by the website :'(
                Card.CARDLIST[7006].MaxNumPerDeck = 1;
                Card.CARDLIST[3004].MaxNumPerDeck = 1;
                Card.CARDLIST[5006].MaxNumPerDeck = 1;
                Card.CARDLIST[6020].MaxNumPerDeck = 1;
                Card.CARDLIST[6030].MaxNumPerDeck = 1;
                Card.CARDLIST[6059].MaxNumPerDeck = 1;
                Card.CARDLIST[6071].MaxNumPerDeck = 1;
                Card.CARDLIST[6100].MaxNumPerDeck = 1;
                Card.CARDLIST[6110].MaxNumPerDeck = 1;

#if DEBUG
                //Test the number of cards limited is correct (should be 9)
                List<Card> limitedCards = Card.CARDLIST.Values.Where(x => (x.MaxNumPerDeck == 1) && (x.CardType != CardTypeEnum.Identity)).ToList();
                System.Diagnostics.Debug.Assert(limitedCards.Count() == 9);
                //If more cards are added, this might need to be updated
#endif


                Card.CARD_DATA_LOADED = true;
            }
        }

        public static Card GetCard(int cardId)
        {
            if(CARDLIST.Any(x => x.Key == cardId))
            {
                return CARDLIST[cardId];
            }
            else
            {
                throw new ApplicationException("Unknown Card - " + cardId);
            }
        }

        public bool IsCorp 
        {
            get
            {
                switch(CardType)
                {
                    case CardTypeEnum.Identity:
                        {
                            return !((Faction == FactionEnum.Anarch) || (Faction == FactionEnum.Criminal) || (Faction == FactionEnum.Shaper));
                        }
                    case CardTypeEnum.Asset:
                    case CardTypeEnum.Agenda:
                    case CardTypeEnum.ICE:
                    case CardTypeEnum.Operation:
                    case CardTypeEnum.Upgrade:
                        return true;
                    case CardTypeEnum.Event:
                    case CardTypeEnum.Hardware:
                    case CardTypeEnum.Program:
                    case CardTypeEnum.Resource:
                        return false;
                    default:
                        throw new ApplicationException("Invalid cardtype - " + CardType);
                }
            }
        }

        public int Influence { get; set; }

        public bool CostsInfluence(FactionEnum identityFaction)
        {
            if(Influence <= 0)
            {
                return false;
            }
            else
            {
                return (identityFaction != Faction);
            }
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, ID);
        }
    }
}
