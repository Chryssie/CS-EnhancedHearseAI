using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;
using System;
using System.Collections.Generic;

namespace EnhancedHearseAI
{
    public class Dispatcher : ThreadingExtensionBase
    {
        private Settings _settings;
        private Helper _helper;

        private bool _initialized;
        private bool _baselined;
        private bool _terminated;

        public static Dictionary<ushort, Cemetery> _cemeteries;
        public static Dictionary<ushort, Claimant> _master;
        private HashSet<ushort> _stopped;
        private HashSet<ushort> _updated;
        private uint _lastProcessedFrame;
        public static Dictionary<ushort, HashSet<ushort>> _oldtargets;
        private Dictionary<ushort, ushort> _lasttargets;
        private Dictionary<ushort, ushort> _PathfindCount;
        private CustomHearseAI _CustomHearseAI;

        public override void OnCreated(IThreading threading)
        {
            _settings = Settings.Instance;
            _helper = Helper.Instance;
            _CustomHearseAI = new CustomHearseAI();
            _initialized = false;
            _baselined = false;
            _terminated = false;

            base.OnCreated(threading);
        }

        public override void OnBeforeSimulationTick()
        {
            if (_terminated) return;

            if (!_helper.GameLoaded)
            {
                _initialized = false;
                _baselined = false;
                return;
            }

            base.OnBeforeSimulationTick();
        }

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if (_terminated) return;

            if (!_helper.GameLoaded) return;

            try
            {
                if (!_initialized)
                {
                    if (!Helper.IsOverwatched())
                    {
                        _helper.NotifyPlayer("Skylines Overwatch not found. Terminating...");
                        _terminated = true;

                        return;
                    }

                    SkylinesOverwatch.Settings.Instance.Enable.BuildingMonitor = true;
                    SkylinesOverwatch.Settings.Instance.Enable.VehicleMonitor = true;

                    _cemeteries = new Dictionary<ushort, Cemetery>();
                    _master = new Dictionary<ushort, Claimant>();
                    _stopped = new HashSet<ushort>();
                    _updated = new HashSet<ushort>();
                    _oldtargets = new Dictionary<ushort, HashSet<ushort>>();
                    _lasttargets = new Dictionary<ushort, ushort>();
                    _PathfindCount = new Dictionary<ushort, ushort>();

                    _initialized = true;

                    _helper.NotifyPlayer("Initialized");
                }
                else if (!_baselined)
                {
                    CreateBaseline();
                }
                else
                {
                    ProcessNewCemeteries();
                    ProcessRemovedCemeteries();

                    ProcessNewPickups();

                    if (!SimulationManager.instance.SimulationPaused && Identity.ModConf.MinimizeHearses)
                    {
                        ProcessIdleHearses();
                    }
                    UpdateHearses();
                    _lastProcessedFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                }
            }
            catch (Exception e)
            {
                string error = String.Format("Failed to {0}\r\n", !_initialized ? "initialize" : "update");
                error += String.Format("Error: {0}\r\n", e.Message);
                error += "\r\n";
                error += "==== STACK TRACE ====\r\n";
                error += e.StackTrace;

                _helper.Log(error);

                if (!_initialized)
                    _terminated = true;
            }

            base.OnUpdate(realTimeDelta, simulationTimeDelta);
        }

        public override void OnReleased()
        {
            _initialized = false;
            _baselined = false;
            _terminated = false;

            base.OnReleased();
        }

        private void CreateBaseline()
        {
            SkylinesOverwatch.Data data = SkylinesOverwatch.Data.Instance;

            foreach (ushort id in data.Cemeteries)
                _cemeteries.Add(id, new Cemetery(id, ref _master, ref _oldtargets));

            foreach (ushort pickup in data.BuildingsWithDead)
            {
                foreach (ushort id in _cemeteries.Keys)
                    _cemeteries[id].AddPickup(pickup);
            }

            _baselined = true;
        }

        private void ProcessNewCemeteries()
        {
            SkylinesOverwatch.Data data = SkylinesOverwatch.Data.Instance;

            foreach (ushort x in data.BuildingsUpdated)
            {
                if (!data.IsCemetery(x))
                    continue;

                if (_cemeteries.ContainsKey(x))
                    continue;
                
                _cemeteries.Add(x, new Cemetery(x, ref _master, ref _oldtargets));

                foreach (ushort pickup in data.BuildingsWithDead)
                    _cemeteries[x].AddPickup(pickup);
            }
        }

        private void ProcessRemovedCemeteries()
        {
            SkylinesOverwatch.Data data = SkylinesOverwatch.Data.Instance;

            foreach (ushort id in data.BuildingsRemoved)
                _cemeteries.Remove(id);
        }

        private void ProcessNewPickups()
        {
            SkylinesOverwatch.Data data = SkylinesOverwatch.Data.Instance;

            foreach (ushort pickup in data.BuildingsUpdated)
            {
                if (data.IsCemetery(pickup))
                    continue;

                if (data.IsBuildingWithDead(pickup))
                {
                    foreach (ushort id in _cemeteries.Keys)
                        _cemeteries[id].AddPickup(pickup);
                }
                else
                {
                    foreach (ushort id in _cemeteries.Keys)
                        _cemeteries[id].AddCheckup(pickup);
                }
            }
        }

        private void ProcessIdleHearses()
        {
            SkylinesOverwatch.Data data = SkylinesOverwatch.Data.Instance;

            foreach (ushort x in data.BuildingsUpdated)
            {
                if (!_cemeteries.ContainsKey(x))
                    continue;

                _cemeteries[x].DispatchIdleVehicle();
            }
        }

        private void UpdateHearses()
        {
            SkylinesOverwatch.Data data = SkylinesOverwatch.Data.Instance;

            foreach (ushort vehicleID in data.VehiclesRemoved)
            {
                if (!data.IsHearse(vehicleID))
                    continue;

                if (_lasttargets.ContainsKey(vehicleID)
                    && Helper.IsBuildingWithDead(_lasttargets[vehicleID]))
                {
                    foreach (ushort id in _cemeteries.Keys)
                        _cemeteries[id].AddPickup(_lasttargets[vehicleID]);
                }
                _oldtargets.Remove(vehicleID);
                if (_lasttargets.ContainsKey(vehicleID))
                {
                    _master.Remove(_lasttargets[vehicleID]);
                }
                _lasttargets.Remove(vehicleID);
                _PathfindCount.Remove(vehicleID);
            }

            if (!SimulationManager.instance.SimulationPaused)
            {
                uint num1 = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 4 & 7u;
                uint num2 = _lastProcessedFrame >> 4 & 7u;
                foreach (ushort vehicleID in data.VehiclesUpdated)
                {
                    if (!data.IsHearse(vehicleID))
                        continue;

                    Vehicle v = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID];

                    if (!_cemeteries.ContainsKey(v.m_sourceBuilding))
                        continue;

                    /* 
                     * If a hearse is loading corpse, we will remove it from the vehicle grid,
                     * so other cars can pass it and more than one hearse can service a building.
                     * It doesn't really make sense that only one hearse can be at a high rise
                     * at a time.
                     */
                    if ((v.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
                    {
                        if (!_stopped.Contains(vehicleID))
                        {
                            Singleton<VehicleManager>.instance.RemoveFromGrid(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID], false);
                            _stopped.Add(vehicleID);
                        }
                        continue;
                    }

                    if ((v.m_flags & (Vehicle.Flags.Stopped | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingLoading | Vehicle.Flags.WaitingCargo)) != Vehicle.Flags.None) continue;
                    if ((v.m_flags & (Vehicle.Flags.Spawned)) == Vehicle.Flags.None) continue;
                    if (v.m_path == 0u) continue;

                    _PathfindCount.Remove(vehicleID);

                    if (_stopped.Contains(vehicleID))
                    {
                        _stopped.Remove(vehicleID);
                    }
                    else if ((v.m_flags & (Vehicle.Flags.WaitingTarget)) == Vehicle.Flags.None)
                    {
                        uint num3 = vehicleID & 7u;
                        if (num1 != num3 && num2 != num3)
                        {
                            _updated.Remove(vehicleID);
                            continue;
                        }
                        else if (_updated.Contains(vehicleID))
                        {
                            continue;
                        }
                    }

                    _updated.Add(vehicleID);

                    int truckStatus = GetHearseStatus(ref v);

                    if (truckStatus == VEHICLE_STATUS_HEARSE_RETURN && _lasttargets.ContainsKey(vehicleID))
                    {
                        if (Helper.IsBuildingWithDead(_lasttargets[vehicleID]))
                        {
                            foreach (ushort id in _cemeteries.Keys)
                                _cemeteries[id].AddPickup(_lasttargets[vehicleID]);
                        }
                        _lasttargets.Remove(vehicleID);
                        continue;
                    }
                    if (truckStatus != VEHICLE_STATUS_HEARSE_COLLECT && truckStatus != VEHICLE_STATUS_HEARSE_WAIT)
                        continue;

                    ushort target = _cemeteries[v.m_sourceBuilding].AssignTarget(vehicleID);

                    if (target != 0 && target != v.m_targetBuilding)
                    {
                        if (Helper.IsBuildingWithDead(v.m_targetBuilding))
                        {
                            foreach (ushort id in _cemeteries.Keys)
                                _cemeteries[id].AddPickup(v.m_targetBuilding);
                        }

                        _master.Remove(v.m_targetBuilding);
                        if (truckStatus == VEHICLE_STATUS_HEARSE_COLLECT)
                        {
                            _lasttargets[vehicleID] = v.m_targetBuilding;
                        }
                        _CustomHearseAI.SetTarget(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID], target);
                    }
                    else
                    {
                        if (_master.ContainsKey(v.m_targetBuilding))
                        {
                            if (_master[v.m_targetBuilding].Hearse != vehicleID)
                                _master[v.m_targetBuilding] = new Claimant(vehicleID, v.m_targetBuilding);
                        }
                        else
                            _master.Add(v.m_targetBuilding, new Claimant(vehicleID, v.m_targetBuilding));
                    }
                }
            }

            foreach (ushort vehicleID in data.Hearses)
            {
                if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None)
                {
                    PathManager instance = Singleton<PathManager>.instance;
                    byte pathFindFlags = instance.m_pathUnits.m_buffer[(int)((UIntPtr)Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_path)].m_pathFindFlags;
                    if ((pathFindFlags & 4) != 0)
                    {
                        _PathfindCount.Remove(vehicleID);
                    }
                    else if ((pathFindFlags & 8) != 0)
                    {
                        int truckStatus = GetHearseStatus(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID]);
                        if (_lasttargets.ContainsKey(vehicleID))
                        {
                            Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_flags &= ~Vehicle.Flags.WaitingPath;
                            Singleton<PathManager>.instance.ReleasePath(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_path);
                            Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_path = 0u;
                            _CustomHearseAI.SetTarget(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID], _lasttargets[vehicleID]);
                            _lasttargets.Remove(vehicleID);
                        }
                        else if ((truckStatus == VEHICLE_STATUS_HEARSE_WAIT || truckStatus == VEHICLE_STATUS_HEARSE_COLLECT)
                            && ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_flags & (Vehicle.Flags.Spawned)) != Vehicle.Flags.None)
                            && _cemeteries.ContainsKey(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_sourceBuilding)
                            && (!_PathfindCount.ContainsKey(vehicleID) || _PathfindCount[vehicleID] < 20))
                        {
                            if (!_PathfindCount.ContainsKey(vehicleID)) _PathfindCount[vehicleID] = 0;
                            _PathfindCount[vehicleID]++;
                            ushort target = _cemeteries[Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_sourceBuilding].GetUnclaimedTarget(vehicleID);
                            if (target == 0)
                            {
                                _PathfindCount[vehicleID] = ushort.MaxValue;
                            }
                            else
                            {
                                if (Dispatcher._oldtargets != null)
                                {
                                    if (!Dispatcher._oldtargets.ContainsKey(vehicleID))
                                        Dispatcher._oldtargets.Add(vehicleID, new HashSet<ushort>());
                                    Dispatcher._oldtargets[vehicleID].Add(target);
                                }
                                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_flags &= ~Vehicle.Flags.WaitingPath;
                                Singleton<PathManager>.instance.ReleasePath(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_path);
                                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_path = 0u;
                                _CustomHearseAI.SetTarget(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID], target);
                            }
                        }
                    }
                }
            }
        }

        public const int VEHICLE_STATUS_HEARSE_WAIT = 0;
        const int VEHICLE_STATUS_HEARSE_RETURN = 1;
        public const int VEHICLE_STATUS_HEARSE_COLLECT = 2;
        const int VEHICLE_STATUS_HEARSE_UNLOAD = 3;
        const int VEHICLE_STATUS_HEARSE_TRANSFER = 4;
        const int VEHICLE_STATUS_CONFUSED = 5;

        public static int GetHearseStatus(ref Vehicle data)
        {
            if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
            {
                if ((data.m_flags & (Vehicle.Flags.Stopped | Vehicle.Flags.WaitingTarget)) != Vehicle.Flags.None)
                {
                    return VEHICLE_STATUS_HEARSE_WAIT;
                }
                if ((data.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
                {
                    return VEHICLE_STATUS_HEARSE_RETURN;
                }
                if (data.m_targetBuilding != 0)
                {
                    return VEHICLE_STATUS_HEARSE_COLLECT;
                }
            }
            else if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
            {
                if ((data.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
                {
                    return VEHICLE_STATUS_HEARSE_RETURN;
                }
                if ((data.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None)
                {
                    return VEHICLE_STATUS_HEARSE_UNLOAD;
                }
                if (data.m_targetBuilding != 0)
                {;
                    return VEHICLE_STATUS_HEARSE_TRANSFER;
                }
            }
            return VEHICLE_STATUS_CONFUSED;
        }
    }
}

