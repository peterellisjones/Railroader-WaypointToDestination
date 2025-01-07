using HarmonyLib;
using UI.Builder;
using UI.CarInspector;
using JetBrains.Annotations;
using Model.Ops;
using System;
using Model;
using Track;
using Game.Messages;
using System.Diagnostics.CodeAnalysis;
using WaypointToDestination;
using UI.Common;
using UnityEngine;
using Game;
using Network.Messages;
using Model.AI;
using UI.EngineControls;
using Track.Search;


[PublicAPI]
[HarmonyPatch]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class CarInspectorPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "PopulateWaybillPanel", new Type[] { typeof(UIPanelBuilder), typeof(Waybill) })]
    static void PopulateWaybillPanel(UIPanelBuilder builder, Waybill waybill, CarInspector __instance, Car? ____car)
    {
        if (!WaypointToDestinationPlugin.Shared.IsEnabled)
        {
            return;
        }

        builder.AddButton("Set Waypoint to destination", delegate
        {
            WaypointToDestination(____car, waybill.Destination, true);
        });
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "PopulatePanel")]
    public static void PopulatePanel(UIPanelBuilder builder, Car? ____car, Window ____window)
    {
        if (!WaypointToDestinationPlugin.Shared.IsEnabled)
        {
            return;
        }

        var size = ____window.GetContentSize();

        ____window.SetContentSize(new Vector2(size.x - 2, 322 + 70));
    }
    private static void WaypointToDestination(Car car, OpsCarPosition destination, bool routeToOtherTrackIfNoSwitchesMoved)
    {
        // iterate through all connected cars to calculate the total length of the consists
        // as well as finding the locomotive that should have the waypoint order
        // (any without the brake set to cut-out)
        float totalLength = 0.0f;
        var carCount = 0;
        BaseLocomotive? loco = null;

        car.EnumerateCoupled().Do((c) =>
        {
            totalLength += c.carLength;
            carCount += 1;

            if (c.IsLocomotive)
            {
                if (!((BaseLocomotive)c).locomotiveControl.air.IsCutOut)
                {
                    loco = (BaseLocomotive)c;
                }
            }
        });

        totalLength += 1.04f * (float)(carCount - 1);


        if (loco == null)
        {
            Say("WaypointToDestination: Could not find locomotive attached to consist without brakes cut-out");
            return;
        }

        DebugLog($"Found locomotive in consist. Total consist length: {Math.Round(totalLength)}m");

        var locomotive = (BaseLocomotive)loco;

        AutoEngineerPersistence persistence = new AutoEngineerPersistence(locomotive.KeyValueObject);
        AutoEngineerOrdersHelper helper = new AutoEngineerOrdersHelper(locomotive, persistence);

        // TODO: be smarter about exactly which on the destination track we want to go
        var destinationSpan = destination.Spans[0];

        TrainController shared = TrainController.Shared;
        Graph graph = shared.graph;

        // there are two possible ends for the destination span, we should choose the one that is 
        // furthest away as this is most likely the "end of the track"
        var destinationLocation = destination.Spans[0].lower;

        if (destination.Spans[0].lower == null && destination.Spans[0].upper == null)
        {
            Say("WaypointToDestination ERROR: invalid destination");
            return;
        }

        if (destination.Spans[0].lower == null)
        {
            destinationLocation = destination.Spans[0].upper;
        }
        else
        {
            // compare the distances to both segment ends -- route to the furthest one
            float distanceToLower;
            float travelTimeToLower;
            float distanceToUpper;
            float travelTimeToUpper;

            graph.TryFindDistance(car.LocationA, destination.Spans[0].lower.Value, out distanceToLower, out travelTimeToLower);
            DebugLog($"route to lower is {distanceToLower}m, travel time {travelTimeToLower}");

            graph.TryFindDistance(car.LocationA, destination.Spans[0].upper.Value, out distanceToUpper, out travelTimeToUpper);
            DebugLog($"route to lower is {distanceToUpper}m, travel time {travelTimeToUpper}");


            if (distanceToUpper > distanceToLower)
            {
                DebugLog($"Using upper location as destination");
                destinationLocation = destination.Spans[0].upper;
            }
            else
            {
                DebugLog($"Using lower location as destination");
            }

        }

        DebugLog($"WaypointToDestination: Creating waypoint to {destination.DisplayName} for locomotive {loco.DisplayName}");

        // copied from UI.AutoEngineerDestinationPicker loop which seems to handle waypoint order dispatch
        helper.SetOrdersValue(AutoEngineerMode.Waypoint, null, null, null, (destinationLocation.Value, null));


        var overlayController = UnityEngine.Object.FindObjectOfType<AutoEngineerWaypointOverlayController>();
        if (overlayController == null)
        {
            DebugLog("ERROR: couldn't find AutoEngineerWaypointOverlayController");
            return;
        }

        var waypoint = new OrderWaypoint(Graph.Shared.LocationToString(destinationLocation.Value), null);
        overlayController.WaypointDidChange(waypoint);
    }


    private static void Say(string message)
    {
        Alert alert = new Alert(AlertStyle.Console, AlertLevel.Info, message, TimeWeather.Now.TotalSeconds);
        WindowManager.Shared.Present(alert);
    }

    private static void DebugLog(string message)
    {
        if (!WaypointToDestinationPlugin.Settings.EnableDebug)
        {
            return;
        }

        Say(message);
    }
}
