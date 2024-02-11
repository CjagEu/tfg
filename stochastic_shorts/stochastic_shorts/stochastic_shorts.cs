using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;
using System;

namespace stochastic_shorts
{
    /// <summary> 
    /// TradingMotion SDK Stochastic Shorts Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class stochastic_shorts : Strategy
    {
        Order buyOrder, sellOrder, StopOrder;
        double stoplossInicial;
        bool breakevenFlag;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public stochastic_shorts(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Stochastic Shorts Strategy"; }
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
                new InputParameter("K Line", 14),
                new InputParameter("D Line", 3),
                new InputParameter("Stochastic Upper Line", 80),
                new InputParameter("Stochastic Lower Line", 20),

                new InputParameter("Filter Moving Average Period", 99),

                new InputParameter("Stoploss Ticks", 2.0D),
                new InputParameter("Breakeven Ticks", 2.0D),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("StochasticShortsStrategy onInitialize()");

            var indStochastic = new StochasticIndicator(
                Bars.Bars,
                (int)GetInputParameter("K Line"),
                (int)GetInputParameter("D Line"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma,
                (int)GetInputParameter("D Line"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma
            );

            var indFilterSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Filter Moving Average Period"));

            AddIndicator("Filter SMA", indFilterSMA);
            AddIndicator("Stochastic", indStochastic);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indStochastic = (StochasticIndicator)GetIndicator("Stochastic");
            var indFilterSma = (SMAIndicator)GetIndicator("Filter SMA");

            if (GetOpenPosition() == 0)
            {

                if (indStochastic.GetD()[1] > (int)GetInputParameter("Stochastic Upper Line") && indStochastic.GetD()[0] <= (int)GetInputParameter("Stochastic Upper Line") && indFilterSma.GetAvSimple()[0] > Bars.Close[0])
                {
                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend confirmed, open short");
                    stoplossInicial = Bars.Close[0] + (Bars.Close[0] * ((double)GetInputParameter("Stoploss Ticks") / 100));         //* GetMainChart().Symbol.TickSize;
                    StopOrder = new StopOrder(OrderSide.Buy, 1, stoplossInicial, "StopLoss triggered");

                    this.InsertOrder(sellOrder);
                    this.InsertOrder(StopOrder);

                    breakevenFlag = false;
                }
            }
            else if (GetOpenPosition() != 0)
            {
                //Precio sube X%, stoplossinicial a BE
                if (porcentajeMovimientoPrecio(sellOrder.FillPrice) > ((double)GetInputParameter("Breakeven Ticks") * -1) && !breakevenFlag)
                {
                    StopOrder.Price = sellOrder.FillPrice - (GetMainChart().Symbol.TickSize * 100);
                    StopOrder.Label = "Breakeven triggered ******************";
                    this.ModifyOrder(StopOrder);
                    breakevenFlag = true;
                }
                else if (indStochastic.GetD()[1] > (int)GetInputParameter("Stochastic Lower Line") && indStochastic.GetD()[0] <= (int)GetInputParameter("Stochastic Lower Line"))
                {
                    this.CancelOrder(StopOrder);
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Estocástico entró en rango de nuevo, close short");
                    this.InsertOrder(buyOrder);
                }
            }
        }


        // Devuelve en porcentaje cuánto se ha movido el precio desde la entrada.
        protected double porcentajeMovimientoPrecio(double precioOrigen)
        {
            double porcentaje = 0;

            // Calcular la variación porcentual del precio con respecto a la entrada.
            if (Bars.Close[0] > precioOrigen)
            {
                // Precio actual por encima del precio de entrada.
                porcentaje = ((Bars.Close[0] / precioOrigen) - 1) * 100;
            }
            else if (Bars.Close[0] < precioOrigen)
            {
                // Precio actual por debajo del precio de entrada.
                porcentaje = ((Bars.Close[0] / precioOrigen) - 1) * 100;
            }

            return porcentaje;
        }


        // Implementación de un trailing stop para la estrategia
        protected void ajustarStopLoss(double siguienteNivelStop)
        {
            /* Cálculo del siguiente nivel propuesto para StopLoss */
            siguienteNivelStop = StopOrder.Price + (StopOrder.Price * (int)GetInputParameter("Stoploss Ticks") / 100D);
            /* Si el precio avanza más de X "Ticks", muevo SL [Por ejemplo Ticks=50 -> 0.50% de subida] */
            if ((this.Bars.Close[0] / siguienteNivelStop) - 1 >= (int)GetInputParameter("Stoploss Ticks") / 100D)
            {
                StopOrder.Price = Math.Truncate(siguienteNivelStop);
                StopOrder.Label = "Saltó StopLoss desplazado";
                this.ModifyOrder(StopOrder);
            }
        }
    }
}
