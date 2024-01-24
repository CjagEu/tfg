using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;

namespace ma_longs
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class ma_longs : Strategy
    {
        Order buyOrder, sellOrder;
        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public ma_longs(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "MA Longs Strategy"; }
        }

        /// <summary>
        /// Security filter that ensures the OpenPosition will be closed at the end of the trading session.
        /// </summary>
        /// <returns>
        /// True if the opened position must be closed automatically on session's close, false otherwise
        /// </returns>
        public override bool ForceCloseIntradayPosition
        {
            get { return false; }
        }

        /// <summary>
        /// Security filter that sets a maximum open position level, and ensures that the strategy will never exceeds it
        /// </summary>
        /// <returns>
        /// The maximum opened lots allowed (any side)
        /// </returns>
        public override uint MaxOpenPosition
        {
            get { return 1; }
        }

        /// <summary>
        /// Flag that indicates if the strategy uses advanced Order management or standard
        /// </summary>
        /// <returns>
        /// True if strategy uses advanced Order management. This means that the strategy uses the advanced methods (InsertOrder/CancelOrder/ModifyOrder) in opposite of the simple ones (Buy/Sell/ExitLong/ExitShort).
        /// </returns>
        public override bool UsesAdvancedOrderManagement
        {
            get { return true; }
        }

        /// <summary>
        /// Creates the set of exposed Parameters for the strategy
        /// </summary>
        /// <returns>The exposed Parameters collection</returns>
        public override InputParameterList SetInputParameters()
        {
            return new InputParameterList
            {
                new InputParameter("Long Moving Average Period", 50),
                new InputParameter("Slow Moving Average Period", 20),
                new InputParameter("Fast Moving Average Period", 5),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("MA Longs onInitialize()");

            var indLongSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Long Moving Average Period"));
            var indSlowSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Slow Moving Average Period"));
            var indFastSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Fast Moving Average Period"));

            AddIndicator("Long SMA", indLongSMA);
            AddIndicator("Slow SMA", indSlowSMA);
            AddIndicator("Fast SMA", indFastSMA);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indFastSma = (SMAIndicator)GetIndicator("Fast SMA");
            var indSlowSma = (SMAIndicator)GetIndicator("Slow SMA");
            var indLongSma = (SMAIndicator)GetIndicator("Long SMA");

            //if (GetOpenPosition() == 0)
            //{
            //    if (indFastSma.GetAvSimple()[1] < indSlowSma.GetAvSimple()[1] && indFastSma.GetAvSimple()[0] >= indSlowSma.GetAvSimple()[0])
            //    {
            //        buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
            //        this.InsertOrder(buyOrder);
            //    }
            //}
            //else if(GetOpenPosition() != 0)
            //{
            //    if (indFastSma.GetAvSimple()[1] > indSlowSma.GetAvSimple()[1] && indFastSma.GetAvSimple()[0] <= indSlowSma.GetAvSimple()[0])
            //    {
            //        sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend ended, close long");
            //        this.InsertOrder(sellOrder);
            //    }
            //}

            if (GetOpenPosition() == 0)
            {
                if (indLongSma.GetAvSimple()[0] < indSlowSma.GetAvSimple()[0] && indSlowSma.GetAvSimple()[0] < indFastSma.GetAvSimple()[0])
                {

                }
            }
        }
    }
}
