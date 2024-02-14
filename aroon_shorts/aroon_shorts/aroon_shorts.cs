using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;
using System;

namespace aroon_shorts
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class aroon_shorts : Strategy
    {
        Order buyOrder, sellOrder, StopOrder;
        bool canOpenPosition = false;
        bool canClosePosition = false;
        double stoplossInicial;
        bool breakevenFlag;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public aroon_shorts(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Aroon Shorts Strategy"; }
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
                new InputParameter("Aroon Period", 25),

                new InputParameter("Filter Moving Average Period", 99),

                new InputParameter("Wait Window", 3),

                new InputParameter("UpperLine", 80),
                new InputParameter("LowerLine", 20),

                new InputParameter("Stoploss Ticks", 2.0D),
                new InputParameter("Breakeven Ticks", 2.0D),

                //new InputParameter("Wait Window", 5),

                //new InputParameter("Porcentaje SL", -2D),
                //new InputParameter("Porcentaje TP", 5D),

                //new InputParameter("Ticks", 50),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("Aroon Shorts onInitialize()");

            var indAroon = new AroonIndicator(Bars.Bars, (int)GetInputParameter("Aroon Period"));
            var indFilterSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Filter Moving Average Period"));

            AddIndicator("Filter SMA", indFilterSMA);
            AddIndicator("Aroon", indAroon);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indAroon = (AroonIndicator)GetIndicator("Aroon");
            var indFilterSma = (SMAIndicator)GetIndicator("Filter SMA");

            /* Condiciones de entrada:
             *      Línea Down > 80 durante N días.
             *      Línea Up < 30.   
             *      
             * Condiciones de salida:
             *      Línea Up > 80 durante N días.
             *      Línea Down < 30.
             */
            if (GetOpenPosition() == 0)
            {
                /* Si durante N días la línea Aroon Down se ha mantenido por encima de 80, abrir posición. */
                int counter = 0;
                for (int i = (int)GetInputParameter("Wait Window"); i >= 1; i--)
                {
                    if (indAroon.GetAroonDown()[i] >= (int)GetInputParameter("UpperLine"))
                    {
                        counter++;
                    }
                }
                if (counter == (int)GetInputParameter("Wait Window"))
                {
                    canOpenPosition = true;
                }
                //if (canOpenPosition)
                //{
                //    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                //    this.InsertOrder(buyOrder);
                //    canOpenPosition = false;
                //}
                if (canOpenPosition && indAroon.GetAroonDown()[0] >= (int)GetInputParameter("UpperLine") && indAroon.GetAroonUp()[0] <= (int)GetInputParameter("LowerLine") && indFilterSma.GetAvSimple()[0] > Bars.Close[0])
                {
                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend confirmed, open short");
                    stoplossInicial = Bars.Close[0] + (Bars.Close[0] * ((double)GetInputParameter("Stoploss Ticks") / 100));                        //* GetMainChart().Symbol.TickSize; // TODO
                    StopOrder = new StopOrder(OrderSide.Buy, 1, stoplossInicial, "StopLoss triggered");

                    this.InsertOrder(sellOrder);
                    this.InsertOrder(StopOrder);

                    breakevenFlag = false;
                    canOpenPosition = false;

                    //stopLoss = Math.Truncate(GetFilledOrders()[0].FillPrice - (GetFilledOrders()[0].FillPrice * ((int)GetInputParameter("Ticks") / 10000D)));
                    //stopLossOrder = new StopOrder(OrderSide.Sell, 1, stopLoss, "Saltó StopLoss inicial");
                    //this.InsertOrder(stopLossOrder);
                }
            }
            else if (GetOpenPosition() != 0)
            {
                /* Si durante N días la línea Aroon Up se ha mantenido por encima de 80, cerrar posición. */
                //for (int i = 3; i >= 1; i--)
                //{
                //    if (indAroon.GetAroonUp()[i] >= 80)
                //    {
                //        canClosePosition = true;
                //    }
                //}
                //if (canClosePosition && indAroon.GetAroonUp()[0] >= 80 && indAroon.GetAroonDown()[0] <= 30)
                //{
                //    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Uptrend finished confirmed, close short");
                //    this.InsertOrder(buyOrder);
                //    canClosePosition = false;
                //}

                //Precio sube X%, stoplossinicial a BE
                if (porcentajeMovimientoPrecio(sellOrder.FillPrice) < ((double)GetInputParameter("Breakeven Ticks") * -1) && !breakevenFlag)
                {
                    StopOrder.Price = sellOrder.FillPrice - (GetMainChart().Symbol.TickSize * 100);
                    StopOrder.Label = "Breakeven triggered ******************";
                    this.ModifyOrder(StopOrder);
                    breakevenFlag = true;
                }
                /* Condición directa para salir de shorts ya que el mercado cae rápido siempre.*/
                else if (indAroon.GetAroonUp()[0] == 0)
                {
                    this.CancelOrder(StopOrder);
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Uptrend finished confirmed, close short");
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
