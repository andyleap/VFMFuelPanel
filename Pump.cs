using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FuelPanel
{
    public class Pump : IConfigNode
    {
        public enum Direction
        {
            In,
            Out,
            Balance
        }

        public Guid vesselID;
        public uint partID;

        public Part part;
        public Direction dir;

        public PumpNetwork network;

        public void Load(ConfigNode node)
        {
            vesselID = new Guid(node.GetValue("Vessel"));
            partID = uint.Parse(node.GetValue("Part"));
            dir = (Direction)Enum.Parse(typeof(Direction), node.GetValue("Dir"));
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("Ship", vesselID.ToString());
            node.AddValue("Part", partID);
            node.AddValue("Dir", dir.ToString());
        }
    }
}
