using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    // 点位向量（Vector）
    public class Vector : Device
    {
        public VectorConfig VectorConfig { get; private set; }

        public Vector(VectorConfig vectorConfig) : base(vectorConfig)
        {
            this.VectorConfig = vectorConfig;
        }

    }

    public enum MoveType { Absolute, Relative, Interpolation_SingleLine, Interpolation_SingleArc, Interpolation_ContiLine, Interpolation_ContiArc }

    public enum PointStatus { Reached, NotReached }


}
