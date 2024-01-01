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
        Order buyOrder, stopLossOrder;
        bool canOpenPosition = false;
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
                new InputParameter("Aroon Period", 16),

                new InputParameter("Wait Window", 5),
                new InputParameter("Exit Operation Level", 75),

                new InputParameter("Porcentaje SL", -2D),
                new InputParameter("Porcentaje TP", 5D),

                new InputParameter("Ticks", 50),
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
                //for (int i = (int)GetInputParameter("Wait Window"); i >= 1; i--)
                //{
                //    if (indAroon.GetAroonUp()[i] >= 90)
                //    {
                //        canOpenPosition = true;
                //    }
                //}
                //if (canOpenPosition)
                //{
                //    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                //    this.InsertOrder(buyOrder);
                //    canOpenPosition = false;
                //}
                if (indAroon.GetAroonUp()[0] >= 90 && indAroon.GetAroonDown()[0] <= 30)
                {
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                    this.InsertOrder(buyOrder);
                    canOpenPosition = false;

                    stopLoss = Math.Truncate(GetFilledOrders()[0].FillPrice - (GetFilledOrders()[0].FillPrice * ((int)GetInputParameter("Ticks") / 10000D)));
                    stopLossOrder = new StopOrder(OrderSide.Sell, 1, stopLoss, "Saltó StopLoss inicial");
                    this.InsertOrder(stopLossOrder);
                }
            }
            else if (GetOpenPosition() != 0)
            {      
                //if (porcentajeMovimientoPrecio() <= (double)GetInputParameter("Porcentaje SL"))
                //{
                //    Order sellOrder = new MarketOrder(OrderSide.Sell, 1, "StopLoss, close long");
                //    this.InsertOrder(sellOrder);
                //    log.Info("Stoploss: " + porcentajeMovimientoPrecio().ToString("F2"));
                //    this.CancelOrder(stopLossOrder);
                //}
                /* Si la línea Aroon Up desciende a menos de 75, cerrar long. */
                //if (indAroon.GetAroonUp()[0] <= (int)GetInputParameter("Exit Operation Level"))
                //{
                //    Order sellOrder = new MarketOrder(OrderSide.Sell, 1, "Aroon Up < 75, close long");
                //    this.InsertOrder(sellOrder);
                //    this.CancelOrder(stopLossOrder);
                //}
                if (indAroon.GetAroonUp()[0] - indAroon.GetAroonDown()[0] >= (int)GetInputParameter("Exit Operation Level"))
                {
                    Order sellOrder = new MarketOrder(OrderSide.Sell, 1, "Aroon Up < 75, close long");
                    this.InsertOrder(sellOrder);
                    this.CancelOrder(stopLossOrder);
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
