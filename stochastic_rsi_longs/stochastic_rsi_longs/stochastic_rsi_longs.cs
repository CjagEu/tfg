using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;

namespace stochastic_rsi_longs
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class stochastic_rsi_longs : Strategy
    {
        Order buyOrder;
        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public stochastic_rsi_longs(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Stochastic RSI Longs Strategy"; }
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
                new InputParameter("timePeriod", 14),
                new InputParameter("fastKPeriod", 4),
                new InputParameter("fastDPeriod", 4)
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("StochsticRSILongs onInitialize()");

            var indStochasticRSI = new StochasticRSIIndicator(source: Bars.Bars, timePeriod: 14, fastKPeriod: 5, fastDPeriod: 5, fastDMAType: TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma);

            AddIndicator("StochasticRSI", indStochasticRSI);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indStochasticRSI = (StochasticRSIIndicator)GetIndicator("StochasticRSI");


            if (GetOpenPosition() == 0)
            {
                if (indStochasticRSI.GetD()[1] < indStochasticRSI.GetLowerLine()[0] && indStochasticRSI.GetD()[0] >= indStochasticRSI.GetLowerLine()[0])
                {
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                    this.InsertOrder(buyOrder);
                }
            }
            else if (GetOpenPosition() != 0)
            {
                if (indStochasticRSI.GetD()[1] > indStochasticRSI.GetUpperLine()[0] && indStochasticRSI.GetD()[0] <= indStochasticRSI.GetUpperLine()[0])
                {
                    buyOrder = new MarketOrder(OrderSide.Sell, 1, "Trend ended, close long");
                    this.InsertOrder(buyOrder);
                }
            }
        }
    }
}
