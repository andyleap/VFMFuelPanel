using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FuelPanel
{
    class PumpNetwork
    {
        public static List<PumpNetwork> Networks = new List<PumpNetwork>();

        public PumpNetwork()
        {
            Networks.Add(this);
        }

        List<Pump> Pumps = new List<Pump>();
        List<Vessel> ConnectedVessels = new List<Vessel>();

        Dictionary<Vessel, List<Vessel>> Connections = new Dictionary<Vessel, List<Vessel>>();

        public static PumpNetwork FindNetwork(Vessel v)
        {
            return Networks.FirstOrDefault(pn => pn.VesselInNetwork(v));
        }

        public bool VesselInNetwork(Vessel v)
        {
            return ConnectedVessels.Contains(v);
        }

        public void AddPumpToNetwork(Pump p)
        {
            Pumps.Add(p);
            p.network = this;
        }

        public void ConnectVessel(Vessel from, Vessel to)
        {
            if (!Connections.ContainsKey(from))
            {
                Connections.Add(from, new List<Vessel>());
            }
            if (!Connections.ContainsKey(to))
            {
                Connections.Add(to, new List<Vessel>());
            }
            if (!Connections[from].Contains(to))
            {
                Connections[from].Add(to);
            }
            if (!Connections[to].Contains(from))
            {
                Connections[to].Add(from);
            }

            if (!ConnectedVessels.Contains(from))
            {
                PumpNetwork origNetwork = Networks.FirstOrDefault(pn => pn.VesselInNetwork(from));
                if (origNetwork != null)
                {
                    ConnectedVessels.AddRange(origNetwork.ConnectedVessels);
                    Pumps.AddRange(origNetwork.Pumps);
                    foreach (var kvp in origNetwork.Connections)
                    {
                        Connections.Add(kvp.Key, kvp.Value);
                    }
                    Networks.Remove(origNetwork);
                }
                else
                {
                    ConnectedVessels.Add(from);
                }
            }
        }

        public void DisconnectVessel(Vessel from, Vessel to)
        {
            Connections[from].Remove(to);
            Connections[to].Remove(from);
            bool connected = WalkGraph(from).Contains(to);
			if(!connected)
			{
				PumpNetwork newNet = new PumpNetwork();
				newNet.ConnectedVessels = WalkGraph(to).ToList();
				ConnectedVessels = WalkGraph(from).ToList();
				ILookup<Vessel, Pump> vp = Pumps.ToLookup(p => p.part.vessel);
				newNet.Pumps = newNet.ConnectedVessels.SelectMany(v => vp[v]).ToList();
				Pumps = ConnectedVessels.SelectMany(v => vp[v]).ToList();
				foreach(var v in Connections.Keys.ToList())
				{
					if(!ConnectedVessels.Contains(v))
					{
						newNet.Connections.Add(v, Connections[v]);
						Connections.Remove(v);
					}
				}

			}
        }

        public IEnumerable<Vessel> WalkGraph(Vessel start)
        {
            HashSet<Vessel> Visited = new HashSet<Vessel>();
            Queue<Vessel> NextSteps = new Queue<Vessel>();

            NextSteps.Enqueue(start);
            Visited.Add(start);

            while (NextSteps.Count > 0)
            {
                Vessel step = NextSteps.Dequeue();
                foreach (Vessel nextstep in Connections[step])
                {
                    if (!Visited.Contains(nextstep))
                    {
                        Visited.Add(nextstep);
                        NextSteps.Enqueue(nextstep);
                    }
                }
                yield return step;
            }
        }
    }
}
