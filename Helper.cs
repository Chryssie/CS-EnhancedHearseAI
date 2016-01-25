﻿using ColossalFramework.Plugins;
using System;
using UnityEngine;

namespace EnhancedHearseAI
{
    internal sealed class Helper
    {
        private Helper()
        {
            GameLoaded = false;
        }

        private static readonly Helper _Instance = new Helper();
        public static Helper Instance { get { return _Instance; } }

        internal bool GameLoaded;

        public void Log(string message)
        {
            Debug.Log(String.Format("{0}: {1}", Settings.Instance.Tag, message));
        }

        public void NotifyPlayer(string message)
        {
            DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, String.Format("{0}: {1}", Settings.Instance.Tag, message));
            Log(message);
        }

        public static double GetAngleDifference(double a, double b)
        {
            if (a < 0) a = Math.PI * 2 + a;
            if (b < 0) b = Math.PI * 2 + b;

            double diff = a - b;

            if (diff > Math.PI)
                diff -= Math.PI * 2;
            else if (diff < -Math.PI)
                diff += Math.PI * 2;

            return diff;
        }
    }
}