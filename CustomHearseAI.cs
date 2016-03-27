using ColossalFramework;
using ColossalFramework.Math;
using System.Collections.Generic;
using UnityEngine;

namespace EnhancedHearseAI
{
    public class CustomHearseAI
    {
        public void SetTarget(ushort vehicleID, ref Vehicle data, ushort targetBuilding)
        {
            if ((data.m_flags & (Vehicle.Flags.Spawned)) == Vehicle.Flags.None)
            {
                if (Identity.ModConf.MinimizeHearses)
                {
                    if (Dispatcher._cemeteries != null && Dispatcher._cemeteries.ContainsKey(data.m_sourceBuilding))
                    {
                        if (Dispatcher._cemeteries[data.m_sourceBuilding]._primary.Count > 0 || Dispatcher._cemeteries[data.m_sourceBuilding]._secondary.Count > 0 || Dispatcher._cemeteries[data.m_sourceBuilding]._checkups.Count > 0)
                            if ((!Dispatcher._cemeteries[data.m_sourceBuilding]._primary.Contains(targetBuilding) && !Dispatcher._cemeteries[data.m_sourceBuilding]._secondary.Contains(targetBuilding)) || !Helper.IsBuildingWithDead(targetBuilding))
                            {
                                data.Unspawn(vehicleID);
                                return;
                            }
                    }
                    else
                    {
                        data.Unspawn(vehicleID);
                        return;
                    }
                }

                if (Dispatcher._cemeteries != null && Dispatcher._cemeteries.ContainsKey(data.m_sourceBuilding))
                {
                    int max, now;
                    Dispatcher._cemeteries[data.m_sourceBuilding].CaluculateWorkingVehicles(out max, out now);

                    if (now > max)
                    {
                        data.Unspawn(vehicleID);
                        return;
                    }
                }
            }

            BuildingManager instance = Singleton<BuildingManager>.instance;

            ushort current = data.m_targetBuilding;

            uint path = data.m_path;
            byte pathPositionIndex = data.m_pathPositionIndex;
            byte lastPathOffset = data.m_lastPathOffset;
            ushort target = targetBuilding;

            int vehicleStatus = Dispatcher.GetHearseStatus(ref data);
            int retry_max = 1;
            if (vehicleStatus == Dispatcher.VEHICLE_STATUS_HEARSE_WAIT)
            {
                if (Dispatcher._oldtargets != null) Dispatcher._oldtargets.Remove(vehicleID);
                retry_max = 20;
            }

            for (int retry = 0; retry < retry_max; retry++)
            {
                if (retry > 0)
                {
                    if (Dispatcher._cemeteries == null || !Dispatcher._cemeteries.ContainsKey(data.m_sourceBuilding)) break;
                    target = Dispatcher._cemeteries[data.m_sourceBuilding].GetUnclaimedTarget(vehicleID);
                    if (target == 0) break;

                    if (Dispatcher._oldtargets != null)
                    {
                        if (!Dispatcher._oldtargets.ContainsKey(vehicleID))
                            Dispatcher._oldtargets.Add(vehicleID, new HashSet<ushort>());
                        Dispatcher._oldtargets[vehicleID].Add(target);
                    }
                }

                this.RemoveTarget(vehicleID, ref data);
                data.m_targetBuilding = targetBuilding;
                data.m_flags &= ~Vehicle.Flags.WaitingTarget;
                data.m_waitCounter = 0;
                if (targetBuilding != 0)
                {
                    instance.m_buildings.m_buffer[(int)targetBuilding].AddGuestVehicle(vehicleID, ref data);
                }
                else
                {
                    if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
                    {
                        if (data.m_transferSize > 0)
                        {
                            TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
                            offer.Priority = 7;
                            offer.Vehicle = vehicleID;
                            if (data.m_sourceBuilding != 0)
                            {
                                offer.Position = (data.GetLastFramePosition() + Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_position) * 0.5f;
                            }
                            else
                            {
                                offer.Position = data.GetLastFramePosition();
                            }
                            offer.Amount = 1;
                            offer.Active = true;
                            Singleton<TransferManager>.instance.AddOutgoingOffer((TransferManager.TransferReason)data.m_transferType, offer);
                            data.m_flags |= Vehicle.Flags.WaitingTarget;
                        }
                        else
                        {
                            data.m_flags |= Vehicle.Flags.GoingBack;
                        }
                    }
                    if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
                    {
                        VehicleInfo m_info = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].Info;
                        int num = ((HearseAI)m_info.m_vehicleAI).m_corpseCapacity;
                        if (this.ShouldReturnToSource(vehicleID, ref data))
                        {
                            num = (int)data.m_transferSize;
                        }
                        else if (data.m_sourceBuilding != 0)
                        {
                            BuildingInfo info = instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].Info;
                            if (info == null)
                            {
                                return;
                            }
                            int num2;
                            int num3;
                            info.m_buildingAI.GetMaterialAmount(data.m_sourceBuilding, ref instance.m_buildings.m_buffer[(int)data.m_sourceBuilding], TransferManager.TransferReason.Dead, out num2, out num3);
                            num = Mathf.Min(num, num3 - num2);
                        }
                        if ((int)data.m_transferSize < num)
                        {
                            TransferManager.TransferOffer offer2 = default(TransferManager.TransferOffer);
                            offer2.Priority = 7;
                            offer2.Vehicle = vehicleID;
                            if (data.m_sourceBuilding != 0)
                            {
                                offer2.Position = (data.GetLastFramePosition() + Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_position) * 0.5f;
                            }
                            else
                            {
                                offer2.Position = data.GetLastFramePosition();
                            }
                            offer2.Amount = 1;
                            offer2.Active = true;
                            Singleton<TransferManager>.instance.AddIncomingOffer((TransferManager.TransferReason)data.m_transferType, offer2);
                            data.m_flags |= Vehicle.Flags.WaitingTarget;
                        }
                        else
                        {
                            data.m_flags |= Vehicle.Flags.GoingBack;
                        }
                    }
                }

                if ((targetBuilding == 0 ||
                        (vehicleStatus != Dispatcher.VEHICLE_STATUS_HEARSE_COLLECT && vehicleStatus != Dispatcher.VEHICLE_STATUS_HEARSE_WAIT)))
                {
                    if (!StartPathFind(vehicleID, ref data))
                    {
                        data.Unspawn(vehicleID);
                    }
                    return;
                }

                if (StartPathFind(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID]))
                {
                    if (Dispatcher._oldtargets != null)
                    {
                        if (!Dispatcher._oldtargets.ContainsKey(vehicleID))
                            Dispatcher._oldtargets.Add(vehicleID, new HashSet<ushort>());
                        Dispatcher._oldtargets[vehicleID].Add(target);
                    }
                    if (Dispatcher._master != null)
                    {
                        if (Dispatcher._master.ContainsKey(target))
                        {
                            if (Dispatcher._master[target].Vehicle != vehicleID)
                                Dispatcher._master[target] = new Claimant(vehicleID, target);
                        }
                        else if (target != 0)
                            Dispatcher._master.Add(target, new Claimant(vehicleID, target));
                    }
                    return;
                }
            }

            if (vehicleStatus == Dispatcher.VEHICLE_STATUS_HEARSE_COLLECT)
            {
                target = current;
                RemoveTarget(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID]);
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_targetBuilding = target;
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_path = path;
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_pathPositionIndex = pathPositionIndex;
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_lastPathOffset = lastPathOffset;
                Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)current].AddGuestVehicle(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID]);

                if (Dispatcher._master != null)
                {
                    if (Dispatcher._master.ContainsKey(target))
                    {
                        if (Dispatcher._master[target].Vehicle != vehicleID)
                            Dispatcher._master[target] = new Claimant(vehicleID, target);
                    }
                    else if (target != 0)
                        Dispatcher._master.Add(target, new Claimant(vehicleID, target));
                }
            }
            else
            {
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].Unspawn(vehicleID);
            }
        }

        private bool ShouldReturnToSource(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_sourceBuilding != 0)
            {
                BuildingManager instance = Singleton<BuildingManager>.instance;
                if ((instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_flags & (Building.Flags.Active | Building.Flags.Downgrading)) != Building.Flags.Active && instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_fireIntensity == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void RemoveTarget(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_targetBuilding != 0)
            {
                Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_targetBuilding].RemoveGuestVehicle(vehicleID, ref data);
                data.m_targetBuilding = 0;
            }
        }

        protected bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData)
        {
            VehicleInfo m_info = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].Info;
            if ((vehicleData.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None)
            {
                return true;
            }
            if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
            {
                if (vehicleData.m_sourceBuilding != 0)
                {
                    BuildingManager instance = Singleton<BuildingManager>.instance;
                    BuildingInfo info = instance.m_buildings.m_buffer[(int)vehicleData.m_sourceBuilding].Info;
                    Randomizer randomizer = new Randomizer((int)vehicleID);
                    Vector3 vector;
                    Vector3 endPos;
                    info.m_buildingAI.CalculateUnspawnPosition(vehicleData.m_sourceBuilding, ref instance.m_buildings.m_buffer[(int)vehicleData.m_sourceBuilding], ref randomizer, m_info, out vector, out endPos);
                    return StartPathFind(vehicleID, ref vehicleData, vehicleData.m_targetPos3, endPos);
                }
            }
            else if (vehicleData.m_targetBuilding != 0)
            {
                BuildingManager instance2 = Singleton<BuildingManager>.instance;
                BuildingInfo info2 = instance2.m_buildings.m_buffer[(int)vehicleData.m_targetBuilding].Info;
                Randomizer randomizer2 = new Randomizer((int)vehicleID);
                Vector3 vector2;
                Vector3 endPos2;
                info2.m_buildingAI.CalculateUnspawnPosition(vehicleData.m_targetBuilding, ref instance2.m_buildings.m_buffer[(int)vehicleData.m_targetBuilding], ref randomizer2, m_info, out vector2, out endPos2);
                return StartPathFind(vehicleID, ref vehicleData, vehicleData.m_targetPos3, endPos2);
            }
            return false;
        }

        protected static bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos)
        {
            return StartPathFind(vehicleID, ref vehicleData, startPos, endPos, true, true);
        }

        protected static bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            VehicleInfo info = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].Info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2) && PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, false, false, 32f, out endPosA, out endPosB, out num3, out num4))
            {
                if (!startBothWays || num < 10f)
                {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || num3 < 10f)
                {
                    endPosB = default(PathUnit.Position);
                }
                uint path;
                if (Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, IsHeavyVehicle(), IgnoreBlocked(vehicleID, ref vehicleData), false, false))
                {
                    if (vehicleData.m_path != 0u)
                    {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }
                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            }
            return false;
        }

        protected static bool IsHeavyVehicle()
        {
            return false;
        }

        protected static bool IgnoreBlocked(ushort vehicleID, ref Vehicle vehicleData)
        {
            return false;
        }
    }
}
