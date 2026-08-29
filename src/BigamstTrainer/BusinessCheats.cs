using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using BigAmbitions.Items;

namespace BigamstTrainer
{
    /// <summary>
    /// Cheats that walk the player's properties: stock, rent, cleanliness, and the
    /// skills of the people working in them.
    /// </summary>
    internal static class BusinessCheats
    {
        /// <summary>
        /// Matches EmployeeInstance's own private MaxSkillValue constant.
        /// </summary>
        private const float SkillMax = 100f;

        /// <summary>
        /// Original daily rent per property, captured the first time rent is zeroed so
        /// the change can be undone. Keyed by street name and number, because
        /// BuildingRegistration instances are replaced on load.
        /// </summary>
        private static readonly Dictionary<string, float> OriginalRent =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private static IModLogger _log;

        internal static void Initialize(IModLogger log) => _log = log;

        internal static void Reset()
        {
            OriginalRent.Clear();
            _log = null;
        }

        /// <summary>
        /// Properties the player actually holds. Everything else in
        /// BuildingRegistrations is world scenery or a rival's.
        /// </summary>
        private static IEnumerable<BuildingRegistration> PlayerProperties()
        {
            List<BuildingRegistration> all = SaveGameManager.Current?.BuildingRegistrations;
            if (all == null)
            {
                yield break;
            }

            foreach (BuildingRegistration property in all)
            {
                if (property != null && property.RentedByPlayer)
                {
                    yield return property;
                }
            }
        }

        private static string KeyOf(BuildingRegistration property) =>
            property.StreetName + "#" + property.StreetNumber;

        /// <summary>
        /// Fills every shelf, fridge and container in every owned property to capacity
        /// and marks the contents paid for.
        /// </summary>
        internal static void RestockEverything(bool quiet = false)
        {
            int holders = 0;
            int units = 0;

            foreach (BuildingRegistration property in PlayerProperties())
            {
                if (property.itemInstances == null)
                {
                    continue;
                }

                foreach (ItemInstance holder in property.itemInstances.Values)
                {
                    if (holder?.cargoInstances == null || holder.cargoInstances.Count == 0)
                    {
                        continue;
                    }

                    bool touched = false;
                    foreach (CargoInstance cargo in holder.cargoInstances)
                    {
                        if (cargo == null)
                        {
                            continue;
                        }

                        int capacity = CapacityOf(cargo, holder);
                        if (capacity <= 0 || cargo.amount >= capacity)
                        {
                            // Still clear the debt on a full shelf.
                            cargo.paid = true;
                            continue;
                        }

                        units += capacity - cargo.amount;
                        cargo.amount = capacity;
                        cargo.paid = true;
                        touched = true;
                    }

                    if (touched)
                    {
                        holders++;
                    }
                }
            }

            if (!quiet)
            {
                _log?.Info($"Restocked {units} unit(s) across {holders} container(s).");
            }
        }

        /// <summary>
        /// Mirrors the game's own capacity rule, including its nested-cargo special case
        /// (see ItemInstance.TryAddCargo). Returns 0 when the item data cannot be
        /// resolved, which is the safe answer — it means "do not touch this".
        /// </summary>
        private static int CapacityOf(CargoInstance cargo, ItemInstance holder)
        {
            try
            {
                if (cargo.nestedCargoInstances != null && cargo.nestedCargoInstances.Count > 0)
                {
                    return 1;
                }

                // GetMaxStockCapacity dereferences ItemCached on both the cargo and the
                // holder, either of which is null for an item the game cannot resolve.
                if (cargo.ItemCached == null || holder.ItemCached == null)
                {
                    return 0;
                }

                return cargo.GetMaxStockCapacity(holder);
            }
            catch (Exception)
            {
                // One unresolvable item must not abort the whole sweep.
                return 0;
            }
        }

        /// <summary>Marks all stock paid without changing quantities.</summary>
        internal static void MarkStockPaid()
        {
            int changed = 0;
            foreach (BuildingRegistration property in PlayerProperties())
            {
                if (property.itemInstances == null)
                {
                    continue;
                }

                foreach (ItemInstance holder in property.itemInstances.Values)
                {
                    if (holder?.cargoInstances == null)
                    {
                        continue;
                    }

                    foreach (CargoInstance cargo in holder.cargoInstances)
                    {
                        if (cargo != null && !cargo.paid)
                        {
                            cargo.paid = true;
                            changed++;
                        }
                    }
                }
            }

            _log?.Info($"Marked {changed} cargo entr(ies) as paid.");
        }

        /// <summary>Removes every dirt spot from every owned property.</summary>
        internal static void CleanEverything(bool quiet = false)
        {
            int properties = 0;
            int spots = 0;

            foreach (BuildingRegistration property in PlayerProperties())
            {
                int count = property.dirtSpots?.Count ?? 0;
                if (count == 0)
                {
                    continue;
                }

                spots += count;
                property.dirtSpots.Clear();
                properties++;
            }

            if (!quiet)
            {
                _log?.Info($"Cleared {spots} dirt spot(s) across {properties} propert(ies).");
            }
        }

        /// <summary>
        /// Sets daily rent to zero on every owned property, remembering the previous
        /// value so <see cref="RestoreRent"/> can put it back.
        /// </summary>
        internal static void SetFreeRent(bool enabled)
        {
            if (!enabled)
            {
                RestoreRent();
                return;
            }

            int changed = 0;
            foreach (BuildingRegistration property in PlayerProperties())
            {
                if (property.RentPerDay <= 0f)
                {
                    continue;
                }

                string key = KeyOf(property);
                if (!OriginalRent.ContainsKey(key))
                {
                    OriginalRent[key] = property.RentPerDay;
                }

                property.RentPerDay = 0f;
                changed++;
            }

            _log?.Info($"Rent waived on {changed} propert(ies).");
        }

        private static void RestoreRent()
        {
            if (OriginalRent.Count == 0)
            {
                return;
            }

            int restored = 0;
            foreach (BuildingRegistration property in PlayerProperties())
            {
                if (OriginalRent.TryGetValue(KeyOf(property), out float original))
                {
                    property.RentPerDay = original;
                    restored++;
                }
            }

            OriginalRent.Clear();
            _log?.Info($"Rent restored on {restored} propert(ies).");
        }

        /// <summary>Raises every skill of every hired employee to full.</summary>
        internal static void MaxEmployeeSkills()
        {
            List<Entities.EmployeeInstance> employees = SaveGameManager.Current?.EmployeeInstances;
            if (employees == null)
            {
                return;
            }

            int people = 0;
            int raised = 0;

            foreach (Entities.EmployeeInstance employee in employees)
            {
                // EmployeeInstance.skills is [Obsolete]; the live list moved onto
                // CharacterData, which is what the game reads today.
                List<Skill> skills = employee?.characterData?.skills;
                if (skills == null)
                {
                    continue;
                }

                bool touched = false;
                foreach (Skill skill in skills)
                {
                    if (skill == null || skill.value >= SkillMax)
                    {
                        continue;
                    }

                    skill.value = SkillMax;
                    raised++;
                    touched = true;
                }

                if (touched)
                {
                    people++;
                }
            }

            _log?.Info($"Raised {raised} skill(s) across {people} employee(s).");
        }
    }
}
