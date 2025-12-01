using System;
using System.Collections.Generic;

namespace RALogicEditor.Models
{
    public class PointerChainArgs : EventArgs
    {
        public string BaseAddress { get; set; }
        public List<long> Offsets { get; set; }

        public PointerChainArgs(string baseAddr, List<long> offsets)
        {
            BaseAddress = baseAddr;
            Offsets = offsets;
        }
    }
}