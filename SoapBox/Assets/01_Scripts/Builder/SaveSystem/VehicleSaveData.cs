using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soapbox.Builder.SaveSystem
{
    /// <summary>Serializable description of one placed part (transforms are root-local).</summary>
    [Serializable]
    public sealed class PartSaveData
    {
        public string partId;
        public string instanceId;
        public Vector3 position;
        public Quaternion rotation;
        public Color paint;
    }

    /// <summary>Serializable description of one connection between two parts' sockets.</summary>
    [Serializable]
    public sealed class ConnectionSaveData
    {
        public string aInstanceId;
        public int aSocket;
        public string bInstanceId;
        public int bSocket;
    }

    /// <summary>Top-level serializable vehicle save (JSON via JsonUtility).</summary>
    [Serializable]
    public sealed class VehicleSaveData
    {
        public string vehicleName = "New Vehicle";
        public string dateIso;
        public List<PartSaveData> parts = new();
        public List<ConnectionSaveData> connections = new();
    }
}
