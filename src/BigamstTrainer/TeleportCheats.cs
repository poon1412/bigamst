using System;
using BAModAPI;
using BigAmbitions.Items;
using Buildings;
using Localizor;
using Helpers;
using Streets;
using UnityEngine;

namespace BigamstTrainer
{
    /// <summary>
    /// Teleporting to the destination marker set on the city map.
    ///
    /// The game already does all of this for the player on foot; the only thing assembled
    /// here is the driving case, where the car has to be moved as well.
    /// </summary>
    internal static class TeleportCheats
    {
        /// <summary>
        /// Street names are localization keys ("ba:street_fifthavenue"), so resolve them
        /// before logging or the message is unreadable.
        /// </summary>
        private static string Describe(Address address) =>
            address.streetName.GetLocalization() + " " + address.streetNumber;

        private static IModLogger _log;

        internal static void Initialize(IModLogger log) => _log = log;

        internal static void Reset() => _log = null;

        /// <summary>
        /// The marker set on the city map, or null when the player has not set one.
        /// </summary>
        private static Address Destination => SaveGameManager.Current?.customDestination;

        /// <summary>
        /// Moves the player to the destination — on foot, or together with the car when
        /// driving. Both land outside, at the building's entrance.
        /// </summary>
        internal static void ToDestination()
        {
            Address destination = Destination;
            if (destination == null)
            {
                _log?.Warn("No destination set. Place a marker on the city map first.");
                return;
            }

            try
            {
                // Resolves to the entrance door, or the drive-in entrance for buildings
                // that have one — which is what you want when arriving by car.
                Vector3 target = GameManager.GetPlayerPositionBasedOnAddress(destination);
                if (target == Vector3.positiveInfinity)
                {
                    _log?.Warn($"Could not locate {Describe(destination)}.");
                    return;
                }

                if (PlayerHelper.IsUsingVehicle)
                {
                    TeleportWithVehicle(target, destination);
                    return;
                }

                // Closes the map and exits any building or underground parking first.
                GameManager.SetPlayerPositionBasedOnAddress(destination);
                _log?.Info($"Teleported to {Describe(destination)}.");
            }
            catch (Exception exception)
            {
                _log?.Error($"Teleport failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Moves the car, which carries the player with it. TeleportVehicleToGround
        /// raycasts for real ground and aligns to the surface, and the TeleportVehicle it
        /// calls resets velocity, wheel simulation, engine RPM and the parking spots —
        /// none of which is safe to skip.
        /// </summary>
        private static void TeleportWithVehicle(Vector3 target, Address destination)
        {
            VehicleController vehicle = InstanceBehavior<GameManager>.Instance?.selectedVehicle;
            if (vehicle == null)
            {
                _log?.Warn("Driving, but no current vehicle was found.");
                return;
            }

            // Keep the car's current facing; there is no sensible heading to infer from
            // an entrance position alone.
            VehicleHelper.TeleportVehicleToGround(vehicle, target, vehicle.transform.rotation);
            _log?.Info($"Teleported with vehicle to {Describe(destination)}.");
        }

        /// <summary>
        /// Loads the destination building's interior. Only meaningful on foot — the game
        /// has no notion of driving indoors.
        /// </summary>
        internal static void InsideDestination()
        {
            Address destination = Destination;
            if (destination == null)
            {
                _log?.Warn("No destination set. Place a marker on the city map first.");
                return;
            }

            if (PlayerHelper.IsUsingVehicle)
            {
                _log?.Warn("Leave the vehicle first — you cannot enter a building while driving.");
                return;
            }

            try
            {
                Building building = BuildingHelper.GetBuilding(destination);
                if (building == null)
                {
                    _log?.Warn($"No building at {Describe(destination)}.");
                    return;
                }

                InstanceBehavior<BuildingManager>.Instance.EnterBuilding(building);
                _log?.Info($"Entered {Describe(destination)}.");
            }
            catch (Exception exception)
            {
                _log?.Error($"Enter building failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Moves the player to the current quest's target, bringing the car if driving.
        ///
        /// The game's own Command_TeleportPlayerToQuestTarget requires the player to be on
        /// foot and silently does nothing otherwise — including when no quest is active —
        /// so the address is read directly and travelled to the same way a map destination
        /// is. That makes it work while driving and lets a missing target be reported
        /// rather than looking like a success.
        /// </summary>
        internal static void ToQuestTarget()
        {
            try
            {
                UI.Guiders.DirectionGuider guider =
                    InstanceBehavior<UI.Guiders.GuidersManager>.Instance?.mainQuestGuider;
                if (guider == null)
                {
                    _log?.Warn("No quest target right now.");
                    return;
                }

                // Prefer the address: it resolves to a building entrance, which is a
                // sensible place to arrive.
                Address address = guider.CurrentAddress;
                if (address != null && !address.IsUndefined())
                {
                    Vector3 entrance = GameManager.GetPlayerPositionBasedOnAddress(address);
                    if (entrance != Vector3.positiveInfinity)
                    {
                        TravelToPosition(entrance, $"quest target at {Describe(address)}");
                        return;
                    }
                }

                // Not every quest points at a building — an early objective can point at a
                // car — and those have no address to resolve. The guider still knows the
                // object it is pointing at, so use its position.
                if (guider.target != null)
                {
                    TravelToPosition(guider.target.position, "quest target");
                    return;
                }

                _log?.Warn("No quest target right now.");
            }
            catch (Exception exception)
            {
                _log?.Error($"Quest teleport failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Travels to a world position on foot or by car, whichever applies. Shared so
        /// every destination behaves the same way.
        /// </summary>
        private static void TravelToPosition(Vector3 target, string what)
        {
            // Nothing extra is needed to bring along what the player is carrying: a
            // shopping cart is a held item (tag isshoppingcontainer), not a vehicle, and
            // SetPlayerPosition below runs ExitFromBuildingCoroutine, so the interior is
            // torn down properly. What teleporting skips is the exit zone's payment gate,
            // so unpaid goods stay the shop's and are lost along with the interior.

            if (PlayerHelper.IsUsingVehicle)
            {
                VehicleController vehicle = InstanceBehavior<GameManager>.Instance?.selectedVehicle;
                if (vehicle == null)
                {
                    _log?.Warn("Driving, but no current vehicle was found.");
                    return;
                }

                VehicleHelper.TeleportVehicleToGround(vehicle, target, vehicle.transform.rotation);
                _log?.Info($"Teleported with vehicle to the {what}.");
                return;
            }

            // The coroutine closes the map and leaves any building or underground parking
            // before warping, which a bare Warp would not do.
            InstanceBehavior<GameManager>.Instance
                ?.StartCoroutine(GameManager.SetPlayerPosition(target));
            _log?.Info($"Teleported to the {what}.");
        }
    }
}
