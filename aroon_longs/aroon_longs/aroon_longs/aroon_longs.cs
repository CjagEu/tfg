using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;
using System;

namespace aroon_longs
{
    /// <summary> 
    /// Aroon Longs Strategy
    /// </summary> 
    /// <remarks> 
    public class aroon_longs : Strategy
    {
        Order buyOrder, sellOrder, stopLossOrder;
        bool canOpenPosition = false;
        bool canClosePosition = false;
        double stopLoss = 0D;
        double siguienteNivelStop = 0D;

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
                new InputParameter("Aroon Period", 25),

                new InputParameter("Wait Window", 3),

                new InputParameter("UpperLine", 80),
                new InputParameter("LowerLine", 20)

                //new InputParameter("Porcentaje SL", -2D),
                //new InputParameter("Porcentaje TP", 5D),

                //new InputParameter("", 50),Ticks
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

            /* Condiciones de entrada:
             *      Línea Up > 80 durante N días.
             *      Línea Down < 30.
             *      
             * Condiciones de salida:
             *      Línea Down > 80 durante N días.
             *      Línea Up < 30.
             */
            if (GetOpenPosition() == 0)
            {
                /* Si durante N días la línea Aroon Up se ha mantenido por encima de 80, abrir posición. */
                int counterOpen = 0;
                for (int i = (int)GetInputParameter("Wait Window"); i >= 1; i--)
                {
                    if (indAroon.GetAroonUp()[i] >= (int)GetInputParameter("UpperLine"))
                    {
                        counterOpen++;
                    }
                }
                if (counterOpen == (int)GetInputParameter("Wait Window"))
                {
                    canOpenPosition = true;
                }
                if (canOpenPosition && indAroon.GetAroonUp()[0] >= (int)GetInputParameter("UpperLine") && indAroon.GetAroonDown()[0] <= (int)GetInputParameter("LowerLine"))
                {
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                    this.InsertOrder(buyOrder);
                    canOpenPosition = false;
                }
            }
            else if (GetOpenPosition() != 0)
            {
                /* Si durante N días la línea Aroon Down se ha mantenido por encima de 80, cerrar posición. */
                int counterClose = 0;
                for (int i = (int)GetInputParameter("Wait Window"); i >= 1; i--)
                {
                    if (indAroon.GetAroonDown()[i] >= (int)GetInputParameter("UpperLine"))
                    {
                        counterClose++;
                    }
                }
                if (counterClose == (int)GetInputParameter("Wait Window"))
                {
                    canClosePosition = true;
                }
                if (canClosePosition && indAroon.GetAroonDown()[0] >= (int)GetInputParameter("UpperLine") && indAroon.GetAroonUp()[0] <= (int)GetInputParameter("LowerLine"))
                {
                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Uptrend finished confirmed, close long");
                    this.InsertOrder(sellOrder);
                    canClosePosition = false;
                }
            }
        }


        // Devuelve en porcentaje cuánto se ha movido el precio desde la entrada.
        protected double porcentajeMovimientoPrecio()
        {
            double porcentaje = 0;

            // Calcular la variación porcentual del precio con respecto a la entrada.
            if (Bars.Close[0] > buyOrder.FillPrice)
            {
                // Precio actual por encima del precio de entrada.
                porcentaje = ((Bars.Close[0] / buyOrder.FillPrice) - 1) * 100;
            }
            else if (Bars.Close[0] < buyOrder.FillPrice)
            {
                // Precio actual por debajo del precio de entrada.
                porcentaje = ((Bars.Close[0] / buyOrder.FillPrice) - 1) * 100;
            }

            return porcentaje;
        }

        // Implementación de un trailing stop para la estrategia
        protected void ajustarStopLoss(double siguienteNivelStop)
        {
            /* Cálculo del siguiente nivel propuesto para StopLoss */
            siguienteNivelStop = stopLossOrder.Price + (stopLossOrder.Price * (int)GetInputParameter("Ticks") / 10000D);
            /* Si el precio avanza más de X "Ticks", muevo SL [Por ejemplo Ticks=50 -> 0.50% de subida] */
            if ((this.Bars.Close[0] / siguienteNivelStop) - 1 >= (int)GetInputParameter("Ticks") / 10000D)
            {
                stopLossOrder.Price = Math.Truncate(siguienteNivelStop);
                stopLossOrder.Label = "Saltó StopLoss desplazado";
                this.ModifyOrder(stopLossOrder);
            }
        }
    }
}
