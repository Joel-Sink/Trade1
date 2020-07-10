using OkonkwoOandaV20.TradeLibrary.REST;
using System;
using OkonkwoOandaV20.TradeLibrary.REST.OrderRequest;
using System.Collections.Generic;
using System.Threading;
using static OkonkwoOandaV20.TradeLibrary.REST.Rest20;
using Money_Management;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;

namespace Trade
{
    class Program
    {
        static LArray betArray = null;
        static Dictionary<string, double> atr;
        static void Main(string[] args)
        {
            List<string> lastpair = new List<string>();
            SetApiCredentials();

            #region CloseAllTrades
            if (GetOpenTradesAsync(AccountID).Result.Count != 0)
            {
                foreach (var i in GetOpenTradesAsync(AccountID).Result)
                {
                    try
                    {
                        Console.WriteLine(i.id);
                        CloseOrder(i.id).Wait();
                    }
                    catch
                    {
                    }
                }
            }
            #endregion


            #region CreateArray
            string[] fileresults;

            betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);

            try
            {
                fileresults = File.ReadAllLines(@"C:\Users\jsink\source\repos\Trade\Trade\betarray.txt");
                if (fileresults.Length > 0)
                {
                    betArray.Clear();

                    foreach (var i in fileresults)
                        betArray.Add(double.Parse(i));
                }
            }
            catch
            {
                
            }
            #endregion

            string pair;
            double bet;
            double units;
            long trade = 0;
            Stopwatch watch; 

            while (0 < 1)
            {
                try
                {
                    #region Numbers
                    while (0 < 1)
                    {
                        GetHourlyATR();

                        pair = atr.Where(i => lastpair.Where((j) => j.Contains(i.Key)).Count() is 0).Aggregate((l, r) => 
                        {
                            if (l.Value < 0 && r.Value > 0)
                            {
                                return -l.Value > r.Value ? l : r;
                            }
                            else if (l.Value > 0 && r.Value < 0)
                            {
                                return l.Value > -r.Value ? l : r;
                            }
                            else if (l.Value < 0 && r.Value < 0)
                            {
                                return -l.Value > -r.Value ? l : r;
                            }
                            else
                            {
                                return l.Value > r.Value ? l : r;
                            }
                        }).Key;

                        bet = betArray.CalcBet();
                        try
                        {
                            units = GetLotSize(pair, bet);
                        }
                        catch
                        {
                            Console.WriteLine("Checking For Smaller Spread");
                            lastpair.Add(pair+"spread");
                            continue;
                        }

                        double margin = CalculateMargin((long)units, pair);

                        if (margin > (double)GetAccountAsync(AccountID).Result.balance * .75)
                        {
                            betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);
                            continue;
                        }
                        break;
                    }

                    lastpair.Add(pair+"traded");
                    lastpair = lastpair.Where((i) => i.Contains("traded")).ToList();
                    if (lastpair.Count > 4)
                    {
                        lastpair = lastpair.Where((i) => i.Contains(pair)).ToList();
                    }

                    #endregion

                    #region OpenTrade
                    while (0 < 1)
                    {
                        try
                        {
                            trade = (long)PlaceOrder((long)units, pair).Result;
                            break;
                        }
                        catch
                        {
                            Console.WriteLine("Error!");
                            Thread.Sleep(60000);
                        }
                    }
                    Console.WriteLine("units: " + units + " bet:" + betArray.CalcBet() + " risk:" + (GetSpread(pair) * 3.25) + " pair:" + pair);
                    watch = Stopwatch.StartNew();
                    #endregion

                    #region CloseTrade
                    while (0 < 1)
                    {
                        if ((double)GetTradeAsync(AccountID, trade).Result.unrealizedPL > betArray.CalcBet())
                        {
                            try
                            {
                                betArray.WinTrade();
                            }
                            catch
                            {
                                betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);
                            }
                        }
                        else if ((double)GetTradeAsync(AccountID, trade).Result.unrealizedPL < -betArray.CalcBet())
                        {
                            betArray.LoseTrade();
                        }
                        else
                        {
                            continue;
                        }
                        try
                        {
                            CloseOrder(trade).Wait();
                        }
                        catch
                        {
                            Console.WriteLine("Error!");
                            Thread.Sleep(60000);
                        }
                        watch.Stop();
                        Console.WriteLine((double)GetTradeAsync(AccountID, trade).Result.realizedPL + " " + pair + " " + (watch.ElapsedMilliseconds / 60000));
                        break;
                    }
                    File.WriteAllLines(@"C:\Users\jsink\source\repos\Trade\Trade\betarray.txt", betArray.Select((i) => i.ToString()));
                }
                catch
                {
                    Console.WriteLine("Error!");
                    Thread.Sleep(60000);
                }
                #endregion
            }
        }
        public static string AccountID { get; set; }

        #region Trade
        static async Task<long?> PlaceOrder(long units, string pair)
        {
            var instrument = (await GetAccountInstrumentsAsync(AccountID, 
                new AccountInstrumentsParameters { instruments = new List<string>() { pair } })).First();

            var request = new MarketOrderRequest(instrument)
            {
                units = units
            };

            PostOrderResponse response;
            
            response = await PostOrderAsync(AccountID, request);
            return response?.orderFillTransaction?.tradeOpened?.tradeID;
        }   

        static async Task<long?> CloseOrder(long trade)
        {
            var request = await PutTradeCloseAsync(AccountID, trade);

            return request.lastTransactionID;

        }
        #endregion

        #region PreTradeCalcs
        public static double GetLotSize(string pair, double bet)
        {
            var risk = GetSpread(pair);

            if ((risk > .001 && !pair.Contains("JPY")) || (risk > .1 && pair.Contains("JPY")))
            {
                throw new Exception();
            }
            if (atr[pair] > 0)
            {
                risk *= 2;
            }
            else
            {
                risk *= -2;
            }

            var basec = pair.Split('_')[0];

            if (basec.Equals("USD"))
            {
                var param = new PricingParameters() { instruments = new List<string>() { pair } };
                return bet * (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price / risk;
            }
            else
            {
                var upair = GetAccountInstrumentsAsync(AccountID).Result.Where(i => i.name.Contains("USD") && i.name.Contains(basec)).ToList()[0];
                var param = new PricingParameters() { instruments = new List<string>() { pair } };
                var param1 = new PricingParameters() { instruments = new List<string>() { upair.name } };

                var price = (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price;
                var price1 = (double)GetPricingAsync(AccountID, param1).Result[0].asks[0].price;

                var counter = upair.name.Split('_')[1];
                if (counter.Equals("USD"))
                {
                    return 1 / price1 * price * bet / risk;  
                }
                else
                {
                    return price1 * price * bet / risk;
                }

            }


        }

        public static void GetHourlyATR()
        {
            atr = new Dictionary<string, double>();
            foreach (var i in GetAccountInstrumentsAsync(AccountID).Result.Where(i => !i.name.Contains("HKD") 
            && !i.name.Contains("MXN") && !i.name.Contains("PLN") && !i.name.Contains("DKK") && !i.name.Contains("HUF") && !i.name.Contains("SEK") 
            && !i.name.Contains("CZK") && !i.name.Contains("TRY") && !i.name.Contains("NRK")))
            {
                var param = new InstrumentCandlesParameters() { price = "M", granularity = "H1", smooth = false, count = 2 };
                var candles = GetInstrumentCandlesAsync(i.name, param).Result.ToArray();
                var pairatr = (double)(candles[candles.Length - 2].mid.c - candles[0].mid.c);
                if (i.name.Contains("JPY"))
                {
                    pairatr /= 100;
                }

                atr.Add(i.name, pairatr);
            }
        }

        public static double GetSpread(string pair)
        {
            var param = new PricingParameters() { instruments = new List<string>() { pair } };
            var price = GetPricingAsync(AccountID, param).Result[0];
            return (double)(price.asks[0].price - price.bids[0].price);
        }

        public static double CalculateMargin(long units, string pair)
        {
            var marginreq = (double)GetAccountInstrumentsAsync(AccountID).Result.Where(i => i.name.Equals(pair)).ToList()[0].marginRate;

            var basec = pair.Split('_')[0];

            if (!basec.Equals("USD"))
            {
                var upair = GetAccountInstrumentsAsync(AccountID).Result.Where(i => i.name.Contains("USD") && i.name.Contains(basec)).ToList()[0];

                var counter = upair.name.Split('_')[1];

                if (counter.Equals("USD"))
                {
                    var param = new PricingParameters() { instruments = new List<string>() { upair.name } };
                    return (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price * units * marginreq;
                }
                else
                {
                    var param = new PricingParameters() { instruments = new List<string>() { upair.name } };
                    return 1 / (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price * units * marginreq;
                }
            }
            else
            {
                return units * marginreq;
            }

        }

        #endregion

        #region Transactions Stream
        static void SetApiCredentials()
        {
            Console.WriteLine("Setting your V20 credentials ...");

            AccountID = "101-001-14918521-001";
            var environment = EEnvironment.Practice;
            var token = "5814e7cef9981369aefcb5d1354ea38e-fdc8d9d6082a5abbb02a3eedca940252";

            Credentials.SetCredentials(environment, token, AccountID);

            Console.WriteLine("Nice! Credentials are set.");
        }

        
        #endregion

    }
}
