using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class Card
    {
        /// <summary>
        /// An enum representing the Faction of a card
        /// </summary>
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

        /// <summary>
        /// An enum representing the type of a card
        /// </summary>
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

        /// <summary>
        /// Returns a bool representing whether Card Data has been loaded
        /// </summary>
        public static bool Loaded
        {
            get
            {
                return CARD_DATA_LOADED;
            }
        }

        /// <summary>
        /// Returns a ``pretty'' string representing this card's Type
        /// </summary>
        public string TypeString
        {
            get
            {
                return CardType.ToString();
            }
        }

        /// <summary>
        /// Returns a ``pretty'' string representing this card's Faction
        /// </summary>
        public string FactionString
        {
            get
            {
                return Faction.ToString();
            }
        }

        /// <summary>
        /// Returns a ``pretty'' string representing this card's Side
        /// </summary>
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

        /// <summary>
        /// The number of times this card has been seen in all decks
        /// </summary>
        public int OccuranceCount { get; set; }

        /// <summary>
        /// The number of decks which include this card
        /// </summary>
        public int DeckInclusionCount { get; set; }

        /// <summary>
        /// The average number of this card in all decks which have at least one of this card
        /// </summary>
        public float Multiplicity
        {
            get
            {
                return ((float)OccuranceCount) / ((float)DeckInclusionCount);
            }
        }

        /// <summary>
        /// The percentage of decks which include this card
        /// </summary>
        public float PercentDeckInclusion { get { return ((float)DeckInclusionCount / (float)Decklist.DECKLISTLIST.Count()) * 100; } }

        public static Dictionary<int, Card> CARDLIST = new Dictionary<int, Card>();
        private static bool CARD_DATA_LOADED = false;

        /// <summary>
        /// The ID of this card
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// The name of this card
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of this card
        /// </summary>
        public CardTypeEnum CardType { get; set; }

        /// <summary>
        /// The faction of this card
        /// </summary>
        public FactionEnum Faction { get; set; }

        /// <summary>
        /// The number of Agenda points on this card
        /// </summary>
        public int AgendaPoints { get; set; }

        /// <summary>
        /// The maximum number of this card that can be included in a deck
        /// </summary>
        public int MaxNumPerDeck { get; set; }

        /// <summary>
        /// The number of Cards currently loaded
        /// </summary>
        public static int LoadedCardCount { get { return Card.CARDLIST.Count(); } }

        /// <summary>
        /// Method to load Card data
        /// </summary>
        /// <param name="datapath"></param>
        public static void LoadCardData(string datapath)
        {
            if (!Card.CARD_DATA_LOADED)
            {
                if (!System.IO.File.Exists(datapath))
                {
                    return;
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

        /// <summary>
        /// Retrieves a Card from the store by ID
        /// </summary>
        /// <param name="cardId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Determines if this card is a Corp card or not
        /// </summary>
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

        /// <summary>
        /// The amount of influence this card costs
        /// </summary>
        public int Influence { get; set; }

        /// <summary>
        /// Determines if this card costs influence for the provided faction
        /// </summary>
        /// <param name="identityFaction">The faction to use to check influence cost</param>
        /// <returns>True if this card costs influence for identityFaction</returns>
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

        /// <summary>
        /// An overried to ensure that Cards look pretty when I .ToString() them
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, ID);
        }
    }
}
