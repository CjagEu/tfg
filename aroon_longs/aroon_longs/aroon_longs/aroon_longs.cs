using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;

namespace aroon_longs
{
    /// <summary> 
    /// Aroon Longs Strategy
    /// </summary> 
    /// <remarks> 
    public class aroon_longs : Strategy
    {
        Order buyOrder;
        int counter = 0;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public aroon_longs(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Aroon Longs Strategy"; }
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
                new InputParameter("Aroon Period", 16),
                new InputParameter("Wait Window", 5),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("Aroon Longs onInitialize()");

            var indAroon = new AroonIndicator(Bars.Bars, (int)GetInputParameter("Aroon Period"));

            AddIndicator("Aroon", indAroon);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indAroon = (AroonIndicator)GetIndicator("Aroon");

            /* Estrategia:
             *      La estrategia se basa en que la línea Aroon Up indica la existencia de tendencia alcista.
             *      Si la linea Aroon Up está por encima de 90 varios días, se confirma la tendencia y se abre
             *      operación.
             */
            if (GetOpenPosition() == 0)
            {
                /* Si durante N días la línea Aroon Up se ha mantenido por encima de 90, abrir posición. */
                if (indAroon.GetAroonUp()[0] >= 90)
                {
                    counter++;
                    if (counter == (int)GetInputParameter("Wait Window"))
                    {
                        buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                        this.InsertOrder(buyOrder);
                    }
                }
            }
            else if (GetOpenPosition() != 0)
            {
                /* Si la línea Aroon Up desciende a menos de 75, cerrar long. */
                if (indAroon.GetAroonUp()[0] <= 75)
                {
                    Order sellOrder = new MarketOrder(OrderSide.Sell, 1, "Aroon Up < 75, close long");
                    this.InsertOrder(sellOrder);
                    counter = 0;
                }
            }
        }
    }
}
