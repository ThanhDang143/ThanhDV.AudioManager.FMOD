using System;
using System.Collections.Generic;

namespace ThanhDV.AudioConductor.FMOD
{
    [Serializable]
    public class FMODReferencesData
    {
        public List<BusEntry> Buses = new();
        public List<EventReferenceEntry> EventReferences = new();
    }
}
