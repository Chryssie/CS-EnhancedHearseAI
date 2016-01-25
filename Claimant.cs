using ColossalFramework;
using System;

namespace EnhancedHearseAI
{
    public class Claimant
    {
        private readonly ushort _id;
        private readonly ushort _target;

        private float _distance;
        private DateTime _lastUpdated;

        public ushort Hearse { get { return _id; } }

        public float Distance 
        { 
            get 
            { 
                UpdateDistance();
                return _distance;
            } 
        }

        public bool IsValid { get { return !float.IsPositiveInfinity(Distance); } }
        public bool IsChallengable { get { return !float.IsNegativeInfinity(Distance); } }

        public Claimant(ushort id, ushort target)
        {
            _id = id;
            _target = target;

            _distance = float.PositiveInfinity;
            _lastUpdated = new DateTime();
        }

        private void UpdateDistance()
        {
            if (_lastUpdated == SimulationManager.instance.m_currentGameTime) return;

            _lastUpdated = SimulationManager.instance.m_currentGameTime;

            Building b = Singleton<BuildingManager>.instance.m_buildings.m_buffer[_target];
            if (!SkylinesOverwatch.Data.Instance.IsHearse(_id) || b.m_deathProblemTimer <= 0) 
            {
                _distance = float.PositiveInfinity;
                return;
            }

            Vehicle v = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[_id];

            _distance = (b.m_position - v.GetLastFramePosition()).sqrMagnitude;

            if (_distance <= Settings.Instance.ImmediateRange1)
            {
                _distance = float.NegativeInfinity;
                return;
            }

            if (v.m_targetBuilding != _target)
            {
                _distance = float.PositiveInfinity;
                return;
            }
        }
    }
}

